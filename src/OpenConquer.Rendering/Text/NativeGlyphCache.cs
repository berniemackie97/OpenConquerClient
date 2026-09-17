using System.Text;

namespace OpenConquer.Rendering.Text;

/// <summary>
/// Caches native encoded glyph keys, performs record-zero missing-glyph fallback, and owns atlas placement.
/// </summary>
/// <remarks>
/// This type is single-owner rendering state. It does not own or dispose the supplied font rasterizers.
/// </remarks>
internal sealed class NativeGlyphCache
{
    private readonly Dictionary<ushort, CachedGlyph> _glyphs = [];
    private readonly EncodedGlyphDecoder _decoder;
    private readonly NativeTextFontRecord _primaryFont;
    private readonly NativeTextFontRecord _recordZeroFont;

    public NativeGlyphCache(NativeTextFontRecord primaryFont, NativeTextFontRecord recordZeroFont, int effectiveCodePage)
    {
        ArgumentNullException.ThrowIfNull(primaryFont);
        ArgumentNullException.ThrowIfNull(recordZeroFont);

        if (recordZeroFont.RecordIndex != 0)
        {
            throw new ArgumentException("The missing-glyph fallback font must be native font record index 0.", nameof(recordZeroFont));
        }

        _primaryFont = primaryFont;
        _recordZeroFont = recordZeroFont;
        _decoder = new EncodedGlyphDecoder(effectiveCodePage);
        Atlas = new GlyphAtlas();
    }

    public GlyphAtlas Atlas
    {
        get;
    }

    public int Count => _glyphs.Count;

    public CachedGlyph GetOrAdd(ushort glyphKey)
    {
        if (_glyphs.TryGetValue(glyphKey, out CachedGlyph? cachedGlyph))
        {
            return cachedGlyph;
        }

        CachedGlyph glyph = CreateGlyph(glyphKey);
        _glyphs.Add(glyphKey, glyph);
        return glyph;
    }

    private CachedGlyph CreateGlyph(ushort glyphKey)
    {
        if (!_decoder.TryDecode(glyphKey, out Rune character))
        {
            return CachedGlyph.Missing(_primaryFont.LineHeightPixels);
        }

        if (TryRasterize(_primaryFont, character) is { } primaryGlyph)
        {
            return primaryGlyph;
        }

        if (_primaryFont.RecordIndex != 0 && TryRasterize(_recordZeroFont, character) is { } fallbackGlyph)
        {
            return fallbackGlyph;
        }

        return CachedGlyph.Missing(_primaryFont.LineHeightPixels);
    }

    private CachedGlyph? TryRasterize(NativeTextFontRecord font, Rune character)
    {
        bool found = font.Rasterizer.TryRasterizeGlyph(character, out RasterizedGlyph? rasterizedGlyph);

        if (found != (rasterizedGlyph is not null))
        {
            throw new InvalidOperationException($"Glyph rasterizer for font record {font.RecordIndex} violated the TryRasterizeGlyph result contract.");
        }

        if (!found)
        {
            return null;
        }

        GlyphAtlasRegion? atlasRegion = Atlas.Add(rasterizedGlyph!);
        return CachedGlyph.FromRasterized(font.RecordIndex, rasterizedGlyph!, atlasRegion);
    }
}
