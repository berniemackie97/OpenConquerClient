using System.IO.Compression;
using System.Text;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapPuzzleResourcesTests
{
    [Fact]
    public void RetailNewbieMap_LoadsGroundAndPositionedBackdropFromOriginalPuzzleFiles()
    {
        using TemporaryContentDirectory root = CreateNewbieContent();
        root.WriteFile("map/puzzle/newbie.pul", ReadPuzzle("newbie.pul"));
        root.WriteFile("map/puzzle/newbiebg.pul", ReadPuzzle("newbiebg.pul"));
        root.WriteFile("ani/newbie.ani", ReadRetailAni());

        ClientContentRoot source = new(root.RootPath);
        GameMapContent map = GameMapContent.Load(source, GameMapCatalog.Load(source), 1010);
        GameMapPuzzleResources resources = GameMapPuzzleResources.Load(source, map);

        Assert.Equal((15, 9), (resources.Ground.Columns, resources.Ground.Rows));
        GameMapPuzzleOverlayEntry backdropEntry =
            Assert.IsType<GameMapPuzzleOverlayEntry>(Assert.Single(Assert.Single(map.Scene.PositionedOverlays).Entries));
        GameMapPuzzleBitmap backdrop = resources.GetRequired(backdropEntry.PuzzlePath);
        Assert.Equal((6, 6), (backdrop.Columns, backdrop.Rows));
        Assert.Equal("ani/newbie.ani", backdrop.TextureIndexPath);
        Assert.Same(resources.Ground, resources.GetRequired("MAP/PUZZLE/NEWBIE.PUL"));
        Assert.Throws<KeyNotFoundException>(() => resources.GetRequired("map/puzzle/missing.pul"));

        Assert.Equal("data/map/puzzle/newbie/newbie004.dds",
            Assert.Single(resources.GetRequiredTileAnimation("map/puzzle/newbie.pul", 1).FramePaths));
        Assert.Equal("data/map/puzzle/sky/stand/bird09.dds",
            Assert.Single(resources.GetRequiredTileAnimation("map/puzzle/newbie.pul", 94).FramePaths));
        Assert.Equal("data/map/puzzle/newbie/bg000.dds",
            Assert.Single(resources.GetRequiredTileAnimation(backdropEntry.PuzzlePath, 50).FramePaths));
        Assert.Equal("data/map/puzzle/newbie/bg035.dds",
            Assert.Single(resources.GetRequiredTileAnimation(backdropEntry.PuzzlePath, 85).FramePaths));
        Assert.Throws<InvalidDataException>(() => resources.GetRequiredTileAnimation(backdropEntry.PuzzlePath, ushort.MaxValue));
        Assert.Throws<InvalidDataException>(() => resources.GetRequiredTileAnimation(backdropEntry.PuzzlePath, 1));
        Assert.Throws<KeyNotFoundException>(() => resources.GetRequiredTileAnimation("map/puzzle/missing.pul", 50));
    }

    [Fact]
    public void MissingPuzzleResource_FailsWithoutFallback()
    {
        using TemporaryContentDirectory root = CreateNewbieContent();
        ClientContentRoot source = new(root.RootPath);
        GameMapContent map = GameMapContent.Load(source, GameMapCatalog.Load(source), 1010);

        Assert.Throws<FileNotFoundException>(() => GameMapPuzzleResources.Load(source, map));

        root.WriteFile("map/puzzle/newbie.pul", ReadPuzzle("newbie.pul"));
        Assert.Throws<FileNotFoundException>(() => GameMapPuzzleResources.Load(source, map));
    }

    [Fact]
    public void MissingAniIndex_FailsBeforeReturningUnresolvedPuzzleTiles()
    {
        using TemporaryContentDirectory root = CreateNewbieContent();
        root.WriteFile("map/puzzle/newbie.pul", ReadPuzzle("newbie.pul"));
        root.WriteFile("map/puzzle/newbiebg.pul", ReadPuzzle("newbiebg.pul"));

        ClientContentRoot source = new(root.RootPath);
        GameMapContent map = GameMapContent.Load(source, GameMapCatalog.Load(source), 1010);

        Assert.Throws<FileNotFoundException>(() => GameMapPuzzleResources.Load(source, map));
    }

    [Fact]
    public void MissingReferencedAniSection_FailsRatherThanDroppingMapTiles()
    {
        using TemporaryContentDirectory root = CreateNewbieContent();
        root.WriteFile("map/puzzle/newbie.pul", ReadPuzzle("newbie.pul"));
        root.WriteFile("map/puzzle/newbiebg.pul", ReadPuzzle("newbiebg.pul"));

        string retail = Encoding.Latin1.GetString(ReadRetailAni());
        Assert.Contains("[Puzzle94]", retail, StringComparison.Ordinal);
        root.WriteFile("ani/newbie.ani",
            Encoding.Latin1.GetBytes(retail.Replace("[Puzzle94]", "[PuzzleA4]", StringComparison.Ordinal)));

        ClientContentRoot source = new(root.RootPath);
        GameMapContent map = GameMapContent.Load(source, GameMapCatalog.Load(source), 1010);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => GameMapPuzzleResources.Load(source, map));
        Assert.Contains("Puzzle94", exception.Message, StringComparison.Ordinal);
    }

    private static TemporaryContentDirectory CreateNewbieContent()
    {
        TemporaryContentDirectory root = new();
        root.WriteFile("ini/GameMap.dat",
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "ini", "GameMap.dat")));
        root.WriteFile("map/map/newbie.7z",
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "newbie.7z")));
        return root;
    }

    private static byte[] ReadRetailAni()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "RetailFixture", "ani", "newbie.ani.zlib");
        using FileStream source = File.OpenRead(path);
        using ZLibStream decoded = new(source, CompressionMode.Decompress);
        using MemoryStream destination = new();
        decoded.CopyTo(destination);
        return destination.ToArray();
    }

    private static byte[] ReadPuzzle(string fileName) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "puzzle", fileName));
}
