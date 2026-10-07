using System.Text;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Glyphs;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class OpenGLTextContextConformance
{
    public static void Run(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        const int targetWidth = 4;
        const int targetHeight = 2;
        const int originX = 1;
        const int originY = 0;

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(new LogicalRenderSize(targetWidth, targetHeight), framebufferSize.Width, framebufferSize.Height);
        using OpenGLTextContext textContext = new(graphicsDevice, new SyntheticGlyphRasterizer(), nominalPixelHeight: 1, effectiveCodePage: 936);

        OpenGLTextLayout layout = textContext.Layout("11"u8);
        NativeTextRenderOptions options = new(NativeTextRenderStyle.Normal, SpriteColor.White, SpriteColor.White, 0, 0, NativeTextVertexColors.Solid(SpriteColor.White));

        if (layout.WidthPixels != 2 || layout.HeightPixels != 1)
        {
            throw new InvalidDataException($"OpenGL text-context layout expected 2x1 pixels; received {layout.WidthPixels}x{layout.HeightPixels}.");
        }

        renderer.BeginFrame();
        textContext.Draw(renderer, layout, options, originX, originY);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        byte[] expected = FramebufferVerifier.CreateOpaqueBlack(targetWidth, targetHeight);
        SetWhitePixel(expected, targetWidth, originX, originY);
        SetWhitePixel(expected, targetWidth, originX + 1, originY);

        FramebufferVerifier.VerifyExact("OpenGL text context façade", expected, actual);

        Console.WriteLine($"OpenGL text context façade: 2x1 cached glyph layout at ({originX}, {originY})");
        Console.WriteLine($"OpenGL text context façade SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static void SetWhitePixel(byte[] framebuffer, int width, int x, int y)
    {
        int offset = checked(((y * width) + x) * 4);
        framebuffer[offset] = byte.MaxValue;
        framebuffer[offset + 1] = byte.MaxValue;
        framebuffer[offset + 2] = byte.MaxValue;
        framebuffer[offset + 3] = byte.MaxValue;
    }

    private sealed class SyntheticGlyphRasterizer : IGlyphRasterizer
    {
        private bool _disposed;

        public bool AntialiasEnabled => true;

        public bool TryRasterizeGlyph(Rune rune, out RasterizedGlyph? glyph)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (rune.Value != '1')
            {
                glyph = null;
                return false;
            }

            glyph = new RasterizedGlyph(widthPixels: 1, heightPixels: 1, bearingLeftPixels: 0, topOffsetPixels: 0, advancePixels: 1, [byte.MaxValue]);
            return true;
        }

        public void Dispose() => _disposed = true;
    }
}
