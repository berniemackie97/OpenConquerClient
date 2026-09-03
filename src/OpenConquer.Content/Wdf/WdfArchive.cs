using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Content.Wdf;

internal sealed class WdfArchive
{
    public const uint Magic = 0x57444650;
    public const int HeaderLength = 12;
    public const int EntryLength = 16;

    private readonly string _archivePath;
    private readonly Dictionary<uint, WdfEntry> _entries;

    private WdfArchive(string archivePath, Dictionary<uint, WdfEntry> entries)
    {
        _archivePath = archivePath;
        _entries = entries;
    }

    public int EntryCount => _entries.Count;

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
        uint tableOffset = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
        long tableLength = checked((long)declaredCount * EntryLength);
        long tableEnd = checked((long)tableOffset + tableLength);

        if (tableOffset < HeaderLength || tableEnd > stream.Length)
        {
            throw new InvalidDataException($"WDF archive '{Path.GetFileName(normalizedPath)}' has an out-of-range entry table.");
        }

        if (declaredCount > int.MaxValue)
        {
            throw new InvalidDataException($"WDF archive '{Path.GetFileName(normalizedPath)}' declares too many entries.");
        }

        stream.Position = tableOffset;
        Dictionary<uint, WdfEntry> entries = new(checked((int)declaredCount));
        Span<byte> encodedEntry = stackalloc byte[EntryLength];

        for (int index = 0; index < declaredCount; index++)
        {
            ReadExactly(stream, encodedEntry, $"WDF entry {index}");

            uint id = BinaryPrimitives.ReadUInt32LittleEndian(encodedEntry);
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(encodedEntry[4..]);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(encodedEntry[8..]);
            long end = checked((long)offset + length);

            if (end > stream.Length)
            {
                throw new InvalidDataException($"WDF entry {index} (0x{id:X8}) extends beyond the archive.");
            }

            if (!entries.TryAdd(id, new WdfEntry(id, offset, length)))
            {
                throw new InvalidDataException($"WDF archive '{Path.GetFileName(normalizedPath)}' contains duplicate entry id 0x{id:X8}.");
            }
        }

        return new WdfArchive(normalizedPath, entries);
    }

    public bool TryOpenRead(string contentPath, [NotNullWhen(true)] out Stream? stream)
    {
        uint id = WdfPathHash.Compute(contentPath);

        if (!_entries.TryGetValue(id, out WdfEntry entry))
        {
            stream = null;
            return false;
        }

        FileStream archiveStream = new(_archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, FileOptions.RandomAccess);

        try
        {
            archiveStream.Position = entry.Offset;
            stream = new WdfEntryStream(archiveStream, entry.Length);
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
                // Preserve the stream-opening failure that initiated cleanup.
            }

            throw;
        }
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

    private sealed class WdfEntryStream(FileStream archiveStream, uint length) : Stream
    {
        private readonly long _length = length;
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            int requestedLength = (int)Math.Min(buffer.Length, _length - _position);

            if (requestedLength <= 0)
            {
                return 0;
            }

            int bytesRead = archiveStream.Read(buffer[..requestedLength]);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException("The WDF archive ended before the selected entry was fully read.");
            }

            _position += bytesRead;
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(_position + offset),
                SeekOrigin.End => checked(_length + offset),

                _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "Unknown seek origin."),
            };

            if (position < 0 || position > _length)
            {
                throw new IOException("Cannot seek outside a WDF entry.");
            }

            archiveStream.Seek(position - _position, SeekOrigin.Current);

            _position = position;
            return position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                archiveStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
