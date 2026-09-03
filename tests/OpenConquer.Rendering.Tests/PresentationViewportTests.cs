namespace OpenConquer.Rendering.Tests;

/// <summary>
/// Covers the logical-to-host transform that both rendering and input depend on.
/// </summary>
/// <remarks>
/// Every case here runs without a graphics device, which is the point of keeping the transform
/// out of the renderer: the arithmetic that decides where a frame is drawn and where a click
/// lands is the part that must not be trusted to manual testing.
/// </remarks>
public sealed class PresentationViewportTests
{
    private static readonly LogicalRenderSize s_logical1024 = new(width: 1024, height: 768);
    private static readonly LogicalRenderSize s_logical800 = new(width: 800, height: 600);

    [Theory]
    // 4:3 logical inside 16:9 hosts: the regression this type exists to prevent.
    [InlineData(1024, 768, 1280, 720)]
    [InlineData(1024, 768, 1920, 1080)]
    [InlineData(1024, 768, 2560, 1440)]
    [InlineData(800, 600, 1280, 720)]
    [InlineData(800, 600, 3440, 1440)]
    // Taller-than-logical hosts letterbox instead of pillarboxing.
    [InlineData(1024, 768, 800, 1200)]
    // Awkward sizes a window can actually be dragged to.
    [InlineData(1024, 768, 1001, 733)]
    [InlineData(800, 600, 1, 1)]
    public void Compute_Fit_NeverDistortsAspectRatio(int logicalWidth, int logicalHeight, int hostWidth, int hostHeight)
    {
        LogicalRenderSize logical = new(logicalWidth, logicalHeight);

        PresentationViewport viewport = PresentationViewport.Compute(logical, hostWidth, hostHeight, PresentationPolicy.Fit);

        // Integer extents cannot always express the ratio exactly, so the tolerance is one
        // destination pixel across the larger axis rather than an exact equality.
        double tolerance = 1d / Math.Min(viewport.Width, viewport.Height);

        Assert.True(Math.Abs(viewport.ScaleX - viewport.ScaleY) <= tolerance,
            $"scaleX={viewport.ScaleX} scaleY={viewport.ScaleY} exceeded tolerance {tolerance}");
    }

    [Theory]
    [InlineData(1280, 720, 960, 720, 160, 0)]
    [InlineData(1920, 1080, 1440, 1080, 240, 0)]
    [InlineData(1024, 768, 1024, 768, 0, 0)]
    [InlineData(800, 1200, 800, 600, 0, 300)]
    public void Compute_Fit_CentresTheLargestFittingRectangle(int hostWidth, int hostHeight, int expectedWidth, int expectedHeight, int expectedOffsetX, int expectedOffsetY)
    {
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, hostWidth, hostHeight, PresentationPolicy.Fit);

