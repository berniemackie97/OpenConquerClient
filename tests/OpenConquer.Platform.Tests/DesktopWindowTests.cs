using Silk.NET.Windowing;

namespace OpenConquer.Platform.Tests;

public sealed class DesktopWindowTests
{
    [Theory]
    [InlineData(DesktopWindowMode.Resizable, WindowState.Normal, WindowBorder.Resizable)]
    [InlineData(DesktopWindowMode.Fixed, WindowState.Normal, WindowBorder.Fixed)]
    [InlineData(DesktopWindowMode.Fullscreen, WindowState.Fullscreen, WindowBorder.Hidden)]
    public void CreateOptions_UsesWindowModePolicyAndHostSize(
        DesktopWindowMode windowMode,
        WindowState expectedWindowState,
        WindowBorder expectedWindowBorder)
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
            DesktopWindow.CreateOptions(new PixelSize(width: 1024, height: 768), (DesktopWindowMode)99)
        );
    }

    [Theory]
    [InlineData(0, 768)]
    [InlineData(1024, 0)]
    public void CreateOptions_RejectsEmptyDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DesktopWindow.CreateOptions(new PixelSize(width, height), DesktopWindowMode.Resizable)
        );
    }
}
