using System.Text;
using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

internal sealed class TestGlyphRasterizer : IGlyphRasterizer
{
    private readonly Func<Rune, (bool Found, RasterizedGlyph? Glyph)> _handler;
    private bool _disposed;

    public TestGlyphRasterizer(Func<Rune, (bool Found, RasterizedGlyph? Glyph)> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    public int CallCount
    {
        get; private set;
    }

    public List<Rune> Characters { get; } = [];

    public bool TryRasterizeGlyph(Rune character, out RasterizedGlyph? glyph)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        CallCount++;
        Characters.Add(character);

        (bool found, RasterizedGlyph? result) = _handler(character);
        glyph = result;
        return found;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
