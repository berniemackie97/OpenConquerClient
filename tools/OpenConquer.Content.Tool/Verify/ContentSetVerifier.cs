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

        Dictionary<string, ContentManifestEntry> expectedByPathKey = manifest.Entries.ToDictionary(entry => entry.PathKey, StringComparer.Ordinal);
        HashSet<string> observedPathKeys = new(StringComparer.Ordinal);

        VerifyPayloadDirectory(payloadRoot, payloadRoot, expectedByPathKey, observedPathKeys);

        string[] missingSourcePaths = expectedByPathKey
            .Where(entry => !observedPathKeys.Contains(entry.Key))
            .Select(entry => entry.Value.SourcePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

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

    private static void VerifyPayloadDirectory(string payloadRoot, string directoryPath, IReadOnlyDictionary<string, ContentManifestEntry> expectedByPathKey, HashSet<string> observedPathKeys)
    {
        HostFileSystemGuard.RequireDirectory(directoryPath, "content-set payload directory");

        foreach (string childDirectoryPath in Directory.EnumerateDirectories(directoryPath).Order(StringComparer.Ordinal))
        {
            VerifyPayloadDirectory(payloadRoot, childDirectoryPath, expectedByPathKey, observedPathKeys);
        }

        foreach (string filePath in Directory.EnumerateFiles(directoryPath).Order(StringComparer.Ordinal))
        {
            VerifyPayloadFile(payloadRoot, filePath, expectedByPathKey, observedPathKeys);
        }
    }

    private static void VerifyPayloadFile(string payloadRoot, string filePath, IReadOnlyDictionary<string, ContentManifestEntry> expectedByPathKey, HashSet<string> observedPathKeys)
    {
        FileInfo file = new(filePath);

        HostFileSystemGuard.RequireNotLinked(file, "content-set payload file", filePath);

        string payloadPath = Path.GetRelativePath(payloadRoot, filePath).Replace('\\', '/');

        ContentPath.Validate(payloadPath);

        string pathKey = ContentPath.ToKey(payloadPath);

        if (!string.Equals(payloadPath, pathKey, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Content-set payload '{payloadPath}' is not stored at its canonical path '{pathKey}'.");
        }

        if (!expectedByPathKey.TryGetValue(pathKey, out ContentManifestEntry expected))
        {
            throw new InvalidDataException($"Content-set payload '{payloadPath}' is not declared in the manifest.");
        }

        if (!observedPathKeys.Add(pathKey))
        {
            throw new InvalidDataException($"Content-set payload contains duplicate canonical path '{pathKey}'.");
        }

        if (file.Length != expected.Length)
        {
            throw new InvalidDataException($"Content-set payload '{payloadPath}' is {file.Length} bytes; the manifest declares {expected.Length}.");
        }

        string observedSignature = ContentSignature.ClassifyFile(filePath);

        if (!string.Equals(observedSignature, expected.Signature, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Content-set payload '{payloadPath}' has signature '{observedSignature}'; the manifest declares '{expected.Signature}'.");
        }

        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, FileOptions.SequentialScan);

        string observedSha256 = Convert.ToHexStringLower(SHA256.HashData(stream));

        if (!string.Equals(observedSha256, expected.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Content-set payload '{payloadPath}' failed SHA-256 verification.");
        }
    }

    private static void VerifyImplementedClosure(string payloadRoot, ContentManifest manifest)
    {
        IReadOnlyList<ClientContentRequirement> closure;

        try
        {
            closure = ClientContentClosure.Resolve(new ClientContentRoot(payloadRoot));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The content-set payload cannot resolve the implemented client content closure.", exception);
        }

        if (closure.Count == 0)
        {
            throw new InvalidDataException("The implemented client content closure resolved to no files.");
        }

        Dictionary<string, string> closurePathsByKey = new(StringComparer.Ordinal);

        foreach (ClientContentRequirement requirement in closure)
        {
            string normalizedPath = requirement.ContentPath.Replace('\\', '/');

            ContentPath.Validate(normalizedPath);

            string pathKey = ContentPath.ToKey(normalizedPath);

            if (!closurePathsByKey.TryAdd(pathKey, normalizedPath))
            {
                throw new InvalidDataException($"The implemented client content closure contains a case-insensitive collision on '{normalizedPath}'.");
            }
        }

        Dictionary<string, string> manifestPathsByKey = manifest.Entries.ToDictionary(entry => entry.PathKey, entry => entry.SourcePath, StringComparer.Ordinal);

        string[] missingFromManifest = closurePathsByKey.Where(entry => !manifestPathsByKey.ContainsKey(entry.Key)).Select(entry => entry.Value).Order(StringComparer.Ordinal).ToArray();
        string[] outsideImplementedClosure = manifestPathsByKey.Where(entry => !closurePathsByKey.ContainsKey(entry.Key)).Select(entry => entry.Value).Order(StringComparer.Ordinal).ToArray();

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

        throw new InvalidDataException($"The content-set manifest does not exactly match the implemented client content closure ({string.Join("; ", differences)}).");
    }
}
