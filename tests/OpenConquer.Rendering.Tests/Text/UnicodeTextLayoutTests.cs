using System.Text;
using OpenConquer.Rendering.Text.Glyphs;
using OpenConquer.Rendering.Text.Layout;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class UnicodeTextLayoutTests
{
    [Fact]
    public void LayoutUnicode_PreservesUnicodeScalarsAndReusesGlyphs()
    {
        TestGlyphRasterizer rasterizer = new(_ => (true, Glyph(5)));
        NativeTextLayoutEngine engine = Engine(rasterizer);
        string text = "A\u6F22\U0001F600";

        NativeTextLayout first = engine.LayoutUnicode(text);
        NativeTextLayout second = engine.LayoutUnicode(text);
        ReadOnlySpan<NativeTextLayoutItem> items = first.Items.Span;

        Assert.Equal(15, first.WidthPixels);
        Assert.Equal(12, first.HeightPixels);
        Assert.Equal(3, items.Length);
        Assert.Equal(new Rune('A'), items[0].UnicodeRune);
        Assert.Equal(new Rune(0x6F22), items[1].UnicodeRune);
        Assert.Equal(new Rune(0x1F600), items[2].UnicodeRune);
        Assert.Equal(0, items[0].XPixels);
        Assert.Equal(5, items[1].XPixels);
        Assert.Equal(10, items[2].XPixels);
        Assert.Equal(3, rasterizer.CallCount);
        Assert.Equal(items[0].AtlasRegion, second.Items.Span[0].AtlasRegion);
        Assert.Same(first.Source, second.Source);
        Assert.Single(engine.Atlas.Pages);
    }

    [Fact]
    public void LayoutUnicode_IgnoresLegacyEncodingAndDataIconParsing()
    {
        TestGlyphRasterizer rasterizer = new(_ => (true, Glyph(3)));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, effectiveCodePage: 0);

        NativeTextLayout text = engine.LayoutUnicode("#07\n\u6F22");

        Assert.Equal(9, text.WidthPixels);
        Assert.Equal(27, text.HeightPixels);
        Assert.Equal(4, text.Items.Length);
        Assert.Equal(new Rune('#'), text.Items.Span[0].UnicodeRune);
        Assert.Equal(new Rune('0'), text.Items.Span[1].UnicodeRune);
        Assert.Equal(new Rune('7'), text.Items.Span[2].UnicodeRune);
        Assert.Equal(new Rune(0x6F22), text.Items.Span[3].UnicodeRune);
        Assert.Equal(15, text.Items.Span[3].YPixels);
        Assert.Equal(4, rasterizer.CallCount);
        Assert.Empty(engine.Layout([(byte)'A'], recognizeDataIcons: false).Items.ToArray());
    }

    [Theory]
    [InlineData(0xD800, false)]
    [InlineData(0xDC00, false)]
    [InlineData(0xD800, true)]
    [InlineData(0, true)]
    public void LayoutUnicode_RejectsMalformedTextBeforeRasterization(int invalidCodeUnit, bool embedded)
    {
        char invalid = checked((char)invalidCodeUnit);
        string text = embedded ? new string(new[] { 'A', invalid, 'B' }) : new string(invalid, 1);
        TestGlyphRasterizer rasterizer = new(_ => (true, Glyph(3)));
        NativeTextLayoutEngine engine = Engine(rasterizer);

        Assert.Throws<ArgumentException>(() => engine.LayoutUnicode(text));
        Assert.Equal(0, rasterizer.CallCount);
        Assert.Empty(engine.Atlas.Pages);
    }

    [Fact]
    public void LayoutUnicode_RejectsExcessiveTextWithoutRasterization()
    {
        TestGlyphRasterizer rasterizer = new(_ => (true, Glyph(3)));
        NativeTextLayoutEngine engine = Engine(rasterizer);

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.LayoutUnicode(new string('A', 4097)));
        Assert.Equal(0, rasterizer.CallCount);
    }

    [Fact]
    public void LayoutUnicode_PreservesEncodedGlyphIdentityAndDataIconSemantics()
    {
        TestGlyphRasterizer rasterizer = new(_ => (true, Glyph(5)));
        NativeTextLayoutEngine engine = Engine(rasterizer);

        NativeTextLayout encoded = engine.Layout(Encoding.ASCII.GetBytes("A#07"), recognizeDataIcons: true, dataIconWidthPixels: 7);
        NativeTextLayout unicode = engine.LayoutUnicode("A#07");

        Assert.Equal(12, encoded.WidthPixels);
        Assert.Equal(20, unicode.WidthPixels);
        Assert.Equal((ushort)'A', encoded.Items.Span[0].GlyphKey);
        Assert.Equal(NativeTextLayoutItemKind.DataIcon, encoded.Items.Span[1].Kind);
        NativeTextLayoutItem encodedGlyph = encoded.Items.Span[0];
        NativeTextLayoutItem unicodeGlyph = unicode.Items.Span[0];
        Assert.Equal(new Rune('A'), unicodeGlyph.UnicodeRune);
        Assert.Throws<InvalidOperationException>(() => _ = encodedGlyph.UnicodeRune);
        Assert.Throws<InvalidOperationException>(() => _ = unicodeGlyph.GlyphKey);
    }

    [Fact]
    public void UnicodeGlyphCache_RejectsExhaustionWithoutAllocatingAnotherGlyph()
    {
        TestGlyphRasterizer rasterizer = new(_ => (true, Glyph(1)));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeGlyphCache cache = new(font, font, 936);

        for (int scalar = 0x10000; scalar < 0x11000; scalar++)
        {
            cache.GetOrAdd(new Rune(scalar));
        }

        Assert.Equal(4096, rasterizer.CallCount);
        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(new Rune(0x11000)));
        Assert.Equal(4096, rasterizer.CallCount);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void LayoutUnicode_MissingGlyphDoesNotFabricateBitmap()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextLayoutEngine engine = Engine(rasterizer);

        NativeTextLayout layout = engine.LayoutUnicode("\u6F22");

        Assert.Equal(12, layout.WidthPixels);
        Assert.True(layout.Items.IsEmpty);
        Assert.Empty(engine.Atlas.Pages);
    }

    private static NativeTextLayoutEngine Engine(TestGlyphRasterizer rasterizer)
    {
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        return new NativeTextLayoutEngine(font, font, effectiveCodePage: 936);
    }

    private static RasterizedGlyph Glyph(int advance) => new(1, 1, 0, 0, advance, [255]);
}
