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

    public static ContentManifest Import(string sourceRootPath, string destinationRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRootPath);

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
            ContentManifest manifest = BuildContentSet(sourceRoot, stagingRoot);

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

    private static ContentManifest BuildContentSet(ImportSourceRoot sourceRoot, string stagingRoot)
    {
        IReadOnlyList<ClientContentRequirement> closure = ClientContentClosure.Resolve(new ClientContentRoot(sourceRoot.RootPath));

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
            string sha256 = ContentPayloadCopier.CopyAndHash(source, payloadRoot, sourcePath, length);

            entriesByPathKey.Add(pathKey, new ContentManifestEntry(sourcePath, pathKey, length, sha256, signature));
        }

        ContentManifest manifest = new(sourceRoot.ClientVersion, sourceRoot.VersionMarkerSha256, [.. entriesByPathKey.Values.OrderBy(entry => entry.SourcePath, StringComparer.Ordinal)]);

        WriteManifest(Path.Combine(stagingRoot, ManifestFileName), manifest);

        return manifest;
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
