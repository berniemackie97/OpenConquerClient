using OpenConquer.Rendering.Text.Atlas;

namespace OpenConquer.Rendering.Text.Layout;

internal enum NativeTextLayoutItemKind
{
    Glyph = 1,
    DataIcon = 2,
}

/// <summary>
/// Represents one ordered drawable item produced by native text layout.
/// </summary>
internal readonly record struct NativeTextLayoutItem
{
    private readonly ushort _glyphKey;
    private readonly GlyphAtlasRegion _atlasRegion;
    private readonly byte _dataIconIndex;
    private readonly int _dataIconWidthPixels;

    private NativeTextLayoutItem(NativeTextLayoutItemKind kind, int xPixels, int yPixels, ushort glyphKey, GlyphAtlasRegion atlasRegion, byte dataIconIndex, int dataIconWidthPixels)
    {
        Kind = kind;
        XPixels = xPixels;
        YPixels = yPixels;
        _glyphKey = glyphKey;
        _atlasRegion = atlasRegion;
        _dataIconIndex = dataIconIndex;
        _dataIconWidthPixels = dataIconWidthPixels;
    }

    public NativeTextLayoutItemKind Kind
    {
        get;
    }

    public int XPixels
    {
        get;
    }

    public int YPixels
    {
        get;
    }

    public ushort GlyphKey => Kind == NativeTextLayoutItemKind.Glyph ? _glyphKey : throw new InvalidOperationException("The layout item does not represent a glyph.");

    public GlyphAtlasRegion AtlasRegion => Kind == NativeTextLayoutItemKind.Glyph ? _atlasRegion : throw new InvalidOperationException("The layout item does not represent a glyph.");

    public byte DataIconIndex => Kind == NativeTextLayoutItemKind.DataIcon ? _dataIconIndex : throw new InvalidOperationException("The layout item does not represent a data icon.");

    public int DataIconWidthPixels => Kind == NativeTextLayoutItemKind.DataIcon ? _dataIconWidthPixels : throw new InvalidOperationException("The layout item does not represent a data icon.");

    public static NativeTextLayoutItem CreateGlyph(ushort glyphKey, int xPixels, int yPixels, GlyphAtlasRegion atlasRegion)
    {
        return new NativeTextLayoutItem(NativeTextLayoutItemKind.Glyph, xPixels, yPixels, glyphKey, atlasRegion, 0, 0);
    }

    public static NativeTextLayoutItem CreateDataIcon(byte dataIconIndex, int xPixels, int yPixels, int widthPixels)
    {
        ArgumentOutOfRangeException.ThrowIfZero(widthPixels);
        return new NativeTextLayoutItem(NativeTextLayoutItemKind.DataIcon, xPixels, yPixels, 0, default, dataIconIndex, widthPixels);
    }
}
