using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Map;

public readonly record struct GameMapTerrainCell(ushort PassabilityFlag, ushort SurfaceId, short ElevationDelta);

public sealed record GameMapTerrainObject(
    string AnimationPath, string SectionName,
    int RenderOffsetX, int RenderOffsetY, int FrameIntervalMilliseconds,
    int GridWidth, int GridHeight, uint AuthoredColor,
    int AnchorOffsetX, int AnchorOffsetY, int DrawParameter,
    IReadOnlyList<GameMapTerrainCell> Cells);

/// <summary>Native 5517 terrain-object records in a DMap type-1 .scene resource.</summary>
public sealed class GameMapTerrainSceneFile
{
    internal const int MaximumFileBytes = 2 * 1024 * 1024;
    private const int FixedObjectBytes = 0x164;
    private readonly IReadOnlyList<GameMapTerrainObject> _objects;

    private GameMapTerrainSceneFile(GameMapTerrainObject[] objects)
    {
        _objects = Array.AsReadOnly(objects);
    }

    public IReadOnlyList<GameMapTerrainObject> Objects => _objects;

    public static GameMapTerrainSceneFile Parse(ReadOnlySpan<byte> source)
    {
        if (source.Length < sizeof(int) || source.Length > MaximumFileBytes)
        {
            throw new InvalidDataException("Terrain scene file length is invalid.");
        }

        int offset = 0;
        int objectCount = ReadInt32(source, ref offset);
        if (objectCount < 0 || objectCount > (source.Length - offset) / FixedObjectBytes)
        {
            throw new InvalidDataException("Terrain scene object count exceeds the file.");
        }

        GameMapTerrainObject[] objects = new GameMapTerrainObject[objectCount];
        for (int i = 0; i < objects.Length; i++)
        {
            string animationPath = ReadString(source, ref offset, 0x100, isPath: true);
            string sectionName = ReadString(source, ref offset, 0x40, isPath: false);
            int renderX = ReadInt32(source, ref offset);
            int renderY = ReadInt32(source, ref offset);
            int frameInterval = ReadInt32(source, ref offset);
            int gridWidth = ReadInt32(source, ref offset);
            int gridHeight = ReadInt32(source, ref offset);
            uint authoredColor = unchecked((uint)ReadInt32(source, ref offset));
            int anchorX = ReadInt32(source, ref offset);
            int anchorY = ReadInt32(source, ref offset);
            int drawParameter = ReadInt32(source, ref offset);

            long cellCount = (long)gridWidth * gridHeight;
            if (gridWidth < 0 || gridHeight < 0 ||
                cellCount > (source.Length - offset) / (3 * sizeof(int)))
            {
                throw new InvalidDataException($"Terrain scene object {i} has an invalid cell grid.");
            }

            GameMapTerrainCell[] cells = new GameMapTerrainCell[(int)cellCount];
            for (int cell = 0; cell < cells.Length; cell++)
            {
                ushort passability = unchecked((ushort)ReadInt32(source, ref offset));
                ushort surface = unchecked((ushort)ReadInt32(source, ref offset));
                short elevation = unchecked((short)ReadInt32(source, ref offset));
                cells[cell] = new GameMapTerrainCell(passability, surface, elevation);
            }

            objects[i] = new GameMapTerrainObject(
                animationPath, sectionName, renderX, renderY, frameInterval,
                gridWidth, gridHeight, authoredColor, anchorX, anchorY, drawParameter,
                Array.AsReadOnly(cells));
        }

        if (offset != source.Length)
        {
            throw new InvalidDataException("Terrain scene has trailing bytes.");
        }

        return new GameMapTerrainSceneFile(objects);
    }

    private static int ReadInt32(ReadOnlySpan<byte> source, ref int offset)
    {
        if (source.Length - offset < sizeof(int))
        {
            throw new InvalidDataException("Terrain scene record is truncated.");
        }

        int value = BinaryPrimitives.ReadInt32LittleEndian(source[offset..]);
        offset += sizeof(int);
        return value;
    }

    private static string ReadString(ReadOnlySpan<byte> source, ref int offset, int length, bool isPath)
    {
        if (source.Length - offset < length)
        {
            throw new InvalidDataException("Terrain scene resource field is truncated.");
        }

        ReadOnlySpan<byte> encoded = source.Slice(offset, length);
        offset += length;
        int end = encoded.IndexOf((byte)0);
        if (end <= 0)
        {
            throw new InvalidDataException("Terrain scene resource name is empty or unterminated.");
        }

        string value = Encoding.Latin1.GetString(encoded[..end]);
        if (!isPath)
        {
            return value;
        }

        try
        {
            return ClientContentPath.NormalizeVirtualPath(value, nameof(source), length);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Terrain scene ANI resource path is invalid.", exception);
        }
    }
}
