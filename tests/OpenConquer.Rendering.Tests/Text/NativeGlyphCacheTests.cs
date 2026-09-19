using System.Text;
using OpenConquer.Rendering.Text.Glyphs;
using OpenConquer.Rendering.Text.Layout;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class NativeGlyphCacheTests
{
    [Fact]
    public void GetOrAdd_DecodesAndCachesNativeGlyphKey()
    {
        Rune observedCharacter = default;
        TestGlyphRasterizer primaryRasterizer = new(character =>
        {
            observedCharacter = character;
            return (true, CreateGlyph(2, 3, advancePixels: 5));
        });
        TestGlyphRasterizer recordZeroRasterizer = new(_ => (false, null));
        NativeTextFontRecord primaryFont = new(2, 12, 12, primaryRasterizer);
        NativeTextFontRecord recordZeroFont = new(0, 12, 12, recordZeroRasterizer);
        NativeGlyphCache cache = new(primaryFont, recordZeroFont, effectiveCodePage: 936);

        CachedGlyph first = cache.GetOrAdd(0xB0D7);
        CachedGlyph second = cache.GetOrAdd(0xB0D7);

        Assert.Same(first, second);
        Assert.Equal(0x767D, observedCharacter.Value);
        Assert.Equal(1, primaryRasterizer.CallCount);
        Assert.Equal(0, recordZeroRasterizer.CallCount);
        Assert.Equal(2, first.SourceFontRecordIndex);
        Assert.False(first.IsMissing);
        Assert.True(first.HasBitmap);
        Assert.Single(cache.Atlas.Pages);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrAdd_PrimaryMissingRetriesRecordZero()
    {
        TestGlyphRasterizer primaryRasterizer = new(_ => (false, null));
        TestGlyphRasterizer recordZeroRasterizer = new(_ => (true, CreateGlyph(2, 2, advancePixels: 6)));
        NativeGlyphCache cache = new(new NativeTextFontRecord(3, 12, 14, primaryRasterizer), new NativeTextFontRecord(0, 12, 12, recordZeroRasterizer), 936);

        CachedGlyph glyph = cache.GetOrAdd(0x0041);

        Assert.Equal(1, primaryRasterizer.CallCount);
        Assert.Equal(1, recordZeroRasterizer.CallCount);
        Assert.Equal(0, glyph.SourceFontRecordIndex);
        Assert.Equal(6, glyph.AdvancePixels);
        Assert.False(glyph.IsMissing);
    }

    [Fact]
    public void GetOrAdd_PrimaryRecordZeroDoesNotRetryItself()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, 12, 13, rasterizer);
        NativeGlyphCache cache = new(font, font, 936);

        CachedGlyph glyph = cache.GetOrAdd(0x0041);

        Assert.Equal(1, rasterizer.CallCount);
        Assert.True(glyph.IsMissing);
        Assert.Equal(13, glyph.AdvancePixels);
    }

    [Fact]
    public void GetOrAdd_BothFontsMissingUsesPrimaryLineHeightAdvance()
    {
        TestGlyphRasterizer primaryRasterizer = new(_ => (false, null));
        TestGlyphRasterizer recordZeroRasterizer = new(_ => (false, null));
        NativeGlyphCache cache = new(new NativeTextFontRecord(4, 12, 17, primaryRasterizer), new NativeTextFontRecord(0, 12, 12, recordZeroRasterizer), 936);

        CachedGlyph glyph = cache.GetOrAdd(0x0041);

        Assert.True(glyph.IsMissing);
        Assert.Null(glyph.SourceFontRecordIndex);
        Assert.Null(glyph.AtlasRegion);
        Assert.Equal(17, glyph.AdvancePixels);
        Assert.Equal(1, primaryRasterizer.CallCount);
        Assert.Equal(1, recordZeroRasterizer.CallCount);
    }

    [Fact]
    public void GetOrAdd_DecodeFailureUsesMissingAdvanceWithoutRasterization()
    {
        TestGlyphRasterizer primaryRasterizer = new(_ => throw new InvalidOperationException("Rasterizer must not run."));
        TestGlyphRasterizer recordZeroRasterizer = new(_ => throw new InvalidOperationException("Rasterizer must not run."));
        NativeGlyphCache cache = new(new NativeTextFontRecord(1, 12, 9, primaryRasterizer), new NativeTextFontRecord(0, 12, 12, recordZeroRasterizer), effectiveCodePage: 0);

        CachedGlyph glyph = cache.GetOrAdd(0x0041);

        Assert.True(glyph.IsMissing);
        Assert.Equal(9, glyph.AdvancePixels);
        Assert.Equal(0, primaryRasterizer.CallCount);
        Assert.Equal(0, recordZeroRasterizer.CallCount);
    }

    [Fact]
    public void GetOrAdd_SpaceCachesAdvanceWithoutAtlasPlacement()
    {
        TestGlyphRasterizer rasterizer = new(_ => (true, new RasterizedGlyph(0, 0, 0, 0, 4, [])));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeGlyphCache cache = new(font, font, 936);

        CachedGlyph first = cache.GetOrAdd(0x0020);
        CachedGlyph second = cache.GetOrAdd(0x0020);

        Assert.Same(first, second);
        Assert.False(first.IsMissing);
        Assert.False(first.HasBitmap);
        Assert.Equal(4, first.AdvancePixels);
        Assert.Empty(cache.Atlas.Pages);
        Assert.Equal(1, rasterizer.CallCount);
    }

    [Fact]
    public void GetOrAdd_MissingGlyphIsCached()
    {
        TestGlyphRasterizer primaryRasterizer = new(_ => (false, null));
        TestGlyphRasterizer recordZeroRasterizer = new(_ => (false, null));
        NativeGlyphCache cache = new(new NativeTextFontRecord(1, 12, 11, primaryRasterizer), new NativeTextFontRecord(0, 12, 12, recordZeroRasterizer), 936);

        CachedGlyph first = cache.GetOrAdd(0x0041);
        CachedGlyph second = cache.GetOrAdd(0x0041);

        Assert.Same(first, second);
        Assert.True(first.IsMissing);
        Assert.Equal(1, primaryRasterizer.CallCount);
        Assert.Equal(1, recordZeroRasterizer.CallCount);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrAdd_DifferentEncodedKeysRemainDifferentCacheEntries()
    {
        TestGlyphRasterizer rasterizer = new(_ => (true, CreateGlyph(1, 1, advancePixels: 3)));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeGlyphCache cache = new(font, font, 936);

        CachedGlyph first = cache.GetOrAdd(0x0041);
        CachedGlyph second = cache.GetOrAdd(0x0042);

        Assert.NotSame(first, second);
        Assert.Equal(2, rasterizer.CallCount);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void GetOrAdd_RasterizerReturningTrueWithNullGlyphThrows()
    {
        TestGlyphRasterizer rasterizer = new(_ => (true, null));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeGlyphCache cache = new(font, font, 936);

        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(0x0041));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void GetOrAdd_RasterizerReturningFalseWithGlyphThrows()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, CreateGlyph(1, 1, advancePixels: 3)));
        NativeTextFontRecord font = new(0, 12, 12, rasterizer);
        NativeGlyphCache cache = new(font, font, 936);

        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(0x0041));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Constructor_RecordZeroFallbackMustBeRecordZero()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord primary = new(1, 12, 12, rasterizer);
        NativeTextFontRecord invalidFallback = new(2, 12, 12, rasterizer);

        Assert.Throws<ArgumentException>(() => new NativeGlyphCache(primary, invalidFallback, 936));
    }

    [Fact]
    public void Constructor_NullPrimaryFontThrows()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord recordZero = new(0, 12, 12, rasterizer);

        Assert.Throws<ArgumentNullException>(() => new NativeGlyphCache(null!, recordZero, 936));
    }

    [Fact]
    public void Constructor_NullRecordZeroFontThrows()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord primary = new(1, 12, 12, rasterizer);

        Assert.Throws<ArgumentNullException>(() => new NativeGlyphCache(primary, null!, 936));
    }

    [Fact]
    public void NativeTextFontRecord_RejectsInvalidIdentityAndZeroMetrics()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));

        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeTextFontRecord(-1, 12, 12, rasterizer));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeTextFontRecord(0, 0, 12, rasterizer));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeTextFontRecord(0, 12, 0, rasterizer));
        Assert.Throws<ArgumentNullException>(() => new NativeTextFontRecord(0, 12, 12, null!));
    }

    [Fact]
    public void NativeTextFontRecord_PreservesNegativeNonzeroMetrics()
    {
        TestGlyphRasterizer rasterizer = new(_ => (false, null));
        NativeTextFontRecord font = new(0, -12, -9, rasterizer);

        Assert.Equal(-12, font.NominalPixelHeight);
        Assert.Equal(-9, font.LineHeightPixels);
    }

    private static RasterizedGlyph CreateGlyph(int widthPixels, int heightPixels, int advancePixels)
    {
        return new RasterizedGlyph(widthPixels, heightPixels, 0, 0, advancePixels, new byte[checked(widthPixels * heightPixels)]);
    }
}
