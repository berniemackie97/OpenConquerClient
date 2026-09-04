using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Content.Wdf;

/// <summary>
/// Reads and indexes one retail WDF archive.
/// </summary>
/// <remarks>
/// <para>
/// The verified retail format contains a 12-byte header, a packed payload region, and a sorted
/// array of 16-byte index records. The native reader binary-searches that sorted UID table.
/// </para>
/// <para>
/// Legacy archive data is treated as untrusted input. Structural validation is therefore stricter
/// than the native implementation where doing so does not change behavior for valid retail 5517
/// archives.
/// </para>
/// </remarks>
internal sealed class WdfArchive
{
    public const uint Magic = 0x57444650;
    public const int HeaderLength = 12;
    public const int EntryLength = 16;

    /// <summary>
    /// Modern resource-safety ceiling for a single WDF index.
    /// </summary>
    /// <remarks>
    /// This is not a native-format limit. The surveyed retail 5517 archives contain 10,274
    /// entries in <c>c3.wdf</c> and 14,739 entries in <c>data.wdf</c>. The ceiling leaves
    /// substantial compatibility headroom while preventing an untrusted header from driving an
    /// unbounded index allocation.
    /// </remarks>
    internal const int MaximumEntryCount = 100_000;

    private readonly string _archivePath;
    private readonly WdfEntry[] _entries;

    private WdfArchive(string archivePath, WdfEntry[] entries)
    {
        _archivePath = archivePath;
        _entries = entries;
    }

    public int EntryCount => _entries.Length;

    public static WdfArchive Open(string archivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        string normalizedPath = Path.GetFullPath(archivePath);
        FileInfo archiveFile = new(normalizedPath);

        if (!archiveFile.Exists)
        {
            throw new FileNotFoundException($"WDF archive '{normalizedPath}' does not exist.", normalizedPath);
        }

        if (archiveFile.LinkTarget is not null || (archiveFile.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"WDF archive '{normalizedPath}' is a symbolic link or reparse point.");
        }

        using FileStream stream = new(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, FileOptions.SequentialScan);

        Span<byte> header = stackalloc byte[HeaderLength];
        ReadExactly(stream, header, "WDF header");

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header);

        if (magic != Magic)
        {
            throw new InvalidDataException($"WDF archive '{Path.GetFileName(normalizedPath)}' has invalid magic 0x{magic:X8}.");
        }

        uint declaredCount = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);

        if (declaredCount > MaximumEntryCount)
        {
            throw new InvalidDataException($"WDF archive '{Path.GetFileName(normalizedPath)}' declares {declaredCount} entries, which exceeds the supported safety limit of {MaximumEntryCount}.");
        }

        int entryCount = checked((int)declaredCount);

        uint tableOffset = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);

        long tableLength = checked((long)entryCount * EntryLength);
        long tableEnd = checked((long)tableOffset + tableLength);

        if (tableOffset < HeaderLength || tableEnd > stream.Length)
        {
            throw new InvalidDataException($"WDF archive '{Path.GetFileName(normalizedPath)}' has an out-of-range entry table.");
        }

        stream.Position = tableOffset;

        WdfEntry[] entries = new WdfEntry[entryCount];

        Span<byte> encodedEntry = stackalloc byte[EntryLength];

        uint previousUid = 0;

        for (int index = 0; index < entryCount; index++)
        {
            ReadExactly(stream, encodedEntry, $"WDF entry {index}");

            uint uid = BinaryPrimitives.ReadUInt32LittleEndian(encodedEntry);
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(encodedEntry[4..]);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(encodedEntry[8..]);
            uint reserved = BinaryPrimitives.ReadUInt32LittleEndian(encodedEntry[12..]);

            if (reserved != 0)
            {
                throw new InvalidDataException($"WDF entry {index} (0x{uid:X8}) has non-zero reserved data 0x{reserved:X8}.");
            }

            if (index != 0)
            {
                if (uid == previousUid)
                {
                    throw new InvalidDataException($"WDF archive '{Path.GetFileName(normalizedPath)}' contains duplicate entry UID 0x{uid:X8}.");
                }

                if (uid < previousUid)
                {
                    throw new InvalidDataException($"WDF archive '{Path.GetFileName(normalizedPath)}' entry UIDs are not strictly ascending.");
                }
            }

            long payloadEnd = checked((long)offset + length);

            if (offset < HeaderLength || payloadEnd > tableOffset)
            {
                throw new InvalidDataException($"WDF entry {index} (0x{uid:X8}) points outside the archive payload region.");
            }

            entries[index] = new WdfEntry(uid, offset, length);

            previousUid = uid;
        }

        return new WdfArchive(normalizedPath, entries);
    }

    public bool TryOpenRead(string contentPath, [NotNullWhen(true)] out Stream? stream)
    {
        uint uid = WdfPathHash.Compute(contentPath);

        if (!TryFindEntry(uid, out WdfEntry entry))
        {
            stream = null;
            return false;
        }

        FileStream archiveStream = new(_archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, FileOptions.RandomAccess);

        try
        {
            stream = new WdfEntryStream(archiveStream, entry.Offset, entry.Length);
            return true;
        }
        catch
        {
            try
            {
                archiveStream.Dispose();
            }
            catch
            {
                // Preserve the entry-stream creation failure that initiated cleanup.
            }

            throw;
        }
    }

    private bool TryFindEntry(uint uid, out WdfEntry entry)
    {
        int lowerBound = 0;
        int upperBound = _entries.Length - 1;

        while (lowerBound <= upperBound)
        {
            int index = lowerBound + ((upperBound - lowerBound) >> 1);

            WdfEntry candidate = _entries[index];

            if (candidate.Uid == uid)
            {
                entry = candidate;
                return true;
            }

            if (uid < candidate.Uid)
            {
                upperBound = index - 1;
            }
            else
            {
                lowerBound = index + 1;
            }
        }

        entry = default;
        return false;
    }

    private static void ReadExactly(Stream stream, Span<byte> destination, string fieldName)
    {
        try
        {
            stream.ReadExactly(destination);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"The {fieldName} is truncated.", exception);
        }
    }
}
