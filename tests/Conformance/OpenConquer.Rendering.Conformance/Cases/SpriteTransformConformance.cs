using System.Numerics;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Conformance.Cases;

/// <summary>
/// Affine sprite placement, the Flash color transform and nested masks. Every expected channel is 0 or 255, so the
/// result is exact in any 16-bit color format.
/// </summary>
internal static class SpriteTransformConformance
{
    private const int Size = 8;

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        LogicalRenderSize logicalRenderSize = new(Size, Size);
        SpriteSourceRectangle single = new(0, 0, 1, 1);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D red = graphicsDevice.CreateTexture2D(width: 2, height: 2,
            [255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255]);
        using OpenGLTexture2D white = graphicsDevice.CreateTexture2D(width: 1, height: 1, [255, 255, 255, 255]);
        using OpenGLTexture2D faint = graphicsDevice.CreateTexture2D(width: 1, height: 1, [100, 100, 100, 100]);

        // Scale 2 and translate (1, 1); then white turned cyan by the color transform.
        renderer.BeginFrame();
        renderer.DrawTransformedSprite(red, new SpriteSourceRectangle(0, 0, 2, 2), Matrix3x2.CreateScale(2f) * Matrix3x2.CreateTranslation(1f, 1f),
            SpriteColorTransform.Identity);
        renderer.DrawTransformedSprite(white, single, Matrix3x2.CreateScale(2f) * Matrix3x2.CreateTranslation(5f, 5f),
            new SpriteColorTransform(0f, 1f, 0f, 1f, 0f, 0f, 1f, 0f));
        byte[] placed = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        byte[] expectedPlaced = FramebufferVerifier.CreateOpaqueBlack(Size, Size);
        Fill(expectedPlaced, 1, 1, 4, 4, 255, 0, 0);
        Fill(expectedPlaced, 5, 5, 2, 2, 0, 255, 255);
        FramebufferVerifier.VerifyExact("Sprite affine transform and color transform", expectedPlaced, placed);

        // Outer mask: the left half (the faint right half stays below the coverage threshold). Inner mask: the top half.
        renderer.BeginFrame();
        renderer.BeginMask();
        renderer.DrawTransformedSprite(white, single, Matrix3x2.CreateScale(4f, 8f), SpriteColorTransform.Identity);
        renderer.DrawTransformedSprite(faint, single, Matrix3x2.CreateScale(4f, 8f) * Matrix3x2.CreateTranslation(4f, 0f), SpriteColorTransform.Identity);
        renderer.BeginMaskedContent();
        renderer.BeginMask();
        renderer.DrawTransformedSprite(white, single, Matrix3x2.CreateScale(8f, 4f), SpriteColorTransform.Identity);
        renderer.BeginMaskedContent();
        renderer.DrawTransformedSprite(red, new SpriteSourceRectangle(0, 0, 2, 2), Matrix3x2.CreateScale(4f), SpriteColorTransform.Identity);
        renderer.EndMask();
        renderer.DrawTransformedSprite(white, single, Matrix3x2.CreateScale(8f, 2f) * Matrix3x2.CreateTranslation(0f, 6f),
            new SpriteColorTransform(0f, 1f, 0f, 1f, 0f, 0f, 0f, 0f));
        renderer.EndMask();
        renderer.DrawTransformedSprite(white, single, Matrix3x2.CreateTranslation(7f, 7f), new SpriteColorTransform(0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f));
        byte[] masked = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        byte[] expectedMasked = FramebufferVerifier.CreateOpaqueBlack(Size, Size);
        Fill(expectedMasked, 0, 0, 4, 4, 255, 0, 0);
        Fill(expectedMasked, 0, 6, 4, 2, 0, 255, 0);
        Fill(expectedMasked, 7, 7, 1, 1, 0, 0, 255);
        FramebufferVerifier.VerifyExact("Sprite nested masks", expectedMasked, masked);

        Console.WriteLine($"Sprite transform: scale/translate, color transform and two nested masks, {colorFormat}");
        Console.WriteLine($"Sprite transform SHA256: {ConformanceHash.Sha256(placed)} / {ConformanceHash.Sha256(masked)}");
    }

    private static void Fill(byte[] framebuffer, int x, int y, int width, int height, byte red, byte green, byte blue)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width; column++)
            {
                int offset = (row * Size + column) * 4;
                framebuffer[offset] = red;
                framebuffer[offset + 1] = green;
                framebuffer[offset + 2] = blue;
            }
        }
    }
}
