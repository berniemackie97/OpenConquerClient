using System.Buffers.Binary;
using System.IO.Compression;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapSceneSectionsTests
{
    private const int GridEnd = 105348;
    private const int NewbieSceneLength = 13296;

    [Fact]
    public void RetailNewbieScene_ParsesExactSourceSectionsAndRenderOrder()
    {
        byte[] decoded = LoadScene();
        GameMapSceneSections scene = GameMapSceneSections.Parse(decoded, 0);

        Assert.Empty(scene.ExitMarkers);
        Assert.Equal(38, scene.Scenery.Count);
        Assert.Equal(18, scene.Scenery.Count(x => x.TypeCode == 4));
        Assert.Equal(20, scene.Scenery.Count(x => x.TypeCode == 1));
        Assert.Equal("ani/MapScene.ani", scene.Scenery[0].ResourcePath);
        Assert.Equal("newbie17.tga", scene.Scenery[0].PartName);
        Assert.Equal((91, 35), (scene.Scenery[0].TileX, scene.Scenery[0].TileY));
        Assert.Equal("map/Scene/stand06.scene", scene.Scenery[18].ResourcePath);

        GameMapPositionedOverlayRecord overlay = Assert.Single(scene.PositionedOverlays);
        Assert.Equal(0, overlay.InsertIndex);
        Assert.Equal((18, 35), (overlay.HeaderInt0, overlay.HeaderInt1));
        GameMapPuzzleOverlayEntry puzzle = Assert.IsType<GameMapPuzzleOverlayEntry>(Assert.Single(overlay.Entries));
        Assert.Equal("map/puzzle/newbiebg.pul", puzzle.PuzzlePath);

        byte[] completeMap = new byte[GridEnd + decoded.Length];
        decoded.CopyTo(completeMap, GridEnd);
        GameMapSceneSections withGrid = GameMapSceneSections.Parse(completeMap, GridEnd);
        Assert.Equal(scene.Scenery.Count, withGrid.Scenery.Count);
    }

    [Fact]
    public void MalformedCountsAndUnknownSceneryTypes_FailClosed()
    {
        byte[] original = LoadScene();
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse(original.AsSpan(0, 3), 0));

        byte[] excessiveCount = (byte[])original.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(excessiveCount.AsSpan(4), int.MaxValue);
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse(excessiveCount, 0));

        byte[] unknownType = (byte[])original.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(unknownType.AsSpan(8), 0x1234);
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse(unknownType, 0));
    }

    [Fact]
    public void TruncatedTrailingAndUnsafeOverlayResource_FailClosed()
    {
        byte[] original = LoadScene();
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse(original.AsSpan(0, original.Length - 1), 0));
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse([.. original, 0], 0));

        byte[] unsafePath = (byte[])original.Clone();
        int overlayPathOffset = 13036;
        unsafePath.AsSpan(overlayPathOffset, 0x104).Clear();
        "../outside.pul"u8.CopyTo(unsafePath.AsSpan(overlayPathOffset));
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse(unsafePath, 0));
    }

    private static byte[] LoadScene()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "newbie.scene.zlib");
        using FileStream source = File.OpenRead(path);
        using ZLibStream decoded = new(source, CompressionMode.Decompress);
        byte[] bytes = new byte[NewbieSceneLength];
        decoded.ReadExactly(bytes);
        Assert.Equal(-1, decoded.ReadByte());
        return bytes;
    }
}
