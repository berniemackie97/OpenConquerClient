using OpenConquer.Client.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldCameraProjectionTests
{
    [Fact]
    public void NewbieVillageTile_ProjectsToWorldPixelsAndClampedGroundOffset()
    {
        using WorldMapFixture fixture = new();
        Assert.Equal((5984, 2016), WorldCameraProjection.ProjectTile(fixture.Map, 90, 35));

        WorldPuzzleProjection projection = WorldCameraProjection.FromHeroWorldPosition(
            fixture.Map, fixture.Puzzles, 5984, 2016, 800, 600);

        Assert.Equal((5344, 1716), (projection.CenterX, projection.CenterY));
        Assert.Equal((-3040, -756), (projection.GroundOffsetX, projection.GroundOffsetY));
        Assert.Equal((256, 256), (projection.ScaleNumerator, projection.ScaleDenominator));
    }

    [Fact]
    public void CameraProjection_ClampsAtBothPuzzleExtents()
    {
        using WorldMapFixture fixture = new();
        WorldPuzzleProjection lower = WorldCameraProjection.FromHeroWorldPosition(
            fixture.Map, fixture.Puzzles, 0, 0, 800, 600);
        WorldPuzzleProjection upper = WorldCameraProjection.FromHeroWorldPosition(
            fixture.Map, fixture.Puzzles, 100_000, 100_000, 800, 600);

        Assert.Equal((2304, 960), (lower.CenterX, lower.CenterY));
        Assert.Equal((0, 0), (lower.GroundOffsetX, lower.GroundOffsetY));
        Assert.Equal((5344, 2664), (upper.CenterX, upper.CenterY));
        Assert.Equal((-3040, -1704), (upper.GroundOffsetX, upper.GroundOffsetY));
    }

    [Fact]
    public void ZoomAndEdgeScroll_ApplyScaleAndClamp()
    {
        using WorldMapFixture fixture = new();
        WorldPuzzleProjection scaled = WorldCameraProjection.FromHeroWorldPosition(
            fixture.Map, fixture.Puzzles, 4224, 1616, 800, 600, zoomPercent: 125,
            edgeScrollX: 50, edgeScrollY: -20);

        Assert.Equal((3874, 1296), (scaled.CenterX, scaled.CenterY));
        Assert.Equal((-1570, -336), (scaled.GroundOffsetX, scaled.GroundOffsetY));
        Assert.Equal((320, 256), (scaled.ScaleNumerator, scaled.ScaleDenominator));

        WorldPuzzleProjection lower = WorldCameraProjection.FromHeroWorldPosition(
            fixture.Map, fixture.Puzzles, 0, 0, 800, 600, zoomPercent: 125);
        Assert.Equal((2224, 900), (lower.CenterX, lower.CenterY));
    }

    [Fact]
    public void MissingGroundPuzzle_UsesConstructorDimensionsForCameraBounds()
    {
        using WorldMapFixture fixture = new(missingGround: true);
        Assert.Equal((20, 20), (fixture.Puzzles.GroundColumns, fixture.Puzzles.GroundRows));

        WorldPuzzleProjection projection = WorldCameraProjection.FromHeroWorldPosition(
            fixture.Map, fixture.Puzzles, 2688, 2704, 800, 600);

        Assert.Equal((2288, 2404), (projection.CenterX, projection.CenterY));
        Assert.Equal((-624, -2852), (projection.GroundOffsetX, projection.GroundOffsetY));
    }

    [Fact]
    public void InvalidProjectionParameters_FailWithoutFallbackValues()
    {
        using WorldMapFixture fixture = new();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldCameraProjection.FromHeroWorldPosition(fixture.Map, fixture.Puzzles, 0, 0, 800, 600, zoomPercent: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldCameraProjection.FromHeroWorldPosition(fixture.Map, fixture.Puzzles, 0, 0, 0, 600));
    }
}
