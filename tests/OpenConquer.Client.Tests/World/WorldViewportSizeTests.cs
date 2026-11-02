using OpenConquer.Client.World;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldViewportSizeTests
{
    [Theory]
    [InlineData(1024, 768, 1024)]
    [InlineData(1920, 1080, 1365)]
    [InlineData(1613, 900, 1376)]
    [InlineData(1080, 1920, 432)]
    public void ResizingChangesTheWorldCanvasWidthWithoutStretchingMapLayers(
        int framebufferWidth, int framebufferHeight, int expectedWidth)
    {
        LogicalRenderSize stage = new(1024, 768);
        PixelSize framebuffer = new(framebufferWidth, framebufferHeight);

        LogicalRenderSize world = WorldViewportSize.FromFramebuffer(stage, framebuffer);
        PresentationViewport destination = PresentationViewport.Compute(world,
            framebufferWidth, framebufferHeight, PresentationPolicy.Stretch);

        Assert.Equal(expectedWidth, world.Width);
        Assert.Equal(stage.Height, world.Height);
        Assert.True(destination.CoversHostFramebuffer());
        Assert.InRange(Math.Abs(destination.ScaleX - destination.ScaleY), 0, 0.001);
    }

    [Fact]
    public void InvalidFramebufferDimensionsAreRejected()
    {
        LogicalRenderSize stage = new(1024, 768);
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldViewportSize.FromFramebuffer(stage, new PixelSize(0, 1080)));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldViewportSize.FromFramebuffer(stage, new PixelSize(1920, 0)));
    }
}
