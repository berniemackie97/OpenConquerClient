using Silk.NET.Windowing;

namespace OpenConquer.Platform.Tests;

public sealed class StartupWindowTests
{
    [Fact]
    public void CreateOptions_UsesNaturalLogicalSizeAndBorderlessHiddenStartupWindow()
    {
        WindowOptions options = StartupWindow.CreateOptions(new PixelSize(width: 500, height: 375));

        Assert.Equal(500, options.Size.X);
        Assert.Equal(375, options.Size.Y);
        Assert.Equal(WindowBorder.Hidden, options.WindowBorder);
        Assert.False(options.IsVisible);
        Assert.False(options.VSync);
        Assert.Equal(0, options.FramesPerSecond);
        Assert.Equal(0, options.UpdatesPerSecond);
        Assert.Equal(0, options.Samples);
    }

    [Theory]
    [InlineData(0, 375)]
    [InlineData(500, 0)]
    public void CreateOptions_RejectsEmptyDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StartupWindow.CreateOptions(new PixelSize(width, height))
        );
    }
}
