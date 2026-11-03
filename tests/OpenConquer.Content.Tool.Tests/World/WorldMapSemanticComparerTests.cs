using System.Buffers.Binary;
using OpenConquer.Content.Map;
using OpenConquer.Content.Tool.World;

namespace OpenConquer.Content.Tool.Tests.World;

public sealed class WorldMapSemanticComparerTests
{
    [Fact]
    public void CompareAcceptsIndependentCopiesOfTheSameMap()
    {
        using TemporarySourceTree source = CreateMapSource();
        using TemporarySourceTree candidate = CreateMapSource();

        WorldMapSemanticComparer.Compare(source.RootPath, candidate.RootPath, 1010);
    }

    [Fact]
    public void CompareRejectsChangedCatalogCompanion()
    {
        using TemporarySourceTree source = CreateMapSource();
        using TemporarySourceTree candidate = CreateMapSource();
        byte[] catalog = File.ReadAllBytes(candidate.ChildPath("ini/GameMap.dat"));
        int recordCount = BinaryPrimitives.ReadInt32LittleEndian(catalog);
        int offset = sizeof(int);
        bool changed = false;

        for (int index = 0; index < recordCount; index++)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(catalog.AsSpan(offset));
            int pathLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(catalog.AsSpan(offset + 4)));
            if (id == 1010)
            {
                catalog[offset + 8 + pathLength] ^= 1;
                changed = true;
                break;
            }

            offset += 12 + pathLength;
        }

        Assert.True(changed);
        candidate.WriteBytes("ini/GameMap.dat", catalog);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            WorldMapSemanticComparer.Compare(source.RootPath, candidate.RootPath, 1010));
        Assert.Contains("catalog companion value", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompareFailsWhenTheCandidateArchiveCannotBeDecoded()
    {
        using TemporarySourceTree source = CreateMapSource();
        using TemporarySourceTree candidate = CreateMapSource();
        candidate.WriteBytes("map/map/newbie.7z", [1, 2, 3]);

        Assert.Throws<InvalidDataException>(() =>
            WorldMapSemanticComparer.Compare(source.RootPath, candidate.RootPath, 1010));
    }

    [Fact]
    public void CompareCellGridIncludesEverySurfacePassabilityAndElevation()
    {
        GameMapCellGrid original = GameMapCellGrid.Parse(CreateGrid(5, 1, -2));
        GameMapCellGrid equivalent = GameMapCellGrid.Parse(CreateGrid(5, 1, -2));
        WorldMapSemanticComparer.Compare(original, equivalent);

        foreach (GameMapCellGrid changed in new[]
        {
            GameMapCellGrid.Parse(CreateGrid(6, 1, -2)),
            GameMapCellGrid.Parse(CreateGrid(5, 2, -2)),
            GameMapCellGrid.Parse(CreateGrid(5, 1, -3)),
        })
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
                WorldMapSemanticComparer.Compare(original, changed));
            Assert.Contains("cell (0, 0)", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CompareSceneryIncludesUninterpretedFieldsAndOrder()
    {
        byte[] first = CreateScene();
        GameMapSceneSections source = GameMapSceneSections.Parse(first, 0, 1004);
        WorldMapSemanticComparer.Compare(source, GameMapSceneSections.Parse(first, 0, 1004));

        byte[] changed = (byte[])first.Clone();
        changed[12 + 30] = 1;
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            WorldMapSemanticComparer.Compare(source, GameMapSceneSections.Parse(changed, 0, 1004)));
        Assert.Contains("scenery record 0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompareOverlaysIncludesInsertionPositionAndResourceReferences()
    {
        byte[] first = CreateOverlay("map/puzzle/first.pul");
        GameMapSceneSections source = GameMapSceneSections.Parse(first, 0, 1004);
        WorldMapSemanticComparer.Compare(source, GameMapSceneSections.Parse(first, 0, 1004));

        byte[] changedPath = CreateOverlay("map/puzzle/other.pul");
        InvalidDataException pathError = Assert.Throws<InvalidDataException>(() =>
            WorldMapSemanticComparer.Compare(source, GameMapSceneSections.Parse(changedPath, 0, 1004)));
        Assert.Contains("entry 0", pathError.Message, StringComparison.Ordinal);

        byte[] changedIndex = (byte[])first.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(changedIndex.AsSpan(12), 9);
        InvalidDataException indexError = Assert.Throws<InvalidDataException>(() =>
            WorldMapSemanticComparer.Compare(source, GameMapSceneSections.Parse(changedIndex, 0, 1004)));
        Assert.Contains("overlay record 0 header", indexError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompareAnimatedOverlaysIncludesTimingAndRenderFields()
    {
        byte[] first = CreateAnimatedOverlay(100);
        GameMapSceneSections source = GameMapSceneSections.Parse(first, 0, 1004);
        WorldMapSemanticComparer.Compare(source, GameMapSceneSections.Parse(first, 0, 1004));

        byte[] changed = CreateAnimatedOverlay(200);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            WorldMapSemanticComparer.Compare(source, GameMapSceneSections.Parse(changed, 0, 1004)));
        Assert.Contains("overlay record 0 entry 0", error.Message, StringComparison.Ordinal);
    }

    private static byte[] CreateAnimatedOverlay(int interval)
    {
        byte[] bytes = new byte[36 + 0x19C];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 3);
        "ani/mapscene.ani"u8.CopyTo(bytes.AsSpan(36));
        "MapScene"u8.CopyTo(bytes.AsSpan(36 + 0x104));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(36 + 0x194), interval);
        return bytes;
    }

    private static TemporarySourceTree CreateMapSource()
    {
        TemporarySourceTree source = new();
        string fixture = Path.Combine(AppContext.BaseDirectory, "RetailFixture");
        source.WriteBytes("ini/GameMap.dat", OpenConquer.Tests.RetailContentFixture.ReadAllBytes(Path.Combine(fixture, "ini", "GameMap.dat")));
        source.WriteBytes("map/map/newbie.7z", File.ReadAllBytes(Path.Combine(fixture, "map", "newbie.7z")));
        return source;
    }

    private static byte[] CreateGrid(ushort surface, ushort passability, short elevation)
    {
        byte[] bytes = new byte[276 + 6 + sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1004);
        "map/puzzle/newbie.pul"u8.CopyTo(bytes.AsSpan(8));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(268), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(272), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(276), passability);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(278), surface);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(280), elevation);
        uint checksum = unchecked(((uint)surface + 1) * passability +
            ((uint)surface + 1) * (uint)(elevation + 2));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(282), checksum);
        return bytes;
    }

    private static byte[] CreateScene()
    {
        byte[] bytes = new byte[8 + sizeof(int) + 0x48 + sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 10);
        return bytes;
    }

    private static byte[] CreateOverlay(string path)
    {
        byte[] bytes = new byte[36 + 0x104];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 8);
        System.Text.Encoding.Latin1.GetBytes(path).CopyTo(bytes, 36);
        return bytes;
    }
}
