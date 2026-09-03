namespace OpenConquer.Rendering.Tests;

public sealed class StartupSurfacePlacementTests
{
    /// <summary>
    /// At a device pixel ratio of 1 the logo occupies exactly its own pixels, which is the verified
    /// native pattern-brush result.
    /// </summary>
    [Fact]
    public void Compute_AtUnitDensity_UsesTheImagesOwnSize()
    {
        StartupSurfacePlacement placement = StartupSurfacePlacement.Compute(
            framebufferWidth: 500,
            framebufferHeight: 375,
            logicalWidth: 500,
            logicalHeight: 375,
            imageWidth: 500,
            imageHeight: 375
        );

        Assert.Equal(new StartupSurfacePlacement(500, 375), placement);
    }

    [Theory]
    [InlineData(2, 1000, 750)]
    [InlineData(3, 1500, 1125)]
    public void Compute_ScalesByTheDevicePixelRatio(int devicePixelRatio, int expectedWidth, int expectedHeight)
    {
        StartupSurfacePlacement placement = StartupSurfacePlacement.Compute(
            framebufferWidth: 500 * devicePixelRatio,
            framebufferHeight: 375 * devicePixelRatio,
            logicalWidth: 500,
            logicalHeight: 375,
            imageWidth: 500,
            imageHeight: 375
        );

        Assert.Equal(new StartupSurfacePlacement(expectedWidth, expectedHeight), placement);
    }

    /// <summary>
    /// Fractional ratios such as the 1.5x used by many Windows displays must still land on whole
    /// pixels.
    /// </summary>
    [Fact]
    public void Compute_AtAFractionalDensity_RoundsToWholePixels()
    {
        StartupSurfacePlacement placement = StartupSurfacePlacement.Compute(
            framebufferWidth: 750,
            framebufferHeight: 563,
            logicalWidth: 500,
            logicalHeight: 375,
            imageWidth: 500,
            imageHeight: 375
        );

        Assert.Equal(new StartupSurfacePlacement(750, 563), placement);
    }

    /// <summary>
    /// An image smaller than the window keeps its natural size instead of being stretched to fill,
    /// and one larger than the window keeps its natural size instead of being shrunk to fit. Both
    /// are the same rule: never scale to the surface.
    /// </summary>
    [Theory]
    [InlineData(100, 80, 100, 80)]
    [InlineData(900, 700, 900, 700)]
    public void Compute_NeverFitsTheImageToTheSurface(int imageWidth, int imageHeight, int expectedWidth, int expectedHeight)
    {
        StartupSurfacePlacement placement = StartupSurfacePlacement.Compute(
            framebufferWidth: 500,
            framebufferHeight: 375,
            logicalWidth: 500,
            logicalHeight: 375,
            imageWidth,
            imageHeight
        );

        Assert.Equal(new StartupSurfacePlacement(expectedWidth, expectedHeight), placement);
    }

    [Fact]
    public void Compute_ClampsToAtLeastOnePixel()
    {
        StartupSurfacePlacement placement = StartupSurfacePlacement.Compute(
            framebufferWidth: 1,
            framebufferHeight: 1,
            logicalWidth: 4000,
            logicalHeight: 4000,
            imageWidth: 2,
            imageHeight: 2
        );

        Assert.Equal(new StartupSurfacePlacement(1, 1), placement);
    }

    [Theory]
    [InlineData(0, 375, 500, 375, 500, 375)]
    [InlineData(500, 0, 500, 375, 500, 375)]
    [InlineData(500, 375, 0, 375, 500, 375)]
    [InlineData(500, 375, 500, 0, 500, 375)]
    [InlineData(500, 375, 500, 375, 0, 375)]
    [InlineData(500, 375, 500, 375, 500, 0)]
    [InlineData(-1, 375, 500, 375, 500, 375)]
    public void Compute_RejectsNonPositiveDimensions(
        int framebufferWidth,
        int framebufferHeight,
        int logicalWidth,
        int logicalHeight,
        int imageWidth,
        int imageHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StartupSurfacePlacement.Compute(
            framebufferWidth,
            framebufferHeight,
            logicalWidth,
            logicalHeight,
            imageWidth,
            imageHeight
        ));
    }
}
