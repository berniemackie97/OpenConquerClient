using OpenConquer.Rendering.Text.Atlas;
using OpenConquer.Rendering.Text.Layout;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class NativeTextLayoutProvenanceTests
{
    [Fact]
    public void Source_PreservesExactPrimaryFontAndAtlasIdentity()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        GlyphAtlas atlas = new();

        NativeTextLayoutSource source = new(font, atlas);

        Assert.Same(font, source.PrimaryFont);
        Assert.Same(atlas, source.Atlas);
    }

    [Fact]
    public void Layout_PreservesExactSourceIdentity()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutSource source = new(font, new GlyphAtlas());

        NativeTextLayout layout = new(source, 0, 12, []);

        Assert.Same(source, layout.Source);
    }

    [Fact]
    public void LayoutEngine_ProducedLayoutUsesEngineSource()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        NativeTextLayout layout = engine.Layout([], recognizeDataIcons: false);

        Assert.Same(engine.Source, layout.Source);
        Assert.Same(engine.Atlas, layout.Source.Atlas);
        Assert.Same(font, layout.Source.PrimaryFont);
    }

    [Fact]
    public void LayoutEngine_ReusesStableSourceAcrossLayouts()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeTextLayoutEngine engine = new(font, font, 936);

        NativeTextLayout first = engine.Layout([], recognizeDataIcons: false);
        NativeTextLayout second = engine.Layout([], recognizeDataIcons: false);

        Assert.Same(engine.Source, first.Source);
        Assert.Same(first.Source, second.Source);
    }

    [Fact]
    public void DistinctLayoutEnginesHaveDistinctSourceIdentity()
    {
        TestGlyphRasterizer firstRasterizer = new(_ => (false, null));
        TestGlyphRasterizer secondRasterizer = new(_ => (false, null));
        NativeTextFontRecord firstFont = new(0, 12, 12, firstRasterizer);
        NativeTextFontRecord secondFont = new(0, 12, 12, secondRasterizer);
        NativeTextLayoutEngine firstEngine = new(firstFont, firstFont, 936);
        NativeTextLayoutEngine secondEngine = new(secondFont, secondFont, 936);

        Assert.NotSame(firstEngine.Source, secondEngine.Source);
        Assert.NotSame(firstEngine.Atlas, secondEngine.Atlas);
    }

    [Fact]
    public void LayoutConstructor_NullSourceThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new NativeTextLayout(null!, 0, 12, []));
    }
}
