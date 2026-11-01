using OpenConquer.Content.Map;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Import;

/// <summary>
/// Builds a content set holding exactly the retail files the implemented slices read.
/// </summary>
internal static class ContentSetImporter
{
    private const string ManifestFileName = "manifest.json";
    private const string PayloadDirectoryName = "payload";
    private const int SignatureHeaderLength = 12;

    public static ContentManifest Import(string sourceRootPath, string destinationRootPath, uint? mapDataId = null,
        bool allMaps = false, bool availableMaps = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRootPath);

        if (availableMaps && (allMaps || mapDataId is not null))
        {
            throw new ArgumentException("Available maps cannot be combined with another map selection.");
        }

        ImportSourceRoot sourceRoot = ImportSourceRoot.Open(sourceRootPath);
        string destinationRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationRootPath));

        if (Directory.Exists(destinationRoot) || File.Exists(destinationRoot))
        {
            throw new IOException($"Content-set destination '{destinationRoot}' already exists.");
        }

        string destinationParent = Path.GetDirectoryName(destinationRoot) ?? throw new ArgumentException("The content-set destination must have a parent directory.", nameof(destinationRootPath));

        Directory.CreateDirectory(destinationParent);

        string stagingRoot = Path.Combine(destinationParent, $".{Path.GetFileName(destinationRoot)}.import-{Guid.NewGuid():N}");

        Directory.CreateDirectory(stagingRoot);

        try
        {
            ContentManifest manifest = BuildContentSet(sourceRoot, stagingRoot, mapDataId, allMaps, availableMaps);

            Directory.Move(stagingRoot, destinationRoot);

            return manifest;
        }
        catch
        {
            try
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, recursive: true);
                }
            }
            catch
            {
                // Preserve the import failure that initiated staging cleanup.
            }

            throw;
        }
    }

    private static ContentManifest BuildContentSet(ImportSourceRoot sourceRoot, string stagingRoot,
        uint? mapDataId, bool allMaps, bool availableMaps)
    {
        IReadOnlyList<MissingMapArchive>? missingMaps = availableMaps ? FindMissingMapArchives(sourceRoot) : null;
        HashSet<uint>? excluded = missingMaps is null ? null : [.. missingMaps.Select(static map => map.MapDataId)];
        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(
            sourceRoot.ContentSource, mapDataId, allMaps || availableMaps, excluded);

        if (closure.Count == 0)
        {
            throw new InvalidOperationException("The client content closure resolved to no files.");
        }

        string payloadRoot = Path.Combine(stagingRoot, PayloadDirectoryName);
        Dictionary<string, ContentManifestEntry> entriesByPathKey = new(StringComparer.Ordinal);

        foreach (ClientContentRequirement requirement in closure)
        {
            using Stream source = sourceRoot.OpenRequiredRead(requirement, out string sourcePath);

            ContentPath.Validate(sourcePath);

            string pathKey = ContentPath.ToKey(sourcePath);

            if (entriesByPathKey.TryGetValue(pathKey, out ContentManifestEntry existing))
            {
                throw new InvalidDataException($"Closure paths '{existing.SourcePath}' and '{sourcePath}' collide case-insensitively.");
            }

            if (!source.CanSeek)
            {
                throw new InvalidOperationException($"Retail content '{sourcePath}' does not expose a seekable source stream.");
            }

            long length = source.Length;
            string signature = ClassifySignature(source);
            string sha256 = ContentPayloadCopier.CopyAndHash(source, payloadRoot, pathKey, length);

            entriesByPathKey.Add(pathKey, new ContentManifestEntry(sourcePath, pathKey, length, sha256, signature));
        }

        ContentManifest manifest = new(sourceRoot.ClientVersion, sourceRoot.VersionMarkerSha256,
            [.. entriesByPathKey.Values.OrderBy(entry => entry.SourcePath, StringComparer.Ordinal)], missingMaps);

        WriteManifest(Path.Combine(stagingRoot, ManifestFileName), manifest);

        return manifest;
    }

    private static List<MissingMapArchive> FindMissingMapArchives(ImportSourceRoot source)
    {
        GameMapCatalog catalog = GameMapCatalog.Load(source.ContentSource);
        List<MissingMapArchive> missing = [];

        foreach (uint id in catalog.MapDataIds.Order())
        {
            GameMapCatalog.Terrain terrain = catalog.TryGetTerrain(id, out GameMapCatalog.Terrain? found)
                ? found
                : throw new InvalidOperationException("Map catalog changed during import.");

            if (!terrain.Path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Map data ID {id} does not reference a .7z archive.");
            }

            if (!source.ContainsLooseFile(terrain.Path))
            {
                missing.Add(new MissingMapArchive(id, terrain.Path));
            }
        }

        if (missing.Count == catalog.Count)
        {
            throw new InvalidDataException("The source contains none of its catalogued map archives.");
        }

        return missing;
    }

    private static string ClassifySignature(Stream source)
    {
        Span<byte> header = stackalloc byte[SignatureHeaderLength];
        int readLength = source.ReadAtLeast(header, minimumBytes: SignatureHeaderLength, throwOnEndOfStream: false);

        source.Position = 0;

        return ContentSignature.Classify(header[..readLength]);
    }

    private static void WriteManifest(string manifestPath, ContentManifest manifest)
    {
        using FileStream stream = new(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        ContentManifestWriter.Write(stream, manifest);
    }
}
