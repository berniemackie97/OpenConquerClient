using System.Text;
using OpenConquer.Rendering.Text.Atlas;
using OpenConquer.Rendering.Text.Glyphs;
using OpenConquer.Rendering.Text.Layout;

namespace OpenConquer.Rendering.Conformance.Fixtures;

/// <summary>
/// Provides deterministic synthetic glyph-atlas data for native-text OpenGL conformance without relying on host fonts or FreeType output.
/// </summary>
internal sealed class NativeTextSyntheticFixture : IDisposable
{
    private readonly SyntheticGlyphRasterizer _rasterizer;
    private bool _disposed;

    public NativeTextSyntheticFixture(bool antialiasEnabled = true)
    {
        _rasterizer = new SyntheticGlyphRasterizer(antialiasEnabled);
        Font = new NativeTextFontRecord(recordIndex: 0, nominalPixelHeight: 12, lineHeightPixels: 12, _rasterizer);
        Atlas = new GlyphAtlas();
        Source = new NativeTextLayoutSource(Font, Atlas);
    }

    public NativeTextFontRecord Font
    {
        get;
    }

    public GlyphAtlas Atlas
    {
        get;
    }

    public NativeTextLayoutSource Source
    {
        get;
    }

    public GlyphAtlasRegion AddGlyph(int widthPixels, int heightPixels, ReadOnlySpan<byte> coverage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        RasterizedGlyph glyph = new(widthPixels, heightPixels, bearingLeftPixels: 0, topOffsetPixels: 0, advancePixels: widthPixels, coverage);

        return Atlas.Add(glyph) ?? throw new InvalidOperationException("Synthetic conformance glyph unexpectedly produced no atlas region.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _rasterizer.Dispose();
        _disposed = true;
    }

    private sealed class SyntheticGlyphRasterizer : IGlyphRasterizer
    {
        private bool _disposed;

        public SyntheticGlyphRasterizer(bool antialiasEnabled)
        {
            AntialiasEnabled = antialiasEnabled;
        }

        public bool AntialiasEnabled
        {
            get;
        }

        public bool TryRasterizeGlyph(Rune rune, out RasterizedGlyph? glyph)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            glyph = null;
            throw new NotSupportedException("Synthetic text conformance populates the glyph atlas directly and must not invoke font rasterization.");
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
