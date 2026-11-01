using OpenConquer.Client.World;
using OpenConquer.Content.Map;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldMapSceneTests
{
    [Fact]
    public void NewbieVillage_FirstFrameOrdersBackdropGroundAndTerrain()
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], 60, 108, 100, 1000);

        IReadOnlyList<WorldMapLayer> layers = scene.BuildFrame(800, 600, 1000);

        Assert.Equal(6, layers.Count);
        Assert.NotEmpty(layers[0].Puzzle!);
        Assert.NotEmpty(layers[1].Puzzle!);
        Assert.Null(layers[2].Puzzle);
        Assert.Null(layers[3].Puzzle);
        Assert.NotNull(layers[4].Terrain);
        Assert.Null(layers[5].Puzzle);

        (int x, int y) = WorldCameraProjection.ProjectTile(fixture.Map, 60, 108);
        WorldPuzzleProjection projection = WorldCameraProjection.FromHeroWorldPosition(
            fixture.Map, fixture.Puzzles, x, y, 800, 600);
        IReadOnlyList<WorldPuzzleTile> expected = WorldPuzzleDrawList.Ground(
            fixture.Map, fixture.Puzzles, projection, 0);

        Assert.Equal(expected, layers[1].Puzzle);
    }

    [Fact]
    public void PositionedPuzzle_AdvancesWithItsOwnFrameClock()
    {
        using WorldMapFixture fixture = new(twoFrameBackdrop: true);
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], 60, 108, 100, 1000);

        IReadOnlyList<WorldPuzzleTile> first = scene.BuildFrame(800, 600, 1000)[0].Puzzle!;
        IReadOnlyList<WorldPuzzleTile> second = scene.BuildFrame(800, 600, 1100)[0].Puzzle!;
        IReadOnlyList<WorldPuzzleTile> third = scene.BuildFrame(800, 600, 1200)[0].Puzzle!;

        Assert.All(first, static tile => Assert.Equal(0, tile.FrameIndex));
        Assert.All(second, static tile => Assert.Equal(0, tile.FrameIndex));
        Assert.All(third, static tile => Assert.Equal(1, tile.FrameIndex));
    }
}
