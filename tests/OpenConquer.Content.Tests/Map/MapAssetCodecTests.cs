using System.Buffers.Binary;
using System.Security.Cryptography;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class MapAssetCodecTests
{
    private const int ReferencesDirectoryOffset = 16;
    private const int CellsDirectoryOffset = 68;
    private const int ObjectsDirectoryOffset = 120;

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
        Assert.Equal(original.Layers[0].Entries, decoded.Layers[0].Entries);
        Assert.Equal(encoded, MapAssetCodec.Encode(decoded));
    }

    [Fact]
    public void StreamingWriterProducesTheSameBytesAsMemoryEncoding()
    {
        MapAsset map = CreateMap();
        using MemoryStream stream = new();
        MapAssetCodec.Write(map, stream);

        Assert.Equal(MapAssetCodec.Encode(map), stream.ToArray());
        Assert.Equal(stream.Length, stream.Position);
        Assert.Equal(1010u, MapAssetCodec.Decode(stream).MapDataId);
    }

    [Fact]
    public void EncodeDoesNotDependOnInputCollectionImplementations()
    {
        MapAsset original = CreateMap();
        MapAsset alternate = original with
        {
            Cells = original.Cells.ToArray(),
            Objects = original.Objects.ToArray(),
            Layers = original.Layers.Select(layer => layer with
            {
                Entries = layer.Entries.ToArray(),
            }).ToArray(),
        };

        Assert.Equal(MapAssetCodec.Encode(original), MapAssetCodec.Encode(alternate));
    }

    [Fact]
    public void IndexedReaderCanLoadOnlyTheRequestedGridWindow()
    {
        using MapAssetReader reader = new(new MemoryStream(MapAssetCodec.Encode(CreateMap())));
        Assert.Equal(1010u, reader.MapDataId);
        GameMapCell cell = Assert.Single(reader.ReadCellWindow(1, 0, 1, 1));
        Assert.Equal(new GameMapCell(4, 5, 6), cell);
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadCellWindow(2, 0, 1, 1));
        Assert.Equal(CreateMap().Objects, reader.Read().Objects);
    }

    [Fact]
    public void DecodeRejectsWrongVersionAndTrailingPayload()
    {
        byte[] bytes = MapAssetCodec.Encode(CreateMap());
        byte[] modified = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(modified.AsSpan(4), 2);

        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(modified));
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode([.. bytes, 0]));
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(bytes.AsSpan(0, 30)));
    }

    [Fact]
    public void DecodeRejectsMisorderedOrOverlappingSections()
    {
        byte[] bytes = MapAssetCodec.Encode(CreateMap());
        byte[] wrongType = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(wrongType.AsSpan(CellsDirectoryOffset), 1);
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(wrongType));

        byte[] overlapping = (byte[])bytes.Clone();
        BinaryPrimitives.WriteInt64LittleEndian(overlapping.AsSpan(CellsDirectoryOffset + 4),
            BinaryPrimitives.ReadInt64LittleEndian(overlapping.AsSpan(CellsDirectoryOffset + 4)) - 1);
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(overlapping));
    }

    [Fact]
    public void DecodeRejectsCorruptedSectionDespiteValidIndex()
    {
        byte[] bytes = MapAssetCodec.Encode(CreateMap());
        int offset = SectionOffset(bytes, CellsDirectoryOffset);
        bytes[offset + 12] ^= 1;
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(bytes));
    }

    [Fact]
    public void DecodeRejectsNoncanonicalReferenceEvenWithMatchingDigest()
    {
        byte[] bytes = MapAssetCodec.Encode(CreateMap());
        int offset = SectionOffset(bytes, ReferencesDirectoryOffset);
        bytes[offset + 6] = (byte)'W';
        Rehash(bytes, ReferencesDirectoryOffset);
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(bytes));
    }

    [Fact]
    public void DecodeRejectsInvalidUtf8EvenWithMatchingDigest()
    {
        byte[] bytes = MapAssetCodec.Encode(CreateMap());
        int offset = SectionOffset(bytes, ReferencesDirectoryOffset);
        bytes[offset + 6] = 0xFF;
        Rehash(bytes, ReferencesDirectoryOffset);
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(bytes));
    }

    [Fact]
    public void DecodeRejectsExcessiveDimensionsAndUnknownReferences()
    {
        byte[] size = MapAssetCodec.Encode(CreateMap());
        int offset = SectionOffset(size, CellsDirectoryOffset);
        BinaryPrimitives.WriteInt32LittleEndian(size.AsSpan(offset + 4), int.MaxValue);
        Rehash(size, CellsDirectoryOffset);
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(size));

        byte[] reference = MapAssetCodec.Encode(CreateMap());
        offset = SectionOffset(reference, CellsDirectoryOffset);
        BinaryPrimitives.WriteInt32LittleEndian(reference.AsSpan(offset), int.MaxValue);
        Rehash(reference, CellsDirectoryOffset);
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(reference));
    }

    [Fact]
    public void DecodeRejectsUnknownObjectTypes()
    {
        byte[] bytes = MapAssetCodec.Encode(CreateMap());
        int offset = SectionOffset(bytes, ObjectsDirectoryOffset);
        bytes[offset + 4] = 255;
        Rehash(bytes, ObjectsDirectoryOffset);
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Decode(bytes));
    }

    [Fact]
    public void LoadChecksCompiledAssetIdentity()
    {
        using TemporaryContentDirectory root = new();
        byte[] bytes = MapAssetCodec.Encode(CreateMap());
        root.WriteFile("maps/1010.ocmap", bytes);
        ClientContentRoot content = new(root.RootPath);
        Assert.Equal(1010u, MapAssetCodec.Load(content, 1010).MapDataId);

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 999);
        root.WriteFile("maps/1010.ocmap", bytes);
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Load(content, 1010));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("world/Upper")]
    [InlineData("world//ground")]
    [InlineData("world/trailing.")]
    [InlineData("world/\u00e9")]
    public void EncodeRejectsNoncanonicalAssetIdentifiers(string id)
    {
        Assert.Throws<ArgumentException>(() => MapAssetCodec.Encode(CreateMap() with
        {
            GroundPuzzleAssetId = id,
        }));
    }

    [Fact]
    public void EncodeRejectsInconsistentGridAndPrepopulatedOutput()
    {
        Assert.Throws<InvalidDataException>(() => MapAssetCodec.Encode(CreateMap() with { Width = 4 }));

        using MemoryStream output = new();
        output.WriteByte(1);
        Assert.Throws<ArgumentException>(() => MapAssetCodec.Write(CreateMap(), output));
    }

    private static void Rehash(byte[] bytes, int descriptor)
    {
        int start = SectionOffset(bytes, descriptor);
        int length = checked((int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(descriptor + 12)));
        byte[] digest = SHA256.HashData(bytes.AsSpan(start, length));
        digest.CopyTo(bytes.AsSpan(descriptor + 20, 32));
    }

    private static int SectionOffset(byte[] bytes, int descriptor) =>
        checked((int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(descriptor + 4)));

    private static MapAsset CreateMap() => new(
        1010, "world/ground", 2, 1,
        [new GameMapCell(1, 2, 3), new GameMapCell(4, 5, 6)],
        [new MapTerrainObject("world/trees", 10, 20),
            new MapAnimatedObject("world/animations/trees", "Sway", 30, 40)],
        [new MapLayer(0, 18, 35, [new MapPuzzleLayerEntry("world/sky"),
            new MapAnimationLayerEntry("world/animations/sky", "Cloud", 1, 2, 3, 4, 50, 6)])]);
}