        Assert.Equal(expectedWidth, viewport.Width);
        Assert.Equal(expectedHeight, viewport.Height);
        Assert.Equal(expectedOffsetX, viewport.OffsetX);
        Assert.Equal(expectedOffsetY, viewport.OffsetY);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(800, 1200)]
    public void Compute_Fit_NeverExceedsTheHostFramebuffer(int hostWidth, int hostHeight)
    {
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, hostWidth, hostHeight, PresentationPolicy.Fit);

        Assert.True(viewport.OffsetX >= 0 && viewport.OffsetY >= 0);
        Assert.True(viewport.OffsetX + viewport.Width <= hostWidth);
        Assert.True(viewport.OffsetY + viewport.Height <= hostHeight);
    }

    [Theory]
    [InlineData(1920, 1080, 1024, 768, 448, 156)]
    [InlineData(2560, 1600, 2048, 1536, 256, 32)]
    [InlineData(3200, 2400, 3072, 2304, 64, 48)]
    public void Compute_IntegerScale_UsesWholeMultiplesAndCentres(int hostWidth, int hostHeight, int expectedWidth, int expectedHeight, int expectedOffsetX, int expectedOffsetY)
    {
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, hostWidth, hostHeight, PresentationPolicy.IntegerScale);

        Assert.Equal(expectedWidth, viewport.Width);
        Assert.Equal(expectedHeight, viewport.Height);
        Assert.Equal(expectedOffsetX, viewport.OffsetX);
        Assert.Equal(expectedOffsetY, viewport.OffsetY);
        Assert.Equal(PresentationFilter.Nearest, viewport.Filter);
    }

    [Fact]
    public void Compute_IntegerScale_FallsBackToFitWhenNoWholeCopyFits()
    {
        // A window smaller than one logical frame has no whole-number scale. Clipping the frame
        // would hide part of the game, so the whole frame is fitted instead.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, hostFramebufferWidth: 800, hostFramebufferHeight: 600, PresentationPolicy.IntegerScale);

        Assert.Equal(800, viewport.Width);
        Assert.Equal(600, viewport.Height);
        Assert.False(viewport.IsEmpty);
    }

    [Fact]
    public void Compute_Stretch_FillsTheHostAndIsTheOnlyPolicyThatDistorts()
    {
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, hostFramebufferWidth: 1280, hostFramebufferHeight: 720, PresentationPolicy.Stretch);

        Assert.Equal(0, viewport.OffsetX);
        Assert.Equal(0, viewport.OffsetY);
        Assert.Equal(1280, viewport.Width);
        Assert.Equal(720, viewport.Height);
        Assert.True(viewport.CoversHostFramebuffer());

        // Documented, opt-in distortion: 4:3 content across a 16:9 host is about 1.33x too wide.
        Assert.True(viewport.ScaleX > viewport.ScaleY);
    }

    [Theory]
    [InlineData(1024, 768, PresentationFilter.Nearest)]   // 1:1
    [InlineData(2048, 1536, PresentationFilter.Nearest)]  // exact 2x
    [InlineData(1280, 720, PresentationFilter.Linear)]    // fractional
    [InlineData(2048, 768, PresentationFilter.Linear)]    // whole but unequal per axis
    public void Compute_SelectsPointSamplingOnlyWhenTheCopyIsExact(int hostWidth, int hostHeight, PresentationFilter expected)
    {
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, hostWidth, hostHeight, PresentationPolicy.Stretch);

        Assert.Equal(expected, viewport.Filter);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1280, 0)]
    [InlineData(0, 720)]
    public void Compute_TreatsAZeroSizedHostAsEmptyRatherThanFailing(int hostWidth, int hostHeight)
    {
        // A minimised window reports a zero framebuffer every frame; it is not an error.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, hostWidth, hostHeight);

        Assert.True(viewport.IsEmpty);
        Assert.Equal(0d, viewport.ScaleX);
        Assert.False(viewport.TryMapFramebufferPointToLogical(0, 0, out _, out _));
    }

    [Fact]
    public void Compute_TreatsADefaultLogicalSizeAsEmptyRatherThanDividingByZero()
    {
        // LogicalRenderSize is a struct, so its parameterless default bypasses validation.
        PresentationViewport viewport = PresentationViewport.Compute(default, hostFramebufferWidth: 1280, hostFramebufferHeight: 720);

        Assert.True(viewport.IsEmpty);
    }

    [Theory]
    [InlineData(-1, 720)]
    [InlineData(1280, -1)]
    public void Compute_RejectsNegativeHostDimensions(int hostWidth, int hostHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PresentationViewport.Compute(s_logical1024, hostWidth, hostHeight));
    }

    [Fact]
    public void Compute_RejectsAnUndefinedPolicy()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => PresentationViewport.Compute(s_logical1024, 1280, 720, (PresentationPolicy)99));

        Assert.Equal("policy", exception.ParamName);
    }

    [Fact]
    public void CoversHostFramebuffer_IsFalseWhenBarsArePresent()
    {
        // The renderer uses this to decide whether the host framebuffer needs clearing. Getting it
        // wrong leaves uninitialised bars flickering around the frame.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, 1280, 720, PresentationPolicy.Fit);

        Assert.False(viewport.CoversHostFramebuffer());
    }

    [Theory]
    [InlineData(1280, 720, PresentationPolicy.Fit)]
    [InlineData(1920, 1080, PresentationPolicy.IntegerScale)]
    [InlineData(2560, 1440, PresentationPolicy.Stretch)]
    public void TryMapToLogical_MapsTheDestinationOriginToTheLogicalOrigin(int hostWidth, int hostHeight, PresentationPolicy policy)
    {
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, hostWidth, hostHeight, policy);

        Assert.True(viewport.TryMapFramebufferPointToLogical(viewport.OffsetX, viewport.OffsetY, out int originX, out int originY));
        Assert.Equal(0, originX);
        Assert.Equal(0, originY);
    }

    [Fact]
    public void TryMapToLogical_MapsTheFarCornerToTheLastLogicalPixelWhenNotMinified()
    {
        // 1024x768 at 1:1 inside a 1920x1080 host. Every logical pixel has a destination pixel, so
        // the far corner must reach the last one exactly rather than stopping short.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, 1920, 1080, PresentationPolicy.IntegerScale);

        int lastX = viewport.OffsetX + viewport.Width - 1;
        int lastY = viewport.OffsetY + viewport.Height - 1;

        Assert.True(viewport.TryMapFramebufferPointToLogical(lastX, lastY, out int cornerX, out int cornerY));
        Assert.Equal(1023, cornerX);
        Assert.Equal(767, cornerY);
    }

    [Fact]
    public void TryMapToLogical_LeavesSomeLogicalPixelsUnreachableWhenMinified()
    {
        // Shrinking the window below the logical size means fewer destination pixels than logical
        // ones, so some logical pixels genuinely have no position that resolves to them. That is
        // inherent to downscaling, not an off-by-one: 960 destination columns cannot address 1024
        // logical columns. Pinned so the pointer-precision loss is a known property rather than a
        // surprise once input lands.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, 1280, 720, PresentationPolicy.Fit);

        Assert.Equal(960, viewport.Width);
        Assert.True(viewport.ScaleX < 1d);

        int lastX = viewport.OffsetX + viewport.Width - 1;

        Assert.True(viewport.TryMapFramebufferPointToLogical(lastX, viewport.OffsetY, out int cornerX, out _));
        Assert.Equal(1022, cornerX);
    }

    [Fact]
    public void TryMapToLogical_RejectsPositionsInTheLetterboxBars()
    {
        // 1024x768 in 1280x720 pillarboxes with 160px bars either side. A click on a bar is not a
        // click in the world and must not resolve to one.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, 1280, 720, PresentationPolicy.Fit);

        Assert.False(viewport.TryMapFramebufferPointToLogical(0, 360, out _, out _));
        Assert.False(viewport.TryMapFramebufferPointToLogical(159, 360, out _, out _));
        Assert.True(viewport.TryMapFramebufferPointToLogical(160, 360, out _, out _));
        Assert.True(viewport.TryMapFramebufferPointToLogical(1119, 360, out _, out _));
        Assert.False(viewport.TryMapFramebufferPointToLogical(1120, 360, out _, out _));
        Assert.False(viewport.TryMapFramebufferPointToLogical(1279, 360, out _, out _));
    }

    [Fact]
    public void TryMapToLogical_RejectsPositionsOutsideTheWindow()
    {
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, 1280, 720, PresentationPolicy.Fit);

        Assert.False(viewport.TryMapFramebufferPointToLogical(-1, 360, out _, out _));
        Assert.False(viewport.TryMapFramebufferPointToLogical(640, -1, out _, out _));
        Assert.False(viewport.TryMapFramebufferPointToLogical(int.MaxValue, int.MaxValue, out _, out _));
        Assert.False(viewport.TryMapFramebufferPointToLogical(int.MinValue, int.MinValue, out _, out _));
    }

    [Theory]
    [InlineData(1280, 720, PresentationPolicy.Fit)]
    [InlineData(1920, 1080, PresentationPolicy.Fit)]
    [InlineData(1920, 1080, PresentationPolicy.IntegerScale)]
    [InlineData(2560, 1440, PresentationPolicy.Stretch)]
    [InlineData(1001, 733, PresentationPolicy.Fit)]
    public void TryMapToLogical_StaysInsideTheLogicalFrameForEveryPixelOfTheDestination(int hostWidth, int hostHeight, PresentationPolicy policy)
    {
        // The guarantee input relies on: anything the renderer drew maps back to a real logical
        // pixel, with no clamping and no off-by-one at the far edge.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, hostWidth, hostHeight, policy);

        for (int y = 0; y < viewport.Height; y++)
        {
            for (int x = 0; x < viewport.Width; x++)
            {
                Assert.True(viewport.TryMapFramebufferPointToLogical(viewport.OffsetX + x, viewport.OffsetY + y, out int logicalX, out int logicalY));

                Assert.InRange(logicalX, 0, viewport.LogicalWidth - 1);
                Assert.InRange(logicalY, 0, viewport.LogicalHeight - 1);
            }
        }
    }

    [Fact]
    public void TryMapToLogical_IsMonotonicAcrossTheDestination()
    {
        // Neighbouring host pixels must never map to decreasing logical pixels, or a drag would
        // jitter backwards partway across the window.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, 1920, 1080, PresentationPolicy.Fit);

        int previous = -1;

        for (int x = 0; x < viewport.Width; x++)
        {
            Assert.True(viewport.TryMapFramebufferPointToLogical(viewport.OffsetX + x, viewport.OffsetY, out int logicalX, out _));
            Assert.True(logicalX >= previous, $"logicalX went backwards at host x={x}");

            previous = logicalX;
        }
    }

    [Fact]
    public void TryMapToLogical_ResolvesEveryLogicalColumnWhenMagnified()
    {
        // At a magnifying scale no logical column should be unreachable, or parts of the UI could
        // never be clicked.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical800, 1600, 1200, PresentationPolicy.IntegerScale);

        HashSet<int> reached = [];

        for (int x = 0; x < viewport.Width; x++)
        {
            Assert.True(viewport.TryMapFramebufferPointToLogical(viewport.OffsetX + x, viewport.OffsetY, out int logicalX, out _));
            reached.Add(logicalX);
        }

        Assert.Equal(viewport.LogicalWidth, reached.Count);
    }

    [Theory]
    [InlineData(1280, 720, PresentationPolicy.Fit)]
    [InlineData(1920, 1080, PresentationPolicy.IntegerScale)]
    [InlineData(2560, 1440, PresentationPolicy.Stretch)]
    public void TryMapPointerToLogical_MapsTheTopOfTheWindowToTheTopOfTheLogicalFrame(int hostWidth, int hostHeight, PresentationPolicy policy)
    {
        // The flip is the whole point: a pointer row counted from the top must not resolve to a
        // logical row counted from the bottom.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, hostWidth, hostHeight, policy);

        int topPointerY = hostHeight - 1 - (viewport.OffsetY + viewport.Height - 1);

        Assert.True(viewport.TryMapPointerToLogical(viewport.OffsetX, topPointerY, out int logicalX, out int logicalY));
        Assert.Equal(0, logicalX);

        // One destination row covers 1/ScaleY logical rows, so under minification the topmost
        // destination row starts a row or two into the logical frame. It must still land at the
        // top of it, not the bottom.
        int rowsPerDestinationRow = (int)Math.Ceiling(1d / viewport.ScaleY);

        Assert.InRange(logicalY, 0, rowsPerDestinationRow - 1);
    }

    [Fact]
    public void TryMapPointerToLogical_IsTheVerticalMirrorOfTheFramebufferMapping()
    {
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, 1920, 1080, PresentationPolicy.IntegerScale);

        for (int pointerY = 0; pointerY < viewport.HostHeight; pointerY++)
        {
            bool mappedPointer = viewport.TryMapPointerToLogical(960, pointerY, out _, out int fromPointer);
            bool mappedFramebuffer = viewport.TryMapFramebufferPointToLogical(960, viewport.HostHeight - 1 - pointerY, out _, out int fromFramebuffer);

            Assert.Equal(mappedFramebuffer, mappedPointer);

            if (mappedPointer)
            {
                // Mirrored, not equal: the framebuffer mapping is bottom-up and the pointer
                // mapping is top-down, which is the whole reason they are separate methods.
                Assert.Equal(viewport.LogicalHeight - 1 - fromFramebuffer, fromPointer);
            }
        }
    }

    [Fact]
    public void TryMapPointerToLogical_IncreasesDownwardsAcrossTheWindow()
    {
        // Moving the pointer down the window must move down the logical frame. Getting the flip
        // backwards still maps every position successfully, so only ordering catches it.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, 1920, 1080, PresentationPolicy.Fit);

        int topPointerY = hostTop(viewport);
        int bottomPointerY = viewport.HostHeight - 1 - viewport.OffsetY;

        Assert.True(viewport.TryMapPointerToLogical(960, topPointerY, out _, out int nearTop));
        Assert.True(viewport.TryMapPointerToLogical(960, bottomPointerY, out _, out int nearBottom));

        Assert.True(nearTop < nearBottom, $"top mapped to {nearTop}, bottom mapped to {nearBottom}");

        static int hostTop(PresentationViewport viewport)
        {
            return viewport.HostHeight - 1 - (viewport.OffsetY + viewport.Height - 1);
        }
    }

    [Fact]
    public void TryMapPointerToLogical_RejectsPositionsAboveAndBelowTheLetterboxBars()
    {
        // 1024x768 in a 1024x1200 host letterboxes with 216px bars top and bottom.
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, 1024, 1200, PresentationPolicy.Fit);

        Assert.Equal(768, viewport.Height);
        Assert.Equal(216, viewport.OffsetY);

        Assert.False(viewport.TryMapPointerToLogical(512, 0, out _, out _));
        Assert.False(viewport.TryMapPointerToLogical(512, 215, out _, out _));
        Assert.True(viewport.TryMapPointerToLogical(512, 216, out _, out _));
        Assert.True(viewport.TryMapPointerToLogical(512, 983, out _, out _));
        Assert.False(viewport.TryMapPointerToLogical(512, 984, out _, out _));
        Assert.False(viewport.TryMapPointerToLogical(512, 1199, out _, out _));
    }

    [Fact]
    public void TryMapPointerToLogical_RejectsPositionsOutsideTheWindow()
    {
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, 1280, 720, PresentationPolicy.Fit);

        Assert.False(viewport.TryMapPointerToLogical(640, -1, out _, out _));
        Assert.False(viewport.TryMapPointerToLogical(-1, 360, out _, out _));
        Assert.False(viewport.TryMapPointerToLogical(640, int.MaxValue, out _, out _));
        Assert.False(viewport.TryMapPointerToLogical(640, int.MinValue, out _, out _));
    }

    [Fact]
    public void TryMapPointerToLogical_ReturnsFalseForAnEmptyViewport()
    {
        PresentationViewport viewport = PresentationViewport.Compute(s_logical1024, 0, 0);

        Assert.False(viewport.TryMapPointerToLogical(0, 0, out _, out _));
    }

}
