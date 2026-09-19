namespace OpenConquer.Rendering.Text.Fonts;

/// <summary>
/// Resolves font tokens and the host GUI fallback font.
/// </summary>
internal interface IFontResolver
{
    bool TryResolve(string fontToken, out ResolvedFont? font);

    bool TryResolveDefaultGuiFont(out ResolvedFont? font);
}
