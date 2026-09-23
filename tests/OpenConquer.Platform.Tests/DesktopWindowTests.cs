using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.Windowing.Desktop;
using Silk.NET.Windowing;

namespace OpenConquer.Platform.Tests;

public sealed class DesktopWindowTests
{
    [Theory]
    [InlineData(DesktopWindowMode.Resizable, WindowState.Normal, WindowBorder.Resizable)]
    [InlineData(DesktopWindowMode.Fixed, WindowState.Normal, WindowBorder.Fixed)]
    [InlineData(DesktopWindowMode.Fullscreen, WindowState.Fullscreen, WindowBorder.Hidden)]
    public void CreateOptions_UsesWindowModePolicyAndHostSize(DesktopWindowMode windowMode, WindowState expectedWindowState, WindowBorder expectedWindowBorder)
    {
        WindowOptions options = DesktopWindow.CreateOptions(new PixelSize(width: 1280, height: 720), windowMode);

        Assert.Equal(1280, options.Size.X);
        Assert.Equal(720, options.Size.Y);
        Assert.Equal(expectedWindowState, options.WindowState);
        Assert.Equal(expectedWindowBorder, options.WindowBorder);
        Assert.False(options.VSync);
        Assert.Equal(0, options.FramesPerSecond);
        Assert.Equal(0, options.UpdatesPerSecond);
        Assert.Equal(0, options.Samples);
    }

    [Fact]
    public void CreateOptions_UsesRequestedWindowSizeForFixedMode()
    {
        WindowOptions options = DesktopWindow.CreateOptions(new PixelSize(width: 1280, height: 720), DesktopWindowMode.Fixed);

        Assert.Equal(1280, options.Size.X);
        Assert.Equal(720, options.Size.Y);
    }

    [Fact]
    public void CreateOptions_RejectsUnknownWindowMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DesktopWindow.CreateOptions(new PixelSize(width: 1024, height: 768), (DesktopWindowMode)99));
    }

    [Theory]
    [InlineData(0, 768)]
    [InlineData(1024, 0)]
    public void CreateOptions_RejectsEmptyDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DesktopWindow.CreateOptions(new PixelSize(width, height), DesktopWindowMode.Resizable));
    }

    [Theory]
    [InlineData(0f, 0f, 800, 600, 800, 600, 0, 0)]
    [InlineData(799f, 599f, 800, 600, 800, 600, 799, 599)]
    [InlineData(10f, 20f, 800, 600, 1600, 1200, 20, 40)]
    [InlineData(10f, 20f, 800, 600, 1600, 900, 20, 30)]
    [InlineData(10f, 20f, 800, 600, 1200, 900, 15, 30)]
    [InlineData(10.75f, 20.75f, 800, 600, 1200, 900, 16, 31)]
    [InlineData(400f, 300f, 800, 600, 1024, 768, 512, 384)]
    [InlineData(640f, 360f, 1280, 720, 800, 600, 400, 300)]
    public void TryMapWindowPointToFramebuffer_MapsUsingFullFramebufferRatio(float windowX, float windowY, int windowWidth, int windowHeight, int framebufferWidth, int framebufferHeight, int expectedX, int expectedY)
    {
        bool mapped = DesktopWindow.TryMapWindowPointToFramebuffer(windowX, windowY, windowWidth, windowHeight, framebufferWidth, framebufferHeight, out PixelPoint framebufferPoint);

        Assert.True(mapped);
        Assert.Equal(new PixelPoint(expectedX, expectedY), framebufferPoint);
    }

    [Theory]
    [InlineData(-0.25f, 10f, 800, 600, 1600, 1200, -1, 20)]
    [InlineData(10f, -0.25f, 800, 600, 1600, 1200, 20, -1)]
    [InlineData(800f, 600f, 800, 600, 1600, 1200, 1600, 1200)]
    [InlineData(801f, 601f, 800, 600, 1600, 1200, 1602, 1202)]
    public void TryMapWindowPointToFramebuffer_PreservesCoordinatesOutsideClientArea(float windowX, float windowY, int windowWidth, int windowHeight, int framebufferWidth, int framebufferHeight, int expectedX, int expectedY)
    {
        bool mapped = DesktopWindow.TryMapWindowPointToFramebuffer(windowX, windowY, windowWidth, windowHeight, framebufferWidth, framebufferHeight, out PixelPoint framebufferPoint);

        Assert.True(mapped);
        Assert.Equal(new PixelPoint(expectedX, expectedY), framebufferPoint);
    }

    [Theory]
    [InlineData(0, 600, 800, 600)]
    [InlineData(-1, 600, 800, 600)]
    [InlineData(800, 0, 800, 600)]
    [InlineData(800, -1, 800, 600)]
    [InlineData(800, 600, 0, 600)]
    [InlineData(800, 600, -1, 600)]
    [InlineData(800, 600, 800, 0)]
    [InlineData(800, 600, 800, -1)]
    public void TryMapWindowPointToFramebuffer_RejectsNonPositiveExtents(int windowWidth, int windowHeight, int framebufferWidth, int framebufferHeight)
    {
        bool mapped = DesktopWindow.TryMapWindowPointToFramebuffer(100f, 100f, windowWidth, windowHeight, framebufferWidth, framebufferHeight, out PixelPoint framebufferPoint);

        Assert.False(mapped);
        Assert.Equal(default, framebufferPoint);
    }

    [Fact]
    public void TryMapWindowPointToFramebuffer_RejectsNonFiniteHorizontalCoordinate()
    {
        Assert.False(DesktopWindow.TryMapWindowPointToFramebuffer(float.NaN, 10f, 800, 600, 1600, 1200, out _));
        Assert.False(DesktopWindow.TryMapWindowPointToFramebuffer(float.PositiveInfinity, 10f, 800, 600, 1600, 1200, out _));
        Assert.False(DesktopWindow.TryMapWindowPointToFramebuffer(float.NegativeInfinity, 10f, 800, 600, 1600, 1200, out _));
    }

    [Fact]
    public void TryMapWindowPointToFramebuffer_RejectsNonFiniteVerticalCoordinate()
    {
        Assert.False(DesktopWindow.TryMapWindowPointToFramebuffer(10f, float.NaN, 800, 600, 1600, 1200, out _));
        Assert.False(DesktopWindow.TryMapWindowPointToFramebuffer(10f, float.PositiveInfinity, 800, 600, 1600, 1200, out _));
        Assert.False(DesktopWindow.TryMapWindowPointToFramebuffer(10f, float.NegativeInfinity, 800, 600, 1600, 1200, out _));
    }

    [Theory]
    [InlineData(float.MaxValue, 0f)]
    [InlineData(-float.MaxValue, 0f)]
    [InlineData(0f, float.MaxValue)]
    [InlineData(0f, -float.MaxValue)]
    public void TryMapWindowPointToFramebuffer_RejectsMappedCoordinatesOutsideInt32Range(float windowX, float windowY)
    {
        bool mapped = DesktopWindow.TryMapWindowPointToFramebuffer(windowX, windowY, 1, 1, int.MaxValue, int.MaxValue, out PixelPoint framebufferPoint);

        Assert.False(mapped);
        Assert.Equal(default, framebufferPoint);
    }
}
