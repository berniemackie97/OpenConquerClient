using OpenConquer.Rendering.Text.Atlas;
using OpenConquer.Rendering.Text.Glyphs;
using OpenConquer.Rendering.Text.Layout;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class UnicodeTextCoverageTests
{
    [Fact]
    public void UnicodeLayout_DistinguishesMissingGlyphFromWhitespaceWithoutBitmap()
    {
        TestGlyphRasterizer rasterizer = new(character => character.Value switch
        {
            ' ' => (true, new RasterizedGlyph(0, 0, 0, 0, 4, [])),
            'A' => (true, new RasterizedGlyph(2, 3, -1, 4, 5, [255, 255, 255, 255, 255, 255])),
            _ => (false, null),
        });
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        NativeTextLayout complete = engine.LayoutUnicode(" A");
        NativeTextLayout incomplete = engine.LayoutUnicode(" A\u6F22");

        Assert.False(complete.HasMissingGlyphs);
        Assert.True(incomplete.HasMissingGlyphs);
        Assert.Equal(1, incomplete.Items.Length);
        Assert.True(complete.TryGetInkBounds(out int left, out int top, out int right, out int bottom));
        Assert.Equal((3, 4, 5, 7), (left, top, right, bottom));
    }

    [Fact]
    public void InkBounds_IncludeEveryGlyphButNotDataIcons()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutSource source = new(font, new GlyphAtlas());
        NativeTextLayout layout = new(source, 40, 12,
        [
            NativeTextLayoutItem.CreateGlyph(0x41, -3, 2, new GlyphAtlasRegion(0, 0, 0, 3, 4)),
            NativeTextLayoutItem.CreateDataIcon(2, 5, 6, 8),
            NativeTextLayoutItem.CreateGlyph(0x42, 12, -2, new GlyphAtlasRegion(0, 8, 0, 5, 9)),
        ]);

        Assert.True(layout.TryGetInkBounds(out int left, out int top, out int right, out int bottom));
        Assert.Equal((-3, -2, 17, 7), (left, top, right, bottom));
    }

    [Fact]
    public void InkBounds_EmptyOrIconOnlyLayoutHasNoGlyphInk()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutSource source = new(font, new GlyphAtlas());
        NativeTextLayout layout = new(source, 16, 12, [NativeTextLayoutItem.CreateDataIcon(1, 0, 0, 16)]);

        Assert.False(layout.TryGetInkBounds(out int left, out int top, out int right, out int bottom));
        Assert.Equal((0, 0, 0, 0), (left, top, right, bottom));
    }
}
