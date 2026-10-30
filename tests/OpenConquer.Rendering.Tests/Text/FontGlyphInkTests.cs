using System.Text;
using OpenConquer.Rendering.Text.Fonts;
using OpenConquer.Rendering.Text.Glyphs;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class FontGlyphInkTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(12, true)]
    public void MonochromeGlyph_RequiresMoreThanFiveLitPixels(int lit, bool expected)
    {
        byte[] pixels = new byte[16];
        Array.Fill(pixels, (byte)255, 0, lit);
        using TestGlyphRasterizer rasterizer = new(_ =>
            (true, new RasterizedGlyph(16, 1, 0, 0, 16, pixels)), antialiasEnabled: false);
        using FontGlyphInk ink = new(rasterizer);

        Assert.Equal(expected, ink.HasMoreThanFivePixels(new Rune('a')));
        Assert.Equal([new Rune('a')], rasterizer.Characters);
    }

    [Fact]
    public void MissingGlyph_IsRejected()
    {
        using TestGlyphRasterizer rasterizer = new(_ => (false, null), antialiasEnabled: false);
        using FontGlyphInk ink = new(rasterizer);

        Assert.False(ink.HasMoreThanFivePixels(new Rune('a')));
    }
}
