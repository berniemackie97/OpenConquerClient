using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;

namespace OpenConquer.Content.Map;

/// <summary>Extracts the single DMap payload from a retail 5517 map archive.</summary>
public static class GameMapArchive
{
    private const int MaximumArchiveBytes = 128 * 1024;
    private const int MaximumDecodedBytes = 32 * 1024 * 1024;

    public static byte[] Decode(ReadOnlySpan<byte> encoded, string contentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);

        string path = ClientContentPath.NormalizeVirtualPath(contentPath, nameof(contentPath), 260);
        if (!path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Map archives must have the retail .7z extension.", nameof(contentPath));
        }

        ReadOnlySpan<byte> signature = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];
        if (encoded.Length < 32 || encoded.Length > MaximumArchiveBytes ||
            !encoded[..6].SequenceEqual(signature))
        {
            throw new InvalidDataException("Map archive has an invalid signature or size.");
        }

        string archiveName = path[(path.LastIndexOf('/') + 1)..];
        string expectedEntry = $"{archiveName[..^3]}.DMap";

        using MemoryStream stream = new(encoded.ToArray(), writable: false);
        using IArchive archive = SevenZipArchive.OpenArchive(stream, ReaderOptions.ForExternalStream);

        byte[]? result = null;
        foreach (var entry in archive.Entries)
        {
            if (result is not null || entry.IsDirectory || !entry.IsComplete ||
                !string.Equals(entry.Key, expectedEntry, StringComparison.OrdinalIgnoreCase) ||
                entry.Size <= 0 || entry.Size > MaximumDecodedBytes)
            {
                throw new InvalidDataException("Map archive contains an unsupported entry or decoded length.");
            }

            byte[] decoded = new byte[(int)entry.Size];
            using (Stream data = entry.OpenEntryStream())
            {
                data.ReadExactly(decoded);
                if (data.ReadByte() != -1)
                {
                    throw new InvalidDataException("Map archive entry exceeds its declared decoded length.");
                }
            }

            result = decoded;
        }

        return result ?? throw new InvalidDataException("Map archive contains no DMap entry.");
    }
}
