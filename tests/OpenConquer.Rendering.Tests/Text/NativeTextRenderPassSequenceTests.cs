using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class NativeTextRenderPassSequenceTests
{
    private static readonly SpriteColor s_textColor = new(10, 20, 30, 40);
    private static readonly SpriteColor s_cornerColor = new(50, 60, 70, 80);

    [Fact]
    public void Constructor_NullFontThrows()
    {
        NativeTextRenderOptions options = CreateOptions(NativeTextRenderStyle.Normal);

        Assert.Throws<ArgumentNullException>(() => new NativeTextRenderPassSequence(options, null!));
    }

    [Fact]
    public void Normal_EmitsOnlyBasePass()
    {
        NativeTextRenderPassSequence sequence = CreateSequence(NativeTextRenderStyle.Normal, antialiasEnabled: true);

        NativeTextRenderPass[] passes = Collect(sequence);

        Assert.Equal(1, sequence.Count);
        NativeTextRenderPass pass = Assert.Single(passes);
        Assert.Equal(new NativeTextRenderPass(0, 0, NativeTextVertexColors.Solid(s_textColor)), pass);
    }

    [Fact]
    public void ShadowOffset_EmitsCornerPassBeforeBase()
    {
        NativeTextRenderPassSequence sequence = CreateSequence(NativeTextRenderStyle.ShadowOffset, antialiasEnabled: true, offsetXPixels: 3, offsetYPixels: -2);

        NativeTextRenderPass[] passes = Collect(sequence);

        Assert.Equal(2, sequence.Count);
        Assert.Equal(new NativeTextRenderPass(3, -2, NativeTextVertexColors.Solid(s_cornerColor)), passes[0]);
        Assert.Equal(new NativeTextRenderPass(0, 0, NativeTextVertexColors.Solid(s_textColor)), passes[1]);
    }

    [Fact]
    public void MultiOffsetOutline_UsesVerifiedNativeOrderThenBase()
    {
        NativeTextRenderPassSequence sequence = CreateSequence(NativeTextRenderStyle.MultiOffsetOutline, antialiasEnabled: true, offsetXPixels: 99, offsetYPixels: 99);

        NativeTextRenderPass[] passes = Collect(sequence);

        Assert.Equal(9, sequence.Count);

        (int X, int Y)[] expectedOffsets =
        [
            (-1, 0),
            (1, 0),
            (0, -1),
            (0, 1),
            (-1, -1),
            (1, -1),
            (-1, 1),
            (1, 1),
            (0, 0),
        ];

        for (int index = 0; index < expectedOffsets.Length; index++)
        {
            Assert.Equal(expectedOffsets[index], (passes[index].OffsetXPixels, passes[index].OffsetYPixels));
        }

        for (int index = 0; index < passes.Length - 1; index++)
        {
            Assert.Equal(NativeTextVertexColors.Solid(s_cornerColor), passes[index].Colors);
        }

        Assert.Equal(NativeTextVertexColors.Solid(s_textColor), passes[^1].Colors);
    }

    [Fact]
    public void OffsetTrail_UsesVerifiedPositiveWalkThenBase()
    {
        NativeTextRenderPassSequence sequence = CreateSequence(NativeTextRenderStyle.OffsetTrail, antialiasEnabled: true, offsetXPixels: 3, offsetYPixels: 1);

        NativeTextRenderPass[] passes = Collect(sequence);

        (int X, int Y)[] expectedOffsets =
        [
            (3, 1),
            (2, 1),
            (2, 0),
            (1, 0),
            (0, 0),
        ];

        Assert.Equal(expectedOffsets.Length, sequence.Count);

        for (int index = 0; index < expectedOffsets.Length; index++)
        {
            Assert.Equal(expectedOffsets[index], (passes[index].OffsetXPixels, passes[index].OffsetYPixels));
        }

        for (int index = 0; index < passes.Length - 1; index++)
        {
            Assert.Equal(NativeTextVertexColors.Solid(s_cornerColor), passes[index].Colors);
        }

        Assert.Equal(NativeTextVertexColors.Solid(s_textColor), passes[^1].Colors);
    }

    [Fact]
    public void OffsetTrail_PreservesNegativeDirection()
    {
        NativeTextRenderPassSequence sequence = CreateSequence(NativeTextRenderStyle.OffsetTrail, antialiasEnabled: true, offsetXPixels: -2, offsetYPixels: -1);

        NativeTextRenderPass[] passes = Collect(sequence);

        (int X, int Y)[] expectedOffsets =
        [
            (-2, -1),
            (-1, -1),
            (-1, 0),
            (0, 0),
        ];

        Assert.Equal(expectedOffsets.Length, sequence.Count);

        for (int index = 0; index < expectedOffsets.Length; index++)
        {
            Assert.Equal(expectedOffsets[index], (passes[index].OffsetXPixels, passes[index].OffsetYPixels));
        }
    }

    [Fact]
    public void OffsetTrail_ZeroOffsetEmitsOnlyBasePass()
    {
        NativeTextRenderPassSequence sequence = CreateSequence(NativeTextRenderStyle.OffsetTrail, antialiasEnabled: true);

        NativeTextRenderPass pass = Assert.Single(Collect(sequence));

        Assert.Equal(1, sequence.Count);
        Assert.Equal(new NativeTextRenderPass(0, 0, NativeTextVertexColors.Solid(s_textColor)), pass);
    }

    [Fact]
    public void AntialiasDisabled_ForcesTextAndCornerAlphaOpaque()
    {
        NativeTextRenderPassSequence sequence = CreateSequence(NativeTextRenderStyle.ShadowOffset, antialiasEnabled: false, offsetXPixels: 2, offsetYPixels: 3);

        NativeTextRenderPass[] passes = Collect(sequence);

        SpriteColor expectedCornerColor = new(s_cornerColor.Red, s_cornerColor.Green, s_cornerColor.Blue, byte.MaxValue);
        SpriteColor expectedTextColor = new(s_textColor.Red, s_textColor.Green, s_textColor.Blue, byte.MaxValue);

        Assert.Equal(NativeTextVertexColors.Solid(expectedCornerColor), passes[0].Colors);
        Assert.Equal(NativeTextVertexColors.Solid(expectedTextColor), passes[1].Colors);
    }

    [Fact]
    public void AntialiasDisabled_DoesNotModifyPerCornerColors()
    {
        NativeTextVertexColors perCornerColors = CreatePerCornerColors();
        NativeTextRenderOptions options = new(NativeTextRenderStyle.PerCornerColor, s_textColor, s_cornerColor, 4, 5, perCornerColors);
        NativeTextFontRecord font = CreateFont(antialiasEnabled: false);

        NativeTextRenderPassSequence sequence = new(options, font);
        NativeTextRenderPass pass = Assert.Single(Collect(sequence));

        Assert.Equal(perCornerColors, pass.Colors);
    }

    [Fact]
    public void AntialiasEnabled_PreservesConfiguredAlpha()
    {
        NativeTextRenderPassSequence sequence = CreateSequence(NativeTextRenderStyle.ShadowOffset, antialiasEnabled: true, offsetXPixels: 2, offsetYPixels: 3);

        NativeTextRenderPass[] passes = Collect(sequence);

        Assert.Equal(NativeTextVertexColors.Solid(s_cornerColor), passes[0].Colors);
        Assert.Equal(NativeTextVertexColors.Solid(s_textColor), passes[1].Colors);
    }

    [Fact]
    public void OffsetTrail_PassCountOverflowThrowsBeforeEnumeration()
    {
        NativeTextRenderOptions horizontalOverflow = CreateOptions(NativeTextRenderStyle.OffsetTrail, int.MinValue, 0);
        NativeTextRenderOptions verticalOverflow = CreateOptions(NativeTextRenderStyle.OffsetTrail, 0, int.MinValue);
        NativeTextFontRecord font = CreateFont(antialiasEnabled: true);

        Assert.Throws<OverflowException>(() => new NativeTextRenderPassSequence(horizontalOverflow, font));
        Assert.Throws<OverflowException>(() => new NativeTextRenderPassSequence(verticalOverflow, font));
    }

    [Fact]
    public void Enumeration_IsRepeatableAndDeterministic()
    {
        NativeTextRenderPassSequence sequence = CreateSequence(NativeTextRenderStyle.OffsetTrail, antialiasEnabled: true, offsetXPixels: 3, offsetYPixels: 1);

        NativeTextRenderPass[] first = Collect(sequence);
        NativeTextRenderPass[] second = Collect(sequence);

        Assert.Equal(first, second);
    }

    private static NativeTextRenderPassSequence CreateSequence(NativeTextRenderStyle style, bool antialiasEnabled, int offsetXPixels = 0, int offsetYPixels = 0)
    {
        return new NativeTextRenderPassSequence(CreateOptions(style, offsetXPixels, offsetYPixels), CreateFont(antialiasEnabled));
    }

    private static NativeTextRenderOptions CreateOptions(NativeTextRenderStyle style, int offsetXPixels = 0, int offsetYPixels = 0)
    {
        return new NativeTextRenderOptions(style, s_textColor, s_cornerColor, offsetXPixels, offsetYPixels, CreatePerCornerColors());
    }

    private static NativeTextFontRecord CreateFont(bool antialiasEnabled)
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null), antialiasEnabled);
        return new NativeTextFontRecord(0, 12, 12, rasterizer);
    }

    private static NativeTextVertexColors CreatePerCornerColors()
    {
        return new NativeTextVertexColors(new SpriteColor(1, 2, 3, 4), new SpriteColor(5, 6, 7, 8), new SpriteColor(9, 10, 11, 12), new SpriteColor(13, 14, 15, 16));
    }

    private static NativeTextRenderPass[] Collect(NativeTextRenderPassSequence sequence)
    {
        NativeTextRenderPass[] passes = new NativeTextRenderPass[sequence.Count];
        NativeTextRenderPassSequence.Enumerator enumerator = sequence.GetEnumerator();
        int index = 0;

        while (enumerator.MoveNext())
        {
            passes[index++] = enumerator.Current;
        }

        Assert.Equal(passes.Length, index);
        return passes;
    }
}
