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

    private static TemporaryContentDirectory CreateNewbieContent()
    {
        TemporaryContentDirectory root = new();
        root.WriteFile("ini/GameMap.dat",
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "ini", "GameMap.dat")));
        root.WriteFile("map/map/newbie.7z",
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "newbie.7z")));
        return root;
    }

    private static byte[] ReadPuzzle(string fileName) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "puzzle", fileName));
}
