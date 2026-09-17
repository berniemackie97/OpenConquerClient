using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class GlyphAtlasPageTests
{
    [Fact]
    public void Coverage_NewPage_IsZeroInitialized()
    {
        GlyphAtlasPage page = new();

        Assert.Equal(GlyphAtlasPage.SizePixels * GlyphAtlasPage.SizePixels, page.Coverage.Length);
        Assert.All(page.Coverage.ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(0, page.Revision);
    }

    [Fact]
    public void WriteGlyph_CopiesCoverageAtRequestedPosition()
    {
        GlyphAtlasPage page = new();

        page.WriteGlyph([1, 2, 3, 4], widthPixels: 2, heightPixels: 2, xPixels: 3, yPixels: 4);

        ReadOnlySpan<byte> coverage = page.Coverage.Span;

        Assert.Equal(1, coverage[(4 * GlyphAtlasPage.SizePixels) + 3]);
        Assert.Equal(2, coverage[(4 * GlyphAtlasPage.SizePixels) + 4]);
        Assert.Equal(3, coverage[(5 * GlyphAtlasPage.SizePixels) + 3]);
        Assert.Equal(4, coverage[(5 * GlyphAtlasPage.SizePixels) + 4]);
        Assert.Equal(0, coverage[(4 * GlyphAtlasPage.SizePixels) + 2]);
        Assert.Equal(0, coverage[(4 * GlyphAtlasPage.SizePixels) + 5]);
        Assert.Equal(1, page.Revision);
    }

    [Fact]
    public void WriteGlyph_ExactBottomRightFit_Succeeds()
    {
        GlyphAtlasPage page = new();

        page.WriteGlyph([1, 2, 3, 4], widthPixels: 2, heightPixels: 2, xPixels: GlyphAtlasPage.SizePixels - 2, yPixels: GlyphAtlasPage.SizePixels - 2);

        ReadOnlySpan<byte> coverage = page.Coverage.Span;
        int lastRow = GlyphAtlasPage.SizePixels - 1;

        Assert.Equal(4, coverage[(lastRow * GlyphAtlasPage.SizePixels) + GlyphAtlasPage.SizePixels - 1]);
        Assert.Equal(1, page.Revision);
    }

    [Fact]
    public void WriteGlyph_MultipleWritesIncrementRevision()
    {
        GlyphAtlasPage page = new();

        page.WriteGlyph([1], 1, 1, 0, 0);
        page.WriteGlyph([2], 1, 1, 1, 0);

        Assert.Equal(2, page.Revision);
    }

    [Fact]
    public void WriteGlyph_ZeroOrNegativeDimensionsThrow()
    {
        GlyphAtlasPage page = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => page.WriteGlyph([], 0, 1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.WriteGlyph([], 1, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.WriteGlyph([], -1, 1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.WriteGlyph([], 1, -1, 0, 0));
    }

    [Fact]
    public void WriteGlyph_DimensionsLargerThanPageThrow()
    {
        GlyphAtlasPage page = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => page.WriteGlyph([], GlyphAtlasPage.SizePixels + 1, 1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.WriteGlyph([], 1, GlyphAtlasPage.SizePixels + 1, 0, 0));
    }

    [Fact]
    public void WriteGlyph_NegativePositionThrows()
    {
        GlyphAtlasPage page = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => page.WriteGlyph([1], 1, 1, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.WriteGlyph([1], 1, 1, 0, -1));
    }

    [Fact]
    public void WriteGlyph_MismatchedCoverageLengthThrows()
    {
        GlyphAtlasPage page = new();

        Assert.Throws<ArgumentException>(() => page.WriteGlyph([1, 2, 3], 2, 2, 0, 0));
        Assert.Equal(0, page.Revision);
    }

    [Fact]
    public void WriteGlyph_HorizontalOverflowThrows()
    {
        GlyphAtlasPage page = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => page.WriteGlyph([1, 2], 2, 1, GlyphAtlasPage.SizePixels - 1, 0));
        Assert.Equal(0, page.Revision);
    }

    [Fact]
    public void WriteGlyph_VerticalOverflowThrows()
    {
        GlyphAtlasPage page = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => page.WriteGlyph([1, 2], 1, 2, 0, GlyphAtlasPage.SizePixels - 1));
        Assert.Equal(0, page.Revision);
    }
}
