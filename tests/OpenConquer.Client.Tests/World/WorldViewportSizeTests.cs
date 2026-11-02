using OpenConquer.Client.World;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldViewportSizeTests
{
    [Theory]
    [InlineData(800, 600, 0, 0, 800, 600)]
    [InlineData(1440, 810, 180, 0, 1080, 810)]
    [InlineData(878, 808, 0, 75, 878, 658)]
    [InlineData(1000, 1000, 0, 125, 1000, 750)]
    public void ResizePresentsTheSameWorldCanvasWithoutCroppingOrDistortion(
        int framebufferWidth, int framebufferHeight,
        int x, int y, int width, int height)
    {
        LogicalRenderSize world = WorldViewportSize.Logical;
        PresentationViewport viewport = PresentationViewport.Compute(world,
            framebufferWidth, framebufferHeight, PresentationPolicy.Fit);

        Assert.Equal(800, world.Width);
        Assert.Equal(600, world.Height);
        Assert.Equal((x, y, width, height),
            (viewport.OffsetX, viewport.OffsetY, viewport.Width, viewport.Height));
        Assert.True(viewport.Width <= framebufferWidth);
        Assert.True(viewport.Height <= framebufferHeight);
        Assert.InRange(Math.Abs(viewport.ScaleX - viewport.ScaleY), 0, 0.002);
    }

    [Fact]
    public void DifferentWindowSizesDoNotAlterSceneTilePositions()
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], 60, 108, 100, 1000);
        LogicalRenderSize world = WorldViewportSize.Logical;
        IReadOnlyList<WorldMapLayer> expected = scene.BuildFrame(world.Width, world.Height, 1000);

        foreach ((int width, int height) in new[] { (800, 600), (878, 808), (1440, 810) })
        {
            PresentationViewport viewport = PresentationViewport.Compute(world,
                width, height, PresentationPolicy.Fit);
            Assert.False(viewport.IsEmpty);

            IReadOnlyList<WorldMapLayer> actual = scene.BuildFrame(world.Width, world.Height, 1000);
            Assert.Equal(expected[0].Puzzle, actual[0].Puzzle);
            Assert.Equal(expected[1].Puzzle, actual[1].Puzzle);
        }
    }
}
