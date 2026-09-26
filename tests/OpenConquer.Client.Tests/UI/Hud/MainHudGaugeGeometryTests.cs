using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudGaugeGeometryTests
{
    [Fact]
    public void CreateStyle0_NormalSingleFill_UsesFrameZeroAndBottomUpCrop()
    {
        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(4, 513, 36, 74, 86, 100, 50, 60, 0);

        Assert.Equal(1, sequence.Count);
        AssertDraw(sequence.First, 0, new SpriteSourceBounds(0, 37, 36, 74), 4, 550, 36, 37);
    }

    [Fact]
    public void CreateStyle0_NormalDualFill_DrawsCurrentThenBiasedFollower()
    {
        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(4, 513, 36, 74, 86, 100, 80, 50, 0);

        Assert.Equal(2, sequence.Count);
        AssertDraw(sequence.First, 1, new SpriteSourceBounds(0, 15, 36, 74), 4, 528, 36, 59);
        AssertDraw(sequence.Second, 0, new SpriteSourceBounds(0, 37, 36, 74), 4, 550, 36, 38);
    }

    [Fact]
    public void CreateStyle0_AlternateSingleFill_UsesFrameTwoAndAlternateWidth()
    {
        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(4, 513, 36, 74, 86, 100, 50, 60, 1);

        Assert.Equal(1, sequence.Count);
        AssertDraw(sequence.First, 2, new SpriteSourceBounds(0, 37, 86, 74), 4, 550, 86, 37);
    }

    [Fact]
    public void CreateStyle0_AlternateDualValue_DrawsOnlyFollowerWithoutBias()
    {
        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(4, 513, 36, 74, 86, 100, 80, 51, 1);

        Assert.Equal(1, sequence.Count);
        AssertDraw(sequence.First, 2, new SpriteSourceBounds(0, 37, 86, 74), 4, 550, 86, 38);
    }

    [Fact]
    public void CreateStyle0_ZeroSingleFill_ProducesNoDraw()
    {
        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(4, 513, 36, 74, 86, 100, 0, 0, 0);

        Assert.Equal(0, sequence.Count);
    }

    [Fact]
    public void CreateStyle0_Stamina_UsesVerifiedGeometry()
    {
        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(42, 517, 8, 70, 8, 100, 75, 75, 0);

        Assert.Equal(1, sequence.Count);
        AssertDraw(sequence.First, 0, new SpriteSourceBounds(0, 18, 8, 70), 42, 535, 8, 52);
    }

    [Fact]
    public void CreateStyle0_OverflowHalfFill_PreservesSourceBottomBeyondTextureHeight()
    {
        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(42, 511, 8, 35, 8, 50, 25, 25, 0);

        Assert.Equal(1, sequence.Count);
        AssertDraw(sequence.First, 0, new SpriteSourceBounds(0, 18, 8, 35), 42, 529, 8, 17);
    }

    [Fact]
    public void CreateStyle0_OverflowFullFill_UsesCanonicalFloat32Endpoint()
    {
        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(42, 511, 8, 35, 8, 50, 50, 50, 0);

        Assert.Equal(1, sequence.Count);
        AssertDraw(sequence.First, 0, new SpriteSourceBounds(0, 0, 8, 35), 42, 511, 8, 35);
    }

    [Fact]
    public void CreateStyle0_PositiveValueTruncatingToZero_PreservesNativeDestinationSentinel()
    {
        MainHudGaugeDrawSequence sequence = MainHudGaugeGeometry.CreateStyle0(42, 511, 8, 35, 8, 50, 1, 1, 0);

        Assert.Equal(1, sequence.Count);
        AssertDraw(sequence.First, 0, new SpriteSourceBounds(0, 35, 8, 35), 42, 546, 8, 0);
        Assert.Equal(32, sequence.First.ResolveDestinationHeight(32));
    }

    [Fact]
    public void ResolveDestinationHeight_PreservesPositiveComputedHeight()
    {
        MainHudGaugeDraw draw = new(0, new SpriteSourceBounds(0, 18, 8, 35), 42, 529, 8, 17);

        Assert.Equal(17, draw.ResolveDestinationHeight(32));
    }

    [Fact]
    public void CreateStyle0_NonPositiveMaximum_ProducesNoDraw()
    {
        Assert.Equal(0, MainHudGaugeGeometry.CreateStyle0(4, 513, 36, 74, 86, 0, 50, 50, 0).Count);
        Assert.Equal(0, MainHudGaugeGeometry.CreateStyle0(4, 513, 36, 74, 86, -1, 50, 50, 0).Count);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void CreateStyle0_RejectsNegativeGaugeValues(int value, int follower)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudGaugeGeometry.CreateStyle0(4, 513, 36, 74, 86, 100, value, follower, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void CreateStyle0_RejectsUnsupportedSubvariants(int subVariant)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudGaugeGeometry.CreateStyle0(4, 513, 36, 74, 86, 100, 50, 50, subVariant));
    }

    private static void AssertDraw(MainHudGaugeDraw draw, int frameIndex, SpriteSourceBounds sourceBounds, int x, int y, int width, int height)
    {
        Assert.Equal(frameIndex, draw.FrameIndex);
        Assert.Equal(sourceBounds, draw.SourceBounds);
        Assert.Equal(x, draw.X);
        Assert.Equal(y, draw.Y);
        Assert.Equal(width, draw.Width);
        Assert.Equal(height, draw.Height);
    }
}
