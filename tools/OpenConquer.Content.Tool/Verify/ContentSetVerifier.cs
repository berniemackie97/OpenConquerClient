using System.Security.Cryptography;
using OpenConquer.Content.Tool.Import;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Verify;

/// <summary>
/// Verifies that a content set on disk exactly matches both its manifest and the content closure declared by the currently implemented client.
/// </summary>
internal static class ContentSetVerifier
{
    private const string ManifestFileName = "manifest.json";
    private const string PayloadDirectoryName = "payload";

    public static ContentManifest Verify(string contentSetRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSetRootPath);

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentSetRootPath));

        HostFileSystemGuard.RequireDirectory(root, "content-set root");

        ContentManifest manifest = ReadManifest(Path.Combine(root, ManifestFileName));

        string payloadRoot = Path.Combine(root, PayloadDirectoryName);

        HostFileSystemGuard.RequireDirectory(payloadRoot, "content-set payload directory");

        Dictionary<string, ContentManifestEntry> expectedBySourcePath = manifest.Entries.ToDictionary(entry => entry.SourcePath, StringComparer.Ordinal);

        HashSet<string> observedSourcePaths = new(StringComparer.Ordinal);

        VerifyPayloadDirectory(payloadRoot, payloadRoot, expectedBySourcePath, observedSourcePaths);

        string[] missingSourcePaths = expectedBySourcePath.Keys.Where(sourcePath => !observedSourcePaths.Contains(sourcePath)).Order(StringComparer.Ordinal).ToArray();

        if (missingSourcePaths.Length > 0)
        {
            throw new InvalidDataException($"The content set is missing {missingSourcePaths.Length} declared payload file(s): {string.Join(", ", missingSourcePaths)}.");
        }

        VerifyImplementedClosure(payloadRoot, manifest);

        return manifest;
    }

    private static ContentManifest ReadManifest(string manifestPath)
    {
        FileInfo manifestFile = HostFileSystemGuard.RequireFile(manifestPath, "content-set manifest");

        if (manifestFile.Length > ContentManifestReader.MaximumLength)
        {
            throw new InvalidDataException($"The content-set manifest is {manifestFile.Length} bytes; the limit is {ContentManifestReader.MaximumLength} bytes.");
        }

        using FileStream stream = new(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, FileOptions.SequentialScan);

        return ContentManifestReader.Read(stream);
    }

    private static void VerifyPayloadDirectory(string payloadRoot, string directoryPath, IReadOnlyDictionary<string, ContentManifestEntry> expectedBySourcePath, HashSet<string> observedSourcePaths)
    {
        HostFileSystemGuard.RequireDirectory(directoryPath, "content-set payload directory");

        foreach (string childDirectoryPath in Directory.EnumerateDirectories(directoryPath).Order(StringComparer.Ordinal))
        {
            VerifyPayloadDirectory(payloadRoot, childDirectoryPath, expectedBySourcePath, observedSourcePaths);
        }

        foreach (string filePath in Directory.EnumerateFiles(directoryPath).Order(StringComparer.Ordinal))
        {
            VerifyPayloadFile(payloadRoot, filePath, expectedBySourcePath, observedSourcePaths);
        }
    }

    private static void VerifyPayloadFile(string payloadRoot, string filePath, IReadOnlyDictionary<string, ContentManifestEntry> expectedBySourcePath, HashSet<string> observedSourcePaths)
    {
        FileInfo file = new(filePath);

        HostFileSystemGuard.RequireNotLinked(file, "content-set payload file", filePath);

        string sourcePath = Path.GetRelativePath(payloadRoot, filePath).Replace('\\', '/');

        ContentPath.Validate(sourcePath);

        if (!expectedBySourcePath.TryGetValue(sourcePath, out ContentManifestEntry expected))
        {
            throw new InvalidDataException($"Content-set payload '{sourcePath}' is not declared in the manifest.");
        }

        if (file.Length != expected.Length)
        {
            throw new InvalidDataException($"Content-set payload '{sourcePath}' is {file.Length} bytes; the manifest declares {expected.Length}.");
        }

        string observedSignature = ContentSignature.ClassifyFile(filePath);

        if (!string.Equals(observedSignature, expected.Signature, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Content-set payload '{sourcePath}' has signature '{observedSignature}'; the manifest declares '{expected.Signature}'.");
        }

        using (FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, FileOptions.SequentialScan))
        {
            string observedSha256 = Convert.ToHexStringLower(SHA256.HashData(stream));

            if (!string.Equals(observedSha256, expected.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Content-set payload '{sourcePath}' failed SHA-256 verification.");
            }
        }

        observedSourcePaths.Add(sourcePath);
    }

    private static void VerifyImplementedClosure(string payloadRoot, ContentManifest manifest)
    {
        IReadOnlyList<string> closure;

        try
        {
            closure = ClientContentClosure.Resolve(new ClientContentRoot(payloadRoot));
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidDataException
                        or IOException
                        or UnauthorizedAccessException
            )
        {
            throw new InvalidDataException("The content-set payload cannot resolve the implemented client content closure.", exception);
        }

        if (closure.Count == 0)
        {
            throw new InvalidDataException("The implemented client content closure resolved to no files.");
        }

        Dictionary<string, string> closurePathsByKey = new(StringComparer.Ordinal);

        foreach (string contentPath in closure)
        {
            string normalizedPath = contentPath.Replace('\\', '/');

            ContentPath.Validate(normalizedPath);

            string pathKey = ContentPath.ToKey(normalizedPath);

            if (!closurePathsByKey.TryAdd(pathKey, normalizedPath))
            {
                throw new InvalidDataException($"The implemented client content closure contains a case-insensitive collision on '{normalizedPath}'.");
            }
        }

        Dictionary<string, string> manifestPathsByKey = manifest.Entries.ToDictionary(entry => entry.PathKey, entry => entry.SourcePath, StringComparer.Ordinal);

        string[] missingFromManifest = closurePathsByKey.Where(entry => !manifestPathsByKey.ContainsKey(entry.Key))
            .Select(entry => entry.Value).Order(StringComparer.Ordinal).ToArray();

        string[] outsideImplementedClosure = manifestPathsByKey.Where(entry => !closurePathsByKey.ContainsKey(entry.Key))
            .Select(entry => entry.Value).Order(StringComparer.Ordinal).ToArray();

        if (missingFromManifest.Length == 0 && outsideImplementedClosure.Length == 0)
        {
            return;
        }

        List<string> differences = [];

        if (missingFromManifest.Length > 0)
        {
            differences.Add($"missing from manifest: {string.Join(", ", missingFromManifest)}");
        }

        if (outsideImplementedClosure.Length > 0)
        {
            differences.Add($"outside implemented closure: {string.Join(", ", outsideImplementedClosure)}");
        }

        throw new InvalidDataException("The content-set manifest does not exactly match the implemented client content "
                                       + $"closure ({string.Join("; ", differences)}).");
    }
}
