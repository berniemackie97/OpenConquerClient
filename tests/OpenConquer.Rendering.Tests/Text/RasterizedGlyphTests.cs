using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class RasterizedGlyphTests
{
    [Fact]
    public void Constructor_PreservesMetricsAndCoverage()
    {
        byte[] coverage =
        [
            0, 64,
            128, 255,
        ];

        RasterizedGlyph glyph = new(
            widthPixels: 2,
            heightPixels: 2,
            bearingLeftPixels: -1,
            bearingTopPixels: 9,
            advancePixels: 7,
            coverage);

        Assert.Equal(2, glyph.WidthPixels);
        Assert.Equal(2, glyph.HeightPixels);
        Assert.Equal(-1, glyph.BearingLeftPixels);
        Assert.Equal(9, glyph.BearingTopPixels);
        Assert.Equal(7, glyph.AdvancePixels);
        Assert.Equal(coverage, glyph.Coverage.ToArray());
    }

    [Fact]
    public void Constructor_CopiesCoverage()
    {
        byte[] coverage = [10, 20, 30, 40];

        RasterizedGlyph glyph = new(
            widthPixels: 2,
            heightPixels: 2,
            bearingLeftPixels: 0,
            bearingTopPixels: 0,
            advancePixels: 2,
            coverage);

        coverage[0] = 255;

        Assert.Equal((byte)10, glyph.Coverage.Span[0]);
    }

    [Fact]
    public void Constructor_AcceptsEmptyBitmapWithNonZeroAdvance()
    {
        RasterizedGlyph glyph = new(
            widthPixels: 0,
            heightPixels: 0,
            bearingLeftPixels: 0,
            bearingTopPixels: 0,
            advancePixels: 6,
            []);

        Assert.Equal(0, glyph.WidthPixels);
        Assert.Equal(0, glyph.HeightPixels);
        Assert.Equal(6, glyph.AdvancePixels);
        Assert.True(glyph.Coverage.IsEmpty);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Constructor_RejectsNegativeBitmapDimensions(int widthPixels, int heightPixels)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RasterizedGlyph(widthPixels, heightPixels, 0, 0, 0, []));
    }

    [Theory]
    [InlineData(2, 2, 3)]
    [InlineData(2, 2, 5)]
    [InlineData(0, 4, 1)]
    [InlineData(4, 0, 1)]
    public void Constructor_RejectsCoverageLengthThatDoesNotMatchBitmap(int widthPixels, int heightPixels, int coverageLength)
    {
        byte[] coverage = new byte[coverageLength];

        Assert.Throws<ArgumentException>(() =>
            new RasterizedGlyph(widthPixels, heightPixels, 0, 0, 0, coverage));
    }

    [Fact]
    public void Constructor_RejectsBitmapAreaOverflow()
    {
        Assert.Throws<OverflowException>(() =>
            new RasterizedGlyph(int.MaxValue, 2, 0, 0, 0, []));
    }
}
