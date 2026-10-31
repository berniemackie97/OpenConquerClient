using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Map;

/// <summary>Tile indices and ANI source for a retail PUZZLE or PUZZLE2 bitmap.</summary>
public sealed class GameMapPuzzleBitmap
{
    internal const int MaximumEncodedBytes = 128 * 1024;
    private const int HeaderLength = 8 + 0x100 + 8;

    private readonly ushort[] _tileIndices;

    private GameMapPuzzleBitmap(string textureIndexPath, int width, int height,
        ushort[] tileIndices, bool isPuzzle2, int scrollVelocityX, int scrollVelocityY)
    {
        TextureIndexPath = textureIndexPath;
        Width = width;
        Height = height;
        _tileIndices = tileIndices;
        IsPuzzle2 = isPuzzle2;
        ScrollVelocityX = scrollVelocityX;
        ScrollVelocityY = scrollVelocityY;
    }

    public string TextureIndexPath { get; }
    public int Width { get; }
    public int Height { get; }
    public bool IsPuzzle2 { get; }
    public int ScrollVelocityX { get; }
    public int ScrollVelocityY { get; }

    public ushort GetTileIndex(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return _tileIndices[y * Width + x];
    }

    public static GameMapPuzzleBitmap Load(IClientContentSource contentSource, string contentPath)
    {
        byte[] payload = ContentReader.ReadRequiredBytes(contentSource, contentPath,
            ContentLookupMode.LooseOnly, MaximumEncodedBytes);
        return Parse(payload);
    }

    public static GameMapPuzzleBitmap Parse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderLength || payload.Length > MaximumEncodedBytes)
        {
            throw new InvalidDataException("Puzzle bitmap length is invalid.");
        }

        ReadOnlySpan<byte> signature = payload[..8];
        bool isPuzzle2;
        if (signature.SequenceEqual("PUZZLE\0\0"u8))
        {
            isPuzzle2 = false;
        }
        else if (signature.SequenceEqual("PUZZLE2\0"u8))
        {
            isPuzzle2 = true;
        }
        else
        {
            throw new InvalidDataException("Puzzle bitmap signature is invalid.");
        }

        ReadOnlySpan<byte> pathField = payload.Slice(8, 0x100);
        int pathLength = pathField.IndexOf((byte)0);
        if (pathLength <= 0)
        {
            throw new InvalidDataException("Puzzle bitmap ANI path is missing or unterminated.");
        }

        string textureIndexPath;
        try
        {
            textureIndexPath = ClientContentPath.NormalizeVirtualPath(
                Encoding.Latin1.GetString(pathField[..pathLength]), nameof(payload), 0x100);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Puzzle bitmap ANI path is invalid.", exception);
        }

        int width = BinaryPrimitives.ReadInt32LittleEndian(payload[264..]);
        int height = BinaryPrimitives.ReadInt32LittleEndian(payload[268..]);
        long tileCount = (long)width * height;
        int suffixLength = isPuzzle2 ? 8 : 0;

        if (width <= 0 || height <= 0 ||
            tileCount > (payload.Length - HeaderLength - suffixLength) / sizeof(ushort) ||
            HeaderLength + tileCount * sizeof(ushort) + suffixLength != payload.Length)
        {
            throw new InvalidDataException("Puzzle bitmap dimensions do not match the tile data.");
        }

        ushort[] indices = new ushort[(int)tileCount];
        int offset = HeaderLength;
        for (int i = 0; i < indices.Length; i++, offset += sizeof(ushort))
        {
            indices[i] = BinaryPrimitives.ReadUInt16LittleEndian(payload[offset..]);
        }

        int scrollX = 0;
        int scrollY = 0;
        if (isPuzzle2)
        {
            scrollX = BinaryPrimitives.ReadInt32LittleEndian(payload[offset..]);
            scrollY = BinaryPrimitives.ReadInt32LittleEndian(payload[(offset + sizeof(int))..]);
        }

        return new GameMapPuzzleBitmap(textureIndexPath, width, height, indices, isPuzzle2, scrollX, scrollY);
    }
}
