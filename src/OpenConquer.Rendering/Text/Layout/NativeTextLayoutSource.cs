using OpenConquer.Rendering.Text.Atlas;

namespace OpenConquer.Rendering.Text.Layout;

/// <summary>
/// Identifies the authoritative font configuration and glyph atlas that produced a native text layout.
/// </summary>
internal sealed class NativeTextLayoutSource
{
    public NativeTextLayoutSource(NativeTextFontRecord primaryFont, GlyphAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(primaryFont);
        ArgumentNullException.ThrowIfNull(atlas);

        PrimaryFont = primaryFont;
        Atlas = atlas;
    }

    public NativeTextFontRecord PrimaryFont
    {
        get;
    }

    public GlyphAtlas Atlas
    {
        get;
    }
}
