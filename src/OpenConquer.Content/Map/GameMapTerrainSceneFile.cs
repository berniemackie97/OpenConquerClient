using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Map;

/// <summary>One footprint cell of a terrain object, applied to the map cell it covers.</summary>
/// <param name="PassabilityFlag">Non-zero blocks movement.</param>
/// <param name="SurfaceId">Surface type used for footsteps and effects.</param>
/// <param name="ElevationDelta">Height added to the elevation already attached to the map cell.</param>
public readonly record struct GameMapTerrainCell(ushort PassabilityFlag, ushort SurfaceId, short ElevationDelta);

/// <summary>A terrain object authored in a <c>.scene</c> file: an animated sprite with a cell footprint.</summary>
public sealed class GameMapTerrainObject
{
    internal GameMapTerrainObject(string animationPath, string sectionName, int renderOffsetX, int renderOffsetY,
        int frameIntervalMilliseconds, int gridWidth, int gridHeight, uint authoredColor, int anchorOffsetX,
        int anchorOffsetY, int drawParameter, IReadOnlyList<GameMapTerrainCell> cells)
    {
        AnimationPath = animationPath;
        SectionName = sectionName;
        RenderOffsetX = renderOffsetX;
        RenderOffsetY = renderOffsetY;
        FrameIntervalMilliseconds = frameIntervalMilliseconds;
        GridWidth = gridWidth;
        GridHeight = gridHeight;
        AuthoredColor = authoredColor;
        AnchorOffsetX = anchorOffsetX;
        AnchorOffsetY = anchorOffsetY;
        DrawParameter = drawParameter;
        Cells = cells;
    }

    /// <summary>Normalized content path of the animation index.</summary>
    public string AnimationPath
    {
        get;
    }

    /// <summary>Section of <see cref="AnimationPath"/> holding the sprite frames.</summary>
    public string SectionName
    {
        get;
    }

    /// <summary>Screen-pixel offset added to the projected anchor cell before drawing.</summary>
    public int RenderOffsetX
    {
        get;
    }

    /// <inheritdoc cref="RenderOffsetX"/>
    public int RenderOffsetY
    {
        get;
    }

    /// <summary>Milliseconds per animation frame as authored; a value below 1 is drawn as 1.</summary>
    public int FrameIntervalMilliseconds
    {
        get;
    }

    /// <summary>Footprint width in map cells.</summary>
    public int GridWidth
    {
        get;
    }

    /// <summary>Footprint height in map cells.</summary>
    public int GridHeight
    {
        get;
    }

    /// <summary>Colour stored with the object. Drawing tints with the map world colour instead.</summary>
    public uint AuthoredColor
    {
        get;
    }

    /// <summary>Cell offset from the placement's anchor cell to this object's anchor cell.</summary>
    public int AnchorOffsetX
    {
        get;
    }

    /// <inheritdoc cref="AnchorOffsetX"/>
    public int AnchorOffsetY
    {
        get;
    }

    /// <summary>Draw parameter stored with the object; terrain objects draw with the default blend.</summary>
    public int DrawParameter
    {
        get;
    }

    /// <summary>Footprint cells, row by row (<see cref="GridWidth"/> per row).</summary>
    public IReadOnlyList<GameMapTerrainCell> Cells
    {
        get;
    }
}

/// <summary>Parses a <c>.scene</c> file: the terrain objects a type-1 map scenery placement draws.</summary>
public sealed class GameMapTerrainSceneFile
{
    /// <summary>Upper bound on a scene file; the largest shipped file is about 11 KiB.</summary>
    internal const int MaximumFileBytes = 1024 * 1024;

    private const int AnimationPathBytes = 0x100;
    private const int SectionNameBytes = 0x40;
    private const int ObjectFieldCount = 9;
    private const int ObjectHeaderBytes = AnimationPathBytes + SectionNameBytes + ObjectFieldCount * sizeof(int);
    private const int CellBytes = 3 * sizeof(int);

    private GameMapTerrainSceneFile(GameMapTerrainObject[] objects)
    {
        Objects = Array.AsReadOnly(objects);
    }

    public IReadOnlyList<GameMapTerrainObject> Objects
    {
        get;
    }

