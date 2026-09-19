using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class NativeTextRenderOptionsTests
{
    [Fact]
    public void Constructor_PreservesConfiguredRenderState()
    {
        SpriteColor textColor = new(10, 20, 30, 40);
        SpriteColor cornerColor = new(50, 60, 70, 80);
        NativeTextVertexColors perCornerColors = new(
            new SpriteColor(1, 2, 3, 4),
            new SpriteColor(5, 6, 7, 8),
            new SpriteColor(9, 10, 11, 12),
            new SpriteColor(13, 14, 15, 16));

        NativeTextRenderOptions options = new(NativeTextRenderStyle.OffsetTrail, textColor, cornerColor, -7, 9, perCornerColors);

        Assert.Equal(NativeTextRenderStyle.OffsetTrail, options.Style);
        Assert.Equal(textColor, options.TextColor);
        Assert.Equal(cornerColor, options.CornerColor);
        Assert.Equal(-7, options.CornerOffsetXPixels);
        Assert.Equal(9, options.CornerOffsetYPixels);
        Assert.Equal(perCornerColors, options.PerCornerColors);
    }

    [Fact]
    public void Constructor_UnknownStyleThrows()
    {
        NativeTextRenderStyle invalidStyle = (NativeTextRenderStyle)int.MaxValue;

        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeTextRenderOptions(invalidStyle, SpriteColor.White, SpriteColor.White, 0, 0, NativeTextVertexColors.Solid(SpriteColor.White)));
    }

    [Fact]
    public void Constructor_DoesNotImposeArbitraryOffsetLimits()
    {
        NativeTextRenderOptions options = new(NativeTextRenderStyle.ShadowOffset, SpriteColor.White, SpriteColor.White, int.MinValue, int.MaxValue, NativeTextVertexColors.Solid(SpriteColor.White));

        Assert.Equal(int.MinValue, options.CornerOffsetXPixels);
        Assert.Equal(int.MaxValue, options.CornerOffsetYPixels);
    }
}
