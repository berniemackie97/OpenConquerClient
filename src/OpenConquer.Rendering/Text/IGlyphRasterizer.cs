using System.Text;

namespace OpenConquer.Rendering.Text;

/// <summary>
/// Rasterizes Unicode scalar values using one configured font face.
/// </summary>
internal interface IGlyphRasterizer : IDisposable
{
    /// <summary>
    /// Attempts to rasterize <paramref name="character"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the configured font contains the requested glyph;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool TryRasterizeGlyph(Rune character, out RasterizedGlyph? glyph);
}
