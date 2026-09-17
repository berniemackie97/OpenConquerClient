namespace OpenConquer.Rendering.Text;

/// <summary>
/// Measures and lays out legacy encoded text using native text semantics.
/// </summary>
internal sealed class NativeTextLayoutEngine
{
    private const int MissingDataIconWidthPixels = 16;

    private readonly int _effectiveCodePage;
    private readonly int _lineAdvancePixels;
    private readonly NativeTextFontRecord _primaryFont;
    private readonly NativeGlyphCache _glyphCache;
    private readonly IDataIconWidthProvider? _dataIconWidthProvider;

    public NativeTextLayoutEngine(NativeTextFontRecord primaryFont, NativeTextFontRecord recordZeroFont, int effectiveCodePage, IDataIconWidthProvider? dataIconWidthProvider = null)
    {
        ArgumentNullException.ThrowIfNull(primaryFont);
        ArgumentNullException.ThrowIfNull(recordZeroFont);

        _primaryFont = primaryFont;
        _effectiveCodePage = effectiveCodePage;
        _lineAdvancePixels = checked(primaryFont.NominalPixelHeight + (primaryFont.NominalPixelHeight / 4));
        _glyphCache = new NativeGlyphCache(primaryFont, recordZeroFont, effectiveCodePage);
        _dataIconWidthProvider = dataIconWidthProvider;
    }

    public GlyphAtlas Atlas => _glyphCache.Atlas;

    public NativeTextLayout Layout(ReadOnlySpan<byte> encodedText, bool recognizeDataIcons, int dataIconWidthPixels = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dataIconWidthPixels);

        EncodedTextReader reader = new(encodedText, _effectiveCodePage, recognizeDataIcons);
        List<NativeTextLayoutItem> items = new(Math.Min(encodedText.Length, 256));
        int penXPixels = 0;
        int penYPixels = 0;
        int maximumWidthPixels = 0;

        while (reader.TryRead(out EncodedTextToken token))
        {
            switch (token.Kind)
            {
                case EncodedTextTokenKind.Glyph:
                    CachedGlyph glyph = _glyphCache.GetOrAdd(token.GlyphKey);

                    if (glyph.AtlasRegion is { } atlasRegion)
                    {
                        int glyphXPixels = checked(penXPixels + glyph.BearingLeftPixels);
                        int glyphYPixels = checked(penYPixels + glyph.TopOffsetPixels);
                        items.Add(NativeTextLayoutItem.CreateGlyph(token.GlyphKey, glyphXPixels, glyphYPixels, atlasRegion));
                    }

                    penXPixels = checked(penXPixels + glyph.AdvancePixels);
                    maximumWidthPixels = Math.Max(maximumWidthPixels, penXPixels);
                    break;

                case EncodedTextTokenKind.NewLine:
                    maximumWidthPixels = Math.Max(maximumWidthPixels, penXPixels);
                    penXPixels = 0;
                    penYPixels = checked(penYPixels + _lineAdvancePixels);
                    break;

                case EncodedTextTokenKind.DataIcon:
                    int widthPixels = ResolveDataIconWidth(token.DataIconIndex, dataIconWidthPixels);
                    items.Add(NativeTextLayoutItem.CreateDataIcon(token.DataIconIndex, penXPixels, penYPixels, widthPixels));
                    penXPixels = checked(penXPixels + widthPixels);
                    maximumWidthPixels = Math.Max(maximumWidthPixels, penXPixels);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported encoded text token kind {(int)token.Kind}.");
            }
        }

        maximumWidthPixels = Math.Max(maximumWidthPixels, penXPixels);
        int heightPixels = checked(penYPixels + _primaryFont.NominalPixelHeight);

        return new NativeTextLayout(maximumWidthPixels, heightPixels, items);
    }

    private int ResolveDataIconWidth(byte dataIconIndex, int explicitWidthPixels)
    {
        if (explicitWidthPixels > 0)
        {
            return explicitWidthPixels;
        }

        if (_dataIconWidthProvider is null || !_dataIconWidthProvider.TryGetWidth(dataIconIndex, out int widthPixels))
        {
            return MissingDataIconWidthPixels;
        }

        if (widthPixels < 0)
        {
            throw new InvalidOperationException($"Data-icon width provider returned negative width {widthPixels} for icon {dataIconIndex}.");
        }

        return widthPixels == 0 ? MissingDataIconWidthPixels : widthPixels;
    }
}
