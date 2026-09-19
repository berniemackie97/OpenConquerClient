using System.Text;
using OpenConquer.Rendering.Text.Atlas;
using OpenConquer.Rendering.Text.Glyphs;
using OpenConquer.Rendering.Text.Layout;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class NativeTextLayoutEngineTests
{
    [Fact]
    public void Layout_GlyphsUseMetricsAndAdvanceForMeasurement()
    {
        TestGlyphRasterizer rasterizer = new(character => character.Value switch
        {
            'A' => (true, CreateGlyph(2, 3, bearingLeftPixels: 1, topOffsetPixels: 2, advancePixels: 5)),
            'B' => (true, CreateGlyph(1, 2, bearingLeftPixels: 0, topOffsetPixels: 1, advancePixels: 4)),
            _ => (false, null),
        });
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("AB"), recognizeDataIcons: false);
        ReadOnlySpan<NativeTextLayoutItem> items = layout.Items.Span;

        Assert.Equal(9, layout.WidthPixels);
        Assert.Equal(12, layout.HeightPixels);
        Assert.Equal(2, items.Length);
        Assert.Equal(NativeTextLayoutItemKind.Glyph, items[0].Kind);
        Assert.Equal((ushort)'A', items[0].GlyphKey);
        Assert.Equal(1, items[0].XPixels);
        Assert.Equal(2, items[0].YPixels);
        Assert.Equal(NativeTextLayoutItemKind.Glyph, items[1].Kind);
        Assert.Equal((ushort)'B', items[1].GlyphKey);
        Assert.Equal(5, items[1].XPixels);
        Assert.Equal(1, items[1].YPixels);
    }

    [Fact]
    public void Layout_NewLineResetsXAndAdvancesYByNativeLineStep()
    {
        TestGlyphRasterizer rasterizer = new(character => character.Value switch
        {
            'A' => (true, CreateGlyph(1, 1, 0, 0, 5)),
            'B' => (true, CreateGlyph(1, 1, 0, 1, 4)),
            _ => (false, null),
        });
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("A\nB"), recognizeDataIcons: false);
        ReadOnlySpan<NativeTextLayoutItem> items = layout.Items.Span;

        Assert.Equal(5, layout.WidthPixels);
        Assert.Equal(27, layout.HeightPixels);
        Assert.Equal(2, items.Length);
        Assert.Equal(0, items[1].XPixels);
        Assert.Equal(16, items[1].YPixels);
    }

    [Fact]
    public void Layout_UsesWidestLineForMeasuredWidth()
    {
        TestGlyphRasterizer rasterizer = new(_ => (true, CreateGlyph(1, 1, 0, 0, 5)));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("AAA\nA"), recognizeDataIcons: false);

        Assert.Equal(15, layout.WidthPixels);
        Assert.Equal(27, layout.HeightPixels);
    }

    [Fact]
    public void Layout_EmptyTextHasNominalHeight()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        NativeTextLayout layout = engine.Layout([], recognizeDataIcons: false);

        Assert.Equal(0, layout.WidthPixels);
        Assert.Equal(12, layout.HeightPixels);
        Assert.True(layout.Items.IsEmpty);
        Assert.Equal(0, rasterizer.CallCount);
    }

    [Fact]
    public void Layout_SpaceAdvancesWithoutDrawableItemOrAtlasAllocation()
    {
        TestGlyphRasterizer rasterizer = new(character => character.Value == ' ' ? (true, new RasterizedGlyph(0, 0, 0, 0, 4, [])) : (false, null));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        NativeTextLayout layout = engine.Layout([(byte)' '], recognizeDataIcons: false);

        Assert.Equal(4, layout.WidthPixels);
        Assert.Equal(12, layout.HeightPixels);
        Assert.True(layout.Items.IsEmpty);
        Assert.Empty(engine.Atlas.Pages);
    }

    [Fact]
    public void Layout_MissingGlyphAdvancesByPrimaryLineHeightWithoutDrawableItem()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, 7, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        NativeTextLayout layout = engine.Layout([(byte)'A'], recognizeDataIcons: false);

        Assert.Equal(7, layout.WidthPixels);
        Assert.Equal(12, layout.HeightPixels);
        Assert.True(layout.Items.IsEmpty);
    }

    [Fact]
    public void Layout_RepeatedGlyphUsesCachedRasterizationAndAtlasRegion()
    {
        TestGlyphRasterizer rasterizer = new(_ => (true, CreateGlyph(2, 2, 0, 0, 5)));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("AA"), recognizeDataIcons: false);
        ReadOnlySpan<NativeTextLayoutItem> items = layout.Items.Span;

        Assert.Equal(1, rasterizer.CallCount);
        Assert.Equal(2, items.Length);
        Assert.Equal(items[0].AtlasRegion, items[1].AtlasRegion);
        Assert.Equal(0, items[0].XPixels);
        Assert.Equal(5, items[1].XPixels);
        Assert.Single(engine.Atlas.Pages);
    }

    [Fact]
    public void Layout_DataIconUsesProviderWidth()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        TestDataIconWidthProvider provider = new(_ => (true, 9));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936, provider);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("#07"), recognizeDataIcons: true);
        NativeTextLayoutItem item = Assert.Single(layout.Items.ToArray());

        Assert.Equal(9, layout.WidthPixels);
        Assert.Equal(12, layout.HeightPixels);
        Assert.Equal(NativeTextLayoutItemKind.DataIcon, item.Kind);
        Assert.Equal(7, item.DataIconIndex);
        Assert.Equal(9, item.DataIconWidthPixels);
        Assert.Equal(0, item.XPixels);
        Assert.Equal(0, item.YPixels);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(0, rasterizer.CallCount);
    }

    [Fact]
    public void Layout_ExplicitDataIconWidthOverridesProvider()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        TestDataIconWidthProvider provider = new(_ => (true, 9));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936, provider);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("#07"), recognizeDataIcons: true, dataIconWidthPixels: 11);
        NativeTextLayoutItem item = Assert.Single(layout.Items.ToArray());

        Assert.Equal(11, layout.WidthPixels);
        Assert.Equal(11, item.DataIconWidthPixels);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public void Layout_MissingDataIconWidthUsesSixteenPixelFallback()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        TestDataIconWidthProvider provider = new(_ => (false, 0));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936, provider);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("#42"), recognizeDataIcons: true);
        NativeTextLayoutItem item = Assert.Single(layout.Items.ToArray());

        Assert.Equal(16, layout.WidthPixels);
        Assert.Equal(16, item.DataIconWidthPixels);
    }

    [Fact]
    public void Layout_ZeroDataIconWidthUsesSixteenPixelFallback()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        TestDataIconWidthProvider provider = new(_ => (true, 0));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936, provider);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("#42"), recognizeDataIcons: true);

        Assert.Equal(16, layout.WidthPixels);
        Assert.Equal(16, Assert.Single(layout.Items.ToArray()).DataIconWidthPixels);
    }

    [Fact]
    public void Layout_NoDataIconProviderUsesSixteenPixelFallback()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("#42"), recognizeDataIcons: true);

        Assert.Equal(16, layout.WidthPixels);
        Assert.Equal(16, Assert.Single(layout.Items.ToArray()).DataIconWidthPixels);
    }

    [Fact]
    public void Layout_DataIconRecognitionDisabledTreatsSequenceAsGlyphs()
    {
        TestGlyphRasterizer rasterizer = new(_ => (true, CreateGlyph(1, 1, 0, 0, 1)));
        TestDataIconWidthProvider provider = new(_ => (true, 9));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936, provider);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("#07"), recognizeDataIcons: false);

        Assert.Equal(3, layout.WidthPixels);
        Assert.Equal(3, layout.Items.Length);
        Assert.Equal(3, rasterizer.CallCount);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public void Layout_NegativeExplicitDataIconWidthIsPreserved()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        TestDataIconWidthProvider provider = new(_ => throw new InvalidOperationException("Provider must not run."));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936, provider);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("#07"), recognizeDataIcons: true, dataIconWidthPixels: -3);
        NativeTextLayoutItem item = Assert.Single(layout.Items.ToArray());

        Assert.Equal(0, layout.WidthPixels);
        Assert.Equal(-3, item.DataIconWidthPixels);
        Assert.Equal(0, item.XPixels);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public void Layout_NegativeProviderDataIconWidthIsPreserved()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        TestDataIconWidthProvider provider = new(_ => (true, -5));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936, provider);

        NativeTextLayout layout = engine.Layout(Encoding.ASCII.GetBytes("#07"), recognizeDataIcons: true);
        NativeTextLayoutItem item = Assert.Single(layout.Items.ToArray());

        Assert.Equal(0, layout.WidthPixels);
        Assert.Equal(-5, item.DataIconWidthPixels);
        Assert.Equal(0, item.XPixels);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void LayoutItem_ZeroDataIconWidthThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeTextLayoutItem.CreateDataIcon(7, 0, 0, 0));
    }

    [Fact]
    public void Layout_AdvanceOverflowThrows()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, int.MaxValue, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        Assert.Throws<OverflowException>(() => engine.Layout(Encoding.ASCII.GetBytes("AB"), recognizeDataIcons: false));
    }

    [Fact]
    public void Constructor_LineAdvanceOverflowThrows()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, int.MaxValue, 12, rasterizer);

        Assert.Throws<OverflowException>(() => new NativeTextLayoutEngine(font, font, 936));
    }

    [Fact]
    public void LayoutItem_WrongSemanticAccessThrows()
    {
        GlyphAtlasRegion region = new(0, 0, 0, 1, 1);
        NativeTextLayoutItem glyph = NativeTextLayoutItem.CreateGlyph(0x0041, 0, 0, region);
        NativeTextLayoutItem icon = NativeTextLayoutItem.CreateDataIcon(7, 0, 0, 16);

        Assert.Throws<InvalidOperationException>(() => _ = glyph.DataIconIndex);
        Assert.Throws<InvalidOperationException>(() => _ = glyph.DataIconWidthPixels);
        Assert.Throws<InvalidOperationException>(() => _ = icon.GlyphKey);
        Assert.Throws<InvalidOperationException>(() => _ = icon.AtlasRegion);
    }

    private static RasterizedGlyph CreateGlyph(int widthPixels, int heightPixels, int bearingLeftPixels, int topOffsetPixels, int advancePixels)
    {
        return new RasterizedGlyph(widthPixels, heightPixels, bearingLeftPixels, topOffsetPixels, advancePixels, new byte[checked(widthPixels * heightPixels)]);
    }

    private sealed class TestDataIconWidthProvider : IDataIconWidthProvider
    {
        private readonly Func<byte, (bool Found, int WidthPixels)> _handler;

        public TestDataIconWidthProvider(Func<byte, (bool Found, int WidthPixels)> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handler = handler;
        }

        public int CallCount
        {
            get; private set;
        }

        public bool TryGetWidth(byte dataIconIndex, out int widthPixels)
        {
            CallCount++;
            (bool found, int width) = _handler(dataIconIndex);
            widthPixels = width;
            return found;
        }
    }
}