    /// <exception cref="InvalidDataException">The data is truncated, has trailing bytes, or holds an invalid field.</exception>
    public static GameMapTerrainSceneFile Parse(ReadOnlySpan<byte> source)
    {
        if (source.Length < sizeof(int) || source.Length > MaximumFileBytes)
        {
            throw new InvalidDataException("Terrain scene file length is invalid.");
        }

        SceneReader reader = new(source);
        int objectCount = reader.ReadInt32();
        if (objectCount < 0 || objectCount > reader.Remaining / ObjectHeaderBytes)
        {
            throw new InvalidDataException("Terrain scene object count exceeds the file.");
        }

        GameMapTerrainObject[] objects = new GameMapTerrainObject[objectCount];
        for (int index = 0; index < objects.Length; index++)
        {
            objects[index] = ReadObject(ref reader, index);
        }

        if (reader.Remaining != 0)
        {
            throw new InvalidDataException("Terrain scene file has trailing bytes.");
        }

        return new GameMapTerrainSceneFile(objects);
    }

    private static GameMapTerrainObject ReadObject(ref SceneReader reader, int index)
    {
        string animationPath = reader.ReadContentPath(AnimationPathBytes, index);
        string sectionName = reader.ReadName(SectionNameBytes, index);
        int renderOffsetX = reader.ReadInt32();
        int renderOffsetY = reader.ReadInt32();
        int frameInterval = reader.ReadInt32();
        int gridWidth = reader.ReadInt32();
        int gridHeight = reader.ReadInt32();
        uint authoredColor = unchecked((uint)reader.ReadInt32());
        int anchorOffsetX = reader.ReadInt32();
        int anchorOffsetY = reader.ReadInt32();
        int drawParameter = reader.ReadInt32();

        long cellCount = (long)gridWidth * gridHeight;
        if (gridWidth < 0 || gridHeight < 0 || cellCount > reader.Remaining / CellBytes)
        {
            throw new InvalidDataException($"Terrain scene object {index} has an invalid cell grid.");
        }

        GameMapTerrainCell[] cells = new GameMapTerrainCell[(int)cellCount];
        for (int cell = 0; cell < cells.Length; cell++)
        {
            // Each value is stored as a 32-bit field of which only the low 16 bits are significant.
            ushort passability = unchecked((ushort)reader.ReadInt32());
            ushort surface = unchecked((ushort)reader.ReadInt32());
            short elevation = unchecked((short)reader.ReadInt32());
            cells[cell] = new GameMapTerrainCell(passability, surface, elevation);
        }

        return new GameMapTerrainObject(animationPath, sectionName, renderOffsetX, renderOffsetY, frameInterval,
            gridWidth, gridHeight, authoredColor, anchorOffsetX, anchorOffsetY, drawParameter, Array.AsReadOnly(cells));
    }

    private ref struct SceneReader(ReadOnlySpan<byte> source)
    {
        private readonly ReadOnlySpan<byte> _source = source;
        private int _offset;

        public readonly int Remaining => _source.Length - _offset;

        public int ReadInt32()
        {
            if (Remaining < sizeof(int))
            {
                throw new InvalidDataException("Terrain scene record is truncated.");
            }

            int value = BinaryPrimitives.ReadInt32LittleEndian(_source[_offset..]);
            _offset += sizeof(int);
            return value;
        }

        public string ReadName(int fieldBytes, int objectIndex)
        {
            if (Remaining < fieldBytes)
            {
                throw new InvalidDataException("Terrain scene record is truncated.");
            }

            ReadOnlySpan<byte> field = _source.Slice(_offset, fieldBytes);
            _offset += fieldBytes;

            int length = field.IndexOf((byte)0);
            if (length <= 0)
            {
                throw new InvalidDataException($"Terrain scene object {objectIndex} has an empty or unterminated name.");
            }

            return Encoding.Latin1.GetString(field[..length]);
        }

        public string ReadContentPath(int fieldBytes, int objectIndex)
        {
            string path = ReadName(fieldBytes, objectIndex);
            try
            {
                return ClientContentPath.NormalizeVirtualPath(path, nameof(path), fieldBytes);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"Terrain scene object {objectIndex} has an invalid animation path.", exception);
            }
        }
    }
}
