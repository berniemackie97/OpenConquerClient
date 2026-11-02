using System.Buffers.Binary;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class MapAssetCodecTests
{
    [Fact]
    public void EncodeAndDecodePreserveTypedMapData()
    {
        MapAsset original = CreateMap();
        byte[] encoded = MapAssetCodec.Encode(original);
        MapAsset decoded = MapAssetCodec.Decode(encoded);

        Assert.Equal("OCMP"u8.ToArray(), encoded.AsSpan(0, 4).ToArray());
        Assert.Equal(1010u, decoded.MapDataId);
        Assert.Equal("world/ground", decoded.GroundPuzzleAssetId);
        Assert.Equal((2, 1), (decoded.Width, decoded.Height));
        Assert.Equal(original.Cells, decoded.Cells);
        Assert.Equal(original.Objects, decoded.Objects);
        Assert.Equal(original.Layers[0].InsertIndex, decoded.Layers[0].InsertIndex);
        Assert.Equal((18, 35), (decoded.Layers[0].ParallaxXPercent, decoded.Layers[0].ParallaxYPercent));
        Assert.Equal(original.Layers[0].Entries, decoded.Layers[0].Entries);
        Assert.Equal(encoded, MapAssetCodec.Encode(decoded));
    }

    [Fact]
    public void EncodeIsIndependentOfTheInputCollectionImplementation()
    {
        MapAsset first = CreateMap();
        MapAsset second = first with
        {
            Cells = first.Cells.ToArray(),
            Objects = first.Objects.ToArray(),
            Layers = first.Layers.Select(layer => layer with
            {
                Entries = layer.Entries.ToArray(),
            }).ToArray(),
        };

        Assert.Equal(MapAssetCodec.Encode(first), MapAssetCodec.Encode(second));
    }

    [Fact]
    public void DecodeRejectsModifiedVersionAndTrailingData()
    {
        byte[] original = MapAssetCodec.Encode(CreateMap());
        byte[] version = (byte[])original.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(version.AsSpan(4), 2);

        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(version));
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode([.. original, 0]));
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(original.AsSpan(0, 29)));
    }

    [Fact]
    public void LoadResolvesCompiledAssetThroughContentSourceAndChecksIdentity()
    {
        using TemporaryContentDirectory root = new();
        byte[] payload = MapAssetCodec.Encode(CreateMap());
        root.WriteFile("maps/1010.ocmap", payload);
        ClientContentRoot content = new(root.RootPath);

        Assert.Equal(1010u, MapAssetCodec.Load(content, 1010).MapDataId);

        byte[] wrongId = (byte[])payload.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(wrongId.AsSpan(8), 999);
        root.WriteFile("maps/1010.ocmap", wrongId);
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Load(content, 1010));
    }

    [Fact]
    public void DecodeRejectsInvalidUtf8()
    {
        byte[] payload = MapAssetCodec.Encode(CreateMap());
        payload[14] = 0xFF;
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(payload));
    }

    [Fact]
    public void DecodeRejectsExcessiveCellCountBeforeAllocating()
    {
        byte[] payload = MapAssetCodec.Encode(CreateMap());
        int gridOffset = 4 + 4 + 4 + 2 + System.Text.Encoding.UTF8.GetByteCount("world/ground");
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(gridOffset), int.MaxValue);

        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(payload));
    }

    [Fact]
    public void EncodeRejectsUnsafeResourcePathsAndInconsistentCells()
    {
        MapAsset map = CreateMap();

        Assert.Throws<ArgumentException>(() => MapAssetCodec.Encode(map with
        {
            GroundPuzzleAssetId = "../outside",
        }));
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Encode(map with
        {
            Width = 4,
        }));
    }

    private static MapAsset CreateMap() => new(
        1010, "world/ground", 2, 1,
        [new GameMapCell(1, 2, 3), new GameMapCell(4, 5, 6)],
        [new MapTerrainObject("world/trees", 10, 20),
            new MapAnimatedObject("world/animations/trees", "Sway", 30, 40)],
        [new MapLayer(0, 18, 35, [new MapPuzzleLayerEntry("world/sky"),
            new MapAnimationLayerEntry("world/animations/sky", "Cloud", 1, 2, 3, 4, 50, 6)])]);
}
