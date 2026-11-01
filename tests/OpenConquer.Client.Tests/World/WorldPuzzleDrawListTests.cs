using System.Globalization;
using OpenConquer.Client.World;
using OpenConquer.Content.Map;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldPuzzleDrawListTests
{
    private const int GroundOffsetX = -9 * 256;
    private const int GroundOffsetY = -3 * 256;

    [Fact]
    public void NewbieVillageGround_EnumeratesVisibleFramesInTileOrder()
    {
        using WorldMapFixture fixture = new();
        WorldPuzzleProjection projection = new(800, 600, GroundOffsetX, GroundOffsetY, 3500, 1400, 1, 1);

        IReadOnlyList<WorldPuzzleTile> tiles = WorldPuzzleDrawList.Ground(fixture.Map, fixture.Puzzles, projection, 0);

        Assert.Equal(
            ["Puzzle14", "Puzzle15", "Puzzle16", "Puzzle17", "Puzzle20", "Puzzle21", "Puzzle22", "Puzzle23",
             "Puzzle26", "Puzzle27", "Puzzle28", "Puzzle29"],
            tiles.Select(tile => tile.Animation.Name));
        Assert.All(tiles, tile =>
        {
            Assert.Equal(256, tile.Size);
            Assert.Equal(0, tile.FrameIndex);
            Assert.Equal(0, tile.X % 256);
            Assert.Equal(0, tile.Y % 256);
            Assert.True(tile.X < projection.ScreenWidth && tile.Y < projection.ScreenHeight);
            Assert.True(tile.X + tile.Size > 0 && tile.Y + tile.Size > 0);
            Assert.StartsWith("data/map/puzzle/", tile.Animation.FramePaths[tile.FrameIndex], StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void NewbieVillageBackdrop_UsesPositionedPuzzleAndParallax()
    {
        using WorldMapFixture fixture = new();
        WorldPuzzleProjection projection = new(800, 600, 0, 0, 3500, 1400, 1, 1);

        GameMapPositionedOverlayRecord record = Assert.Single(fixture.Map.Scene.PositionedOverlays);
        Assert.Equal(0, record.InsertIndex);
        GameMapPuzzleOverlayEntry overlay = Assert.IsType<GameMapPuzzleOverlayEntry>(Assert.Single(record.Entries));
        IReadOnlyList<WorldPuzzleTile> tiles = WorldPuzzleDrawList.PositionedPuzzle(
            fixture.Map, fixture.Puzzles, projection, record, overlay, frame: 0);

        Assert.NotEmpty(tiles);
        GameMapPuzzleBitmap backdrop = fixture.Puzzles.GetRequired(overlay.PuzzlePath);

        long halfMapWidth = (long)fixture.Map.CellGrid.Width * 64 / 2;
        long halfMapHeight = (long)fixture.Map.CellGrid.Height * 32 / 2;
        long rootX = (long)record.HeaderInt0 *
            (projection.CenterX + projection.ScreenWidth / 2L - halfMapWidth) / 100 +
            halfMapWidth - projection.ScreenWidth / 2;
        long rootY = (long)record.HeaderInt1 *
            (projection.CenterY + projection.ScreenHeight / 2L - halfMapHeight) / 100 +
            halfMapHeight - projection.ScreenHeight / 2;
        long originX = halfMapWidth - backdrop.Columns * 256L / 2 - rootX;
        long originY = halfMapHeight - backdrop.Rows * 256L / 2 - rootY;

        Assert.All(tiles, tile =>
        {
            int tileIndex = int.Parse(tile.Animation.Name.AsSpan(6), CultureInfo.InvariantCulture) - 50;
            Assert.InRange(tileIndex, 0, backdrop.Columns * backdrop.Rows - 1);
            Assert.Equal(checked((int)(originX + tileIndex % backdrop.Columns * 256L)), tile.X);
            Assert.Equal(checked((int)(originY + tileIndex / backdrop.Columns * 256L)), tile.Y);
            Assert.Equal(256, tile.Size);
            Assert.Equal(0, tile.FrameIndex);
            Assert.StartsWith("data/map/puzzle/newbie/bg", tile.Animation.FramePaths[tile.FrameIndex], StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void PositionedPuzzle_UsesSuppliedNonzeroFrameForVisibleTiles()
    {
        using WorldMapFixture fixture = new(twoFrameBackdrop: true);
        WorldPuzzleProjection projection = new(800, 600, 0, 0, 3500, 1400, 1, 1);
        GameMapPositionedOverlayRecord record = Assert.Single(fixture.Map.Scene.PositionedOverlays);
        GameMapPuzzleOverlayEntry overlay =
            Assert.IsType<GameMapPuzzleOverlayEntry>(Assert.Single(record.Entries));

        IReadOnlyList<WorldPuzzleTile> tiles = WorldPuzzleDrawList.PositionedPuzzle(
            fixture.Map, fixture.Puzzles, projection, record, overlay, frame: 1);

        Assert.NotEmpty(tiles);
        Assert.All(tiles, tile =>
        {
            Assert.Equal(2, tile.Animation.FrameCount);
            Assert.Equal(1, tile.FrameIndex);
            Assert.Equal("data/map/puzzle/newbie/bg001.dds",
                tile.Animation.FramePaths[tile.FrameIndex]);
        });
    }

    [Fact]
    public void LargerViewport_DoesNotExpandThe800By600TileEnumeration()
    {
        using WorldMapFixture fixture = new();
        IReadOnlyList<WorldPuzzleTile> baseTiles = WorldPuzzleDrawList.Ground(fixture.Map, fixture.Puzzles,
            new WorldPuzzleProjection(800, 600, GroundOffsetX, GroundOffsetY, 3500, 1400, 1, 1), 0);
        IReadOnlyList<WorldPuzzleTile> larger = WorldPuzzleDrawList.Ground(fixture.Map, fixture.Puzzles,
            new WorldPuzzleProjection(1024, 768, GroundOffsetX, GroundOffsetY, 3500, 1400, 1, 1), 0);

        Assert.Equal(baseTiles.Count, larger.Count);
        Assert.Equal(baseTiles.Select(t => t.Animation.Name), larger.Select(t => t.Animation.Name));
    }

    [Fact]
    public void RejectsInvalidProjection()
    {
        using WorldMapFixture fixture = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => WorldPuzzleDrawList.Ground(fixture.Map, fixture.Puzzles,
            new WorldPuzzleProjection(800, 600, 0, 0, 0, 0, 1, 0), 0));
    }

}
