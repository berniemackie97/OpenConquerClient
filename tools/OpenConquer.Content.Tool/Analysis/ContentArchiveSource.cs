using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Analysis;

internal sealed class ContentArchiveSource : IClientContentSource, IDisposable
{
    private readonly ZipArchive _archive;
    private readonly Dictionary<string, ZipArchiveEntry> _entries;

    public ContentArchiveSource(string archivePath, ContentManifest manifest)
    {
        _archive = ZipFile.OpenRead(archivePath);
        try
        {
            _entries = ValidateEntries(_archive, manifest);
        }
        catch
        {
            _archive.Dispose();
            throw;
        }
    }

    public bool TryOpenRead(string contentPath, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        ContentPath.Validate(contentPath);
        if (mode != ContentLookupMode.PackageOnly &&
            _entries.TryGetValue(ContentPath.ToKey(contentPath), out ZipArchiveEntry? entry))
        {
            stream = entry.Open();
            return true;
        }

        stream = null;
        return false;
    }

    public Stream OpenRequiredRead(string contentPath, ContentLookupMode mode)
    {
        if (TryOpenRead(contentPath, mode, out Stream? stream))
        {
            return stream;
        }

        throw new FileNotFoundException($"Archive content '{contentPath}' was not found.");
    }

    public long CompressedBytesForNewPayloads(ContentManifest manifest, ContentManifest? previous)
    {
        if (previous is null)
        {
            return 0;
        }

        HashSet<(string Sha256, long Length)> oldPayloads = previous.Entries
            .Select(entry => (entry.Sha256, entry.Length)).ToHashSet();
        HashSet<(string Sha256, long Length)> counted = [];
        long total = 0;

        foreach (ContentManifestEntry entry in manifest.Entries)
        {
            (string Sha256, long Length) identity = (entry.Sha256, entry.Length);
            if (!oldPayloads.Contains(identity) && counted.Add(identity))
            {
                total = checked(total + _entries[entry.PathKey].CompressedLength);
            }
        }

        return total;
    }

    public void Dispose() => _archive.Dispose();

    private static Dictionary<string, ZipArchiveEntry> ValidateEntries(ZipArchive archive, ContentManifest manifest)
    {
        Dictionary<string, ContentManifestEntry> expected = manifest.Entries
            .ToDictionary(entry => entry.PathKey, StringComparer.Ordinal);
        if (archive.Entries.Count != expected.Count)
        {
            throw new InvalidDataException("Archive entry count differs from the verified manifest.");
        }

        Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.Ordinal);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            ContentPath.Validate(entry.FullName);
            if (!expected.TryGetValue(entry.FullName, out ContentManifestEntry declaration) ||
                !entries.TryAdd(entry.FullName, entry))
            {
                throw new InvalidDataException($"Archive entry '{entry.FullName}' is not uniquely declared.");
            }

            if (entry.Length != declaration.Length)
            {
                throw new InvalidDataException($"Archive entry '{entry.FullName}' differs in length.");
            }

            using Stream stream = entry.Open();
            string digest = Convert.ToHexStringLower(SHA256.HashData(stream));
            if (!string.Equals(digest, declaration.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Archive entry '{entry.FullName}' differs in SHA-256.");
            }
        }

        return entries;
    }
}
