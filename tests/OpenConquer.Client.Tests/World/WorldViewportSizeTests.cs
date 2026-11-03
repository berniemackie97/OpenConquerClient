using OpenConquer.Client.World;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Presentation;
using OpenConquerStudio.ConquerRendering;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldViewportSizeTests
{
    private static readonly LogicalRenderSize s_stage = new(1024, 768);
    private static readonly LogicalRenderSize s_backdrop = new(1536, 1536);

    [Theory]
    [InlineData(1024, 768, 1024, 768)]
    [InlineData(1920, 1080, 1365, 768)]
    [InlineData(1613, 900, 1376, 768)]
    [InlineData(1440, 125, 1536, 133)]
    [InlineData(1080, 1920, 432, 768)]
    public void WorldViewportFollowsFramebufferWithinTheAvailableBackdrop(
        int framebufferWidth, int framebufferHeight, int width, int height)
    {
        PixelSize framebuffer = new(framebufferWidth, framebufferHeight);
        LogicalRenderSize world = WorldViewportSize.FromFramebuffer(s_stage, framebuffer, s_backdrop, 4096);
        PresentationViewport destination = PresentationViewport.Compute(world,
            framebufferWidth, framebufferHeight, PresentationPolicy.Stretch);

        Assert.Equal((width, height), (world.Width, world.Height));
        Assert.True(destination.CoversHostFramebuffer());
        Assert.True(world.Width <= s_backdrop.Width);
        Assert.True(world.Height <= s_backdrop.Height);
        Assert.InRange(Math.Abs(destination.ScaleX - destination.ScaleY), 0, 0.005);
    }

    [Theory]
    [InlineData(1440, 1)]
    [InlineData(1440, 2)]
    [InlineData(1440, 125)]
    [InlineData(1440, 900)]
    [InlineData(8000, 80)]
    public void ResizeDoesNotRequestOversizedFramebufferResources(int width, int height)
    {
        PixelSize framebuffer = new(width, height);
        LogicalRenderSize world = WorldViewportSize.FromFramebuffer(s_stage, framebuffer, s_backdrop, 1024);

        Assert.InRange(world.Width, 1, 1024);
        Assert.InRange(world.Height, 1, 1024);
    }

    [Fact]
    public void SceneBackdropSuppliesTheMapCoverageLimit()
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], 60, 108, 100, 1000);
        Assert.Equal(new LogicalRenderSize(1536, 1536), scene.BackdropExtent);
    }

    [Fact]
    public void InvalidFramebufferDimensionsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldViewportSize.FromFramebuffer(s_stage, new PixelSize(0, 1080), s_backdrop, 4096));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldViewportSize.FromFramebuffer(s_stage, new PixelSize(1920, 0), s_backdrop, 4096));
    }
}
