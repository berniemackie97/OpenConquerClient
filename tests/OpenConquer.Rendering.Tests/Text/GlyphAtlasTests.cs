using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class GlyphAtlasTests
{
    [Fact]
    public void Add_FirstGlyphStartsAtOrigin()
    {
        GlyphAtlas atlas = new();

        GlyphAtlasRegion? region = atlas.Add(CreateGlyph(4, 3));

        Assert.Equal(new GlyphAtlasRegion(0, 0, 0, 4, 3), region);
        Assert.Single(atlas.Pages);
        Assert.Equal(1, atlas.Pages[0].Revision);
    }

    [Fact]
    public void Add_GlyphsOnSameRowUseTwoPixelSeparation()
    {
        GlyphAtlas atlas = new();

        GlyphAtlasRegion? first = atlas.Add(CreateGlyph(4, 3));
        GlyphAtlasRegion? second = atlas.Add(CreateGlyph(2, 2));

        Assert.Equal(new GlyphAtlasRegion(0, 0, 0, 4, 3), first);
        Assert.Equal(new GlyphAtlasRegion(0, 6, 0, 2, 2), second);
        Assert.Single(atlas.Pages);
        Assert.Equal(2, atlas.Pages[0].Revision);
    }

    [Fact]
    public void Add_RowWrapUsesTallestRowHeightAndTwoPixelSeparation()
    {
        GlyphAtlas atlas = new();

        atlas.Add(CreateGlyph(250, 10));
        atlas.Add(CreateGlyph(250, 20));

        GlyphAtlasRegion? wrapped = atlas.Add(CreateGlyph(20, 5));

        Assert.Equal(new GlyphAtlasRegion(0, 0, 22, 20, 5), wrapped);
    }

    [Fact]
    public void Add_ExactHorizontalFitDoesNotWrap()
    {
        GlyphAtlas atlas = new();

        atlas.Add(CreateGlyph(510, 4));

        GlyphAtlasRegion? second = atlas.Add(CreateGlyph(2, 4));

        Assert.Equal(new GlyphAtlasRegion(0, 0, 6, 2, 4), second);
    }

    [Fact]
    public void Add_FullPageCreatesNewPage()
    {
        GlyphAtlas atlas = new();

        GlyphAtlasRegion? first = atlas.Add(CreateGlyph(512, 512));
        GlyphAtlasRegion? second = atlas.Add(CreateGlyph(1, 1));

        Assert.Equal(new GlyphAtlasRegion(0, 0, 0, 512, 512), first);
        Assert.Equal(new GlyphAtlasRegion(1, 0, 0, 1, 1), second);
        Assert.Equal(2, atlas.Pages.Count);
    }

    [Fact]
    public void Add_FailedFitDoesNotMutateEarlierPageAllocationState()
    {
        GlyphAtlas atlas = new();

        atlas.Add(CreateGlyph(500, 510));
        GlyphAtlasRegion? secondPageRegion = atlas.Add(CreateGlyph(20, 1));
        GlyphAtlasRegion? reusedFirstPageRegion = atlas.Add(CreateGlyph(10, 1));

        Assert.Equal(new GlyphAtlasRegion(1, 0, 0, 20, 1), secondPageRegion);
        Assert.Equal(new GlyphAtlasRegion(0, 502, 0, 10, 1), reusedFirstPageRegion);
        Assert.Equal(2, atlas.Pages.Count);
    }

    [Fact]
    public void Add_EmptyBitmapDoesNotAllocateAtlasStorage()
    {
        GlyphAtlas atlas = new();
        RasterizedGlyph glyph = new(0, 0, 0, 0, 4, []);

        GlyphAtlasRegion? region = atlas.Add(glyph);

        Assert.Null(region);
        Assert.Empty(atlas.Pages);
    }

    [Fact]
    public void Add_OversizedWidthThrows()
    {
        GlyphAtlas atlas = new();

        Assert.Throws<InvalidOperationException>(() => atlas.Add(CreateGlyph(GlyphAtlasPage.SizePixels + 1, 1)));
        Assert.Empty(atlas.Pages);
    }

    [Fact]
    public void Add_OversizedHeightThrows()
    {
        GlyphAtlas atlas = new();

        Assert.Throws<InvalidOperationException>(() => atlas.Add(CreateGlyph(1, GlyphAtlasPage.SizePixels + 1)));
        Assert.Empty(atlas.Pages);
    }

    [Fact]
    public void Add_CopiesGlyphCoverageIntoAllocatedPage()
    {
        GlyphAtlas atlas = new();
        RasterizedGlyph glyph = new(2, 2, 0, 0, 2, [1, 2, 3, 4]);

        GlyphAtlasRegion? region = atlas.Add(glyph);

        Assert.Equal(new GlyphAtlasRegion(0, 0, 0, 2, 2), region);

        ReadOnlySpan<byte> coverage = atlas.Pages[0].Coverage.Span;

        Assert.Equal(1, coverage[0]);
        Assert.Equal(2, coverage[1]);
        Assert.Equal(3, coverage[GlyphAtlasPage.SizePixels]);
        Assert.Equal(4, coverage[GlyphAtlasPage.SizePixels + 1]);
    }

    [Fact]
    public void Add_NullGlyphThrows()
    {
        GlyphAtlas atlas = new();

        Assert.Throws<ArgumentNullException>(() => atlas.Add(null!));
    }

    private static RasterizedGlyph CreateGlyph(int widthPixels, int heightPixels)
    {
        return new RasterizedGlyph(widthPixels, heightPixels, 0, 0, 1, new byte[checked(widthPixels * heightPixels)]);
    }
}
