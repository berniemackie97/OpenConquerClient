using OpenConquer.Content.Images;
using OpenConquer.Platform;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class FireworkDxt3Conformance
{
    private const int TargetWidth = 16;
    private const int TargetHeight = 16;

    private static readonly SpriteColor s_backgroundColor = new(32, 32, 32, byte.MaxValue);

    public static void Run(OpenGLGraphicsDevice graphicsDevice, RgbaImage image, PixelSize framebufferSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(image);

        if (image.Width != TargetWidth || image.Height != TargetHeight)
        {
            throw new ArgumentException($"Expected a {TargetWidth}x{TargetHeight} retail firework image.", nameof(image));
        }

        LogicalRenderSize logicalRenderSize = new(TargetWidth, TargetHeight);
        ReadOnlySpan<byte> whitePixel = [byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue];

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D backgroundTexture = graphicsDevice.CreateTexture2D(width: 1, height: 1, whitePixel);
        using OpenGLTexture2D fireworkTexture = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);

        byte[] background = RenderBackgroundFrame(renderer, backgroundTexture);
        byte[] additive = RenderFireworkFrame(renderer, backgroundTexture, fireworkTexture, SpriteBlendMode.Additive);
        byte[] alpha = RenderFireworkFrame(renderer, backgroundTexture, fireworkTexture, SpriteBlendMode.Alpha);

        if (!HasDifferentRgb(background, additive))
        {
            throw new InvalidDataException("Retail DXT3 firework additive rendering does not contribute visible RGB output.");
        }

        if (!HasDifferentRgb(additive, alpha))
        {
            throw new InvalidDataException("Retail DXT3 firework additive rendering is not observably distinct from alpha blending in RGB output.");
        }

        Console.WriteLine($"Retail DXT3 firework decoded RGBA SHA256: {ConformanceHash.Sha256(image.Pixels.Span)}");
        Console.WriteLine($"Retail DXT3 firework additive framebuffer SHA256: {ConformanceHash.Sha256(additive)}");
        Console.WriteLine($"Retail DXT3 firework alpha framebuffer SHA256: {ConformanceHash.Sha256(alpha)}");
    }

    private static byte[] RenderBackgroundFrame(OpenGLRenderer renderer, OpenGLTexture2D backgroundTexture)
    {
        renderer.BeginFrame();
        renderer.DrawSprite(backgroundTexture, x: 0, y: 0, width: TargetWidth, height: TargetHeight, s_backgroundColor);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static byte[] RenderFireworkFrame(OpenGLRenderer renderer, OpenGLTexture2D backgroundTexture, OpenGLTexture2D fireworkTexture, SpriteBlendMode blendMode)
    {
        renderer.BeginFrame();
        renderer.DrawSprite(backgroundTexture, x: 0, y: 0, width: TargetWidth, height: TargetHeight, s_backgroundColor);
        renderer.DrawSprite(fireworkTexture, x: 0, y: 0, SpriteColor.White, blendMode);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static bool HasDifferentRgb(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length || (left.Length & 3) != 0)
        {
            throw new ArgumentException("Framebuffer comparisons require equally sized RGBA buffers.");
        }

        for (int offset = 0; offset < left.Length; offset += 4)
        {
            if (left[offset] != right[offset] || left[offset + 1] != right[offset + 1] || left[offset + 2] != right[offset + 2])
            {
                return true;
            }
        }

        return false;
    }
}
