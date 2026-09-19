using System.Text;
using OpenConquer.Rendering.Text.Glyphs;

namespace OpenConquer.Rendering.Tests.Text;

internal sealed class TestGlyphRasterizer : IGlyphRasterizer
{
    private readonly Func<Rune, (bool Found, RasterizedGlyph? Glyph)> _handler;
    private bool _disposed;

    public TestGlyphRasterizer(Func<Rune, (bool Found, RasterizedGlyph? Glyph)> handler, bool antialiasEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handler = handler;
        AntialiasEnabled = antialiasEnabled;
    }

    public bool AntialiasEnabled
    {
        get;
    }

    public int CallCount
    {
        get; private set;
    }

    public List<Rune> Characters { get; } = [];

    public bool TryRasterizeGlyph(Rune rune, out RasterizedGlyph? glyph)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        CallCount++;
        Characters.Add(rune);

        (bool found, RasterizedGlyph? result) = _handler(rune);
        glyph = result;
        return found;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
