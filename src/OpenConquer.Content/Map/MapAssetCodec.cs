using System.Text;

namespace OpenConquer.Content.Map;

public static class MapAssetCodec
{
    private static readonly UTF8Encoding s_utf8 = new(false, true);
    private const int Version = 1;
    private const int MaximumBytes = 32 * 1024 * 1024;
    private const int MaximumCells = 4 * 1024 * 1024;
    private const int MaximumObjects = 100_000;
    private const int MaximumLayers = 4_096;
    private const int MaximumEntries = 100_000;

    public static byte[] Encode(MapAsset map)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, s_utf8, leaveOpen: true))
        {
            writer.Write("OCMP"u8);
            writer.Write(Version);
            writer.Write(map.MapDataId);
            WriteGrid(writer, map);
            WriteObjects(writer, map.Objects);
            WriteLayers(writer, map.Layers);
        }

        if (stream.Length > MaximumBytes)
        {
            throw new InvalidDataException("Compiled map exceeds its size limit.");
        }

        return stream.ToArray();
    }

    public static MapAsset Load(IClientContentSource content, uint mapDataId)
    {
        ArgumentNullException.ThrowIfNull(content);
        string path = $"maps/{mapDataId}.ocmap";
        byte[] payload = ContentReader.ReadRequiredBytes(content, path, ContentLookupMode.LooseOnly, MaximumBytes);
        MapAsset map = Decode(payload);
        if (map.MapDataId != mapDataId)
        {
            throw new InvalidDataException($"Map asset '{path}' declares a different map data ID.");
        }

        return map;
    }

    public static MapAsset Decode(ReadOnlySpan<byte> source)
    {
        try
        {
            return DecodeCore(source);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Compiled map ends before its declared data.", exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Compiled map contains invalid UTF-8.", exception);
        }
    }

    private static MapAsset DecodeCore(ReadOnlySpan<byte> source)
    {
        if (source.Length < 28 || source.Length > MaximumBytes)
        {
            throw new InvalidDataException("Compiled map size is invalid.");
        }

        using MemoryStream stream = new(source.ToArray(), writable: false);
        using BinaryReader reader = new(stream, s_utf8);

        if (!reader.ReadBytes(4).AsSpan().SequenceEqual("OCMP"u8) || reader.ReadInt32() != Version)
        {
            throw new InvalidDataException("Compiled map header or version is unsupported.");
        }

        uint id = reader.ReadUInt32();
        string puzzle = ReadResourceId(reader);
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        int count = CountCells(width, height);
        GameMapCell[] cells = new GameMapCell[count];
        RequireRemaining(reader, checked((long)count * 6));
        for (int index = 0; index < count; index++)
        {
            cells[index] = new GameMapCell(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadInt16());
        }

        MapObject[] objects = ReadObjects(reader);
        MapLayer[] layers = ReadLayers(reader);

        if (reader.BaseStream.Position != reader.BaseStream.Length)
        {
            throw new InvalidDataException("Compiled map has trailing bytes.");
        }

        return new MapAsset(id, puzzle, width, height, Array.AsReadOnly(cells),
            Array.AsReadOnly(objects), Array.AsReadOnly(layers));
    }

    private static void WriteGrid(BinaryWriter writer, MapAsset map)
    {
        WriteResourceId(writer, map.GroundPuzzleAssetId);
        int count = CountCells(map.Width, map.Height);
        if (count != map.Cells.Count)
        {
            throw new InvalidDataException("Map dimensions do not match its cell count.");
        }

        writer.Write(map.Width);
        writer.Write(map.Height);
        foreach (GameMapCell cell in map.Cells)
        {
            writer.Write(cell.SurfaceId);
            writer.Write(cell.PassabilityFlag);
            writer.Write(cell.Elevation);
        }
    }

    private static void WriteObjects(BinaryWriter writer, IReadOnlyList<MapObject> objects)
    {
        CheckCount(objects.Count, MaximumObjects, "object");
        writer.Write(objects.Count);
        foreach (MapObject item in objects)
        {
            switch (item)
            {
                case MapTerrainObject terrain:
                    writer.Write((byte)1);
                    WriteResourceId(writer, terrain.SceneAssetId);
                    writer.Write(terrain.TileX);
                    writer.Write(terrain.TileY);
                    break;
                case MapAnimatedObject animation:
                    writer.Write((byte)2);
                    WriteResourceId(writer, animation.AnimationAssetId);
                    WriteName(writer, animation.SectionName);
                    writer.Write(animation.TileX);
                    writer.Write(animation.TileY);
                    break;
                default:
                    throw new InvalidDataException("Map contains an unsupported object.");
            }
        }
    }

    private static MapObject[] ReadObjects(BinaryReader reader)
    {
        int count = ReadCount(reader, MaximumObjects, 1, "object");
        MapObject[] objects = new MapObject[count];
        for (int index = 0; index < objects.Length; index++)
        {
            objects[index] = reader.ReadByte() switch
            {
                1 => new MapTerrainObject(ReadResourceId(reader), reader.ReadInt32(), reader.ReadInt32()),
                2 => new MapAnimatedObject(ReadResourceId(reader), ReadName(reader), reader.ReadInt32(), reader.ReadInt32()),
                _ => throw new InvalidDataException("Compiled map contains an unsupported object type."),
            };
        }

        return objects;
    }

    private static void WriteLayers(BinaryWriter writer, IReadOnlyList<MapLayer> layers)
    {
        CheckCount(layers.Count, MaximumLayers, "layer");
        writer.Write(layers.Count);
        foreach (MapLayer layer in layers)
        {
            writer.Write(layer.InsertIndex);
            writer.Write(layer.ParallaxXPercent);
            writer.Write(layer.ParallaxYPercent);
            CheckCount(layer.Entries.Count, MaximumEntries, "layer entry");
            writer.Write(layer.Entries.Count);

            foreach (MapLayerEntry item in layer.Entries)
            {
                switch (item)
                {
                    case MapPuzzleLayerEntry puzzle:
                        writer.Write((byte)1);
                        WriteResourceId(writer, puzzle.PuzzleAssetId);
                        break;
                    case MapAnimationLayerEntry animation:
                        writer.Write((byte)2);
                        WriteResourceId(writer, animation.AnimationAssetId);
                        WriteName(writer, animation.SectionName);
                        writer.Write(animation.TileX);
                        writer.Write(animation.TileY);
                        writer.Write(animation.OffsetX);
                        writer.Write(animation.OffsetY);
                        writer.Write(animation.FrameIntervalMilliseconds);
                        writer.Write(animation.DrawParameter);
                        break;
                    default:
                        throw new InvalidDataException("Map contains an unsupported layer entry.");
                }
            }
        }
    }

    private static MapLayer[] ReadLayers(BinaryReader reader)
    {
        int count = ReadCount(reader, MaximumLayers, 16, "layer");
        MapLayer[] layers = new MapLayer[count];
        for (int index = 0; index < layers.Length; index++)
        {
            int insertion = reader.ReadInt32();
            int parallaxX = reader.ReadInt32();
            int parallaxY = reader.ReadInt32();
            int entries = ReadCount(reader, MaximumEntries, 1, "layer entry");
            MapLayerEntry[] items = new MapLayerEntry[entries];
            for (int child = 0; child < entries; child++)
            {
                items[child] = reader.ReadByte() switch
                {
                    1 => new MapPuzzleLayerEntry(ReadResourceId(reader)),
                    2 => new MapAnimationLayerEntry(ReadResourceId(reader), ReadName(reader),
                        reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                        reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
                    _ => throw new InvalidDataException("Compiled map contains an unsupported layer entry."),
                };
            }

            layers[index] = new MapLayer(insertion, parallaxX, parallaxY, Array.AsReadOnly(items));
        }

        return layers;
    }

    private static void WriteResourceId(BinaryWriter writer, string value)
    {
        ClientContentPath.NormalizeVirtualPath(value, nameof(value), 260);
        WriteString(writer, value);
    }

    private static string ReadResourceId(BinaryReader reader)
    {
        string value = ReadString(reader);
        try
        {
            return ClientContentPath.NormalizeVirtualPath(value, nameof(value), 260);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Compiled map contains an invalid asset path.", exception);
        }
    }

    private static void WriteName(BinaryWriter writer, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("Map contains an empty animation section name.");
        }

        WriteString(writer, value);
    }

    private static string ReadName(BinaryReader reader)
    {
        string name = ReadString(reader);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidDataException("Compiled map contains an empty animation section name.");
        }

        return name;
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] encoded = s_utf8.GetBytes(value);
        if (encoded.Length is 0 or > 1024)
        {
            throw new InvalidDataException("Compiled map string length is invalid.");
        }

        writer.Write((ushort)encoded.Length);
        writer.Write(encoded);
    }

    private static string ReadString(BinaryReader reader)
    {
        ushort length = reader.ReadUInt16();
        if (length is 0 or > 1024)
        {
            throw new InvalidDataException("Compiled map string length is invalid.");
        }

        RequireRemaining(reader, length);
        return s_utf8.GetString(reader.ReadBytes(length));
    }

    private static int CountCells(int width, int height)
    {
        long count = (long)width * height;
        if (width <= 0 || height <= 0 || count > MaximumCells)
        {
            throw new InvalidDataException("Map cell dimensions exceed their limit.");
        }

        return (int)count;
    }

    private static int ReadCount(BinaryReader reader, int maximum, int minimumBytesPerItem, string label)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > maximum ||
            count > (reader.BaseStream.Length - reader.BaseStream.Position) / minimumBytesPerItem)
        {
            throw new InvalidDataException($"Compiled map {label} count is invalid.");
        }

        return count;
    }

    private static void CheckCount(int count, int maximum, string label)
    {
        if (count > maximum)
        {
            throw new InvalidDataException($"Map {label} count exceeds its limit.");
        }
    }

    private static void RequireRemaining(BinaryReader reader, long bytes)
    {
        if (bytes > reader.BaseStream.Length - reader.BaseStream.Position)
        {
            throw new InvalidDataException("Compiled map is truncated.");
        }
    }
}
