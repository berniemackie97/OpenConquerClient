using System.Globalization;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapContentTests
{
    [Fact]
    public void RetailNewbie_LoadsByMapDataIdThroughCatalogAndContentSource()
    {
        using TemporaryContentDirectory root = CreateNewbieContent();
        ClientContentRoot source = new(root.RootPath);
        GameMapCatalog catalog = GameMapCatalog.Load(source);

        GameMapContent map = GameMapContent.Load(source, catalog, 1010);

        Assert.Equal(1010u, map.MapDataId);
        Assert.Equal("map/map/newbie.7z", map.ArchivePath);
        Assert.Equal(256u, map.CompanionValue);
        Assert.Equal((132, 132), (map.CellGrid.Width, map.CellGrid.Height));
        Assert.Equal("map/puzzle/newbie.pul", map.CellGrid.PuzzlePath);
        Assert.Equal(38, map.Scene.Scenery.Count);
        Assert.Equal("map/puzzle/newbiebg.pul",
            Assert.IsType<GameMapPuzzleOverlayEntry>(
                Assert.Single(Assert.Single(map.Scene.PositionedOverlays).Entries)).PuzzlePath);
    }

    [Fact]
    public void MissingMapDataId_DoesNotFallbackToAnotherMap()
    {
        using TemporaryContentDirectory root = CreateNewbieContent();
        ClientContentRoot source = new(root.RootPath);
        GameMapCatalog catalog = GameMapCatalog.Load(source);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => GameMapContent.Load(source, catalog, uint.MaxValue));

        Assert.Contains(uint.MaxValue.ToString(CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingMapArchive_FailsRatherThanReturningAnEmptyScene()
    {
        using TemporaryContentDirectory root = new();
        CopyRetailIndex(root);
        ClientContentRoot source = new(root.RootPath);
        GameMapCatalog catalog = GameMapCatalog.Load(source);

        Assert.Throws<FileNotFoundException>(() => GameMapContent.Load(source, catalog, 1010));
    }

    [Fact]
    public void IncorrectArchiveIdentity_DoesNotSubstituteAnotherMap()
    {
        using TemporaryContentDirectory root = CreateNewbieContent();
        ClientContentRoot source = new(root.RootPath);
        GameMapCatalog catalog = GameMapCatalog.Load(source);

        // The catalog's map-data ID 1002 selects newplain.7z, not newbie.7z.
        root.WriteFile("map/map/newplain.7z", ReadRetailNewbieArchive());

        Assert.Throws<InvalidDataException>(() => GameMapContent.Load(source, catalog, 1002));
    }

    private static TemporaryContentDirectory CreateNewbieContent()
    {
        TemporaryContentDirectory root = new();
        CopyRetailIndex(root);
        root.WriteFile("map/map/newbie.7z", ReadRetailNewbieArchive());
        return root;
    }

    private static void CopyRetailIndex(TemporaryContentDirectory root)
    {
        byte[] index = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "ini", "GameMap.dat"));
        root.WriteFile("ini/GameMap.dat", index);
    }

    private static byte[] ReadRetailNewbieArchive() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "newbie.7z"));
}
