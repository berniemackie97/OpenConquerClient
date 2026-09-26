using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class SpriteRepeatedSamplingConformance
{
    private const int TargetWidth = 4;
    private const int TargetHeight = 4;

    private static readonly SpriteSourceBounds s_wrappedSource = new(left: 0, top: 0, right: 2, bottom: 3);
    private static readonly SpriteSourceBounds s_degenerateSource = new(left: 0, top: 0, right: 0, bottom: 0);

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        RunWrappedSourceCase(graphicsDevice, framebufferSize);
        RunDegenerateSourceCase(graphicsDevice, framebufferSize);
    }

    private static void RunWrappedSourceCase(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize)
    {
        ReadOnlySpan<byte> texturePixels =
        [
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 255, 255,
        ];

        byte[] expected = FramebufferVerifier.CreateOpaqueBlack(TargetWidth, TargetHeight);

        WritePixel(expected, x: 1, y: 0, red: 255, green: 0, blue: 0);
        WritePixel(expected, x: 2, y: 0, red: 0, green: 255, blue: 0);
        WritePixel(expected, x: 1, y: 1, red: 0, green: 0, blue: 255);
        WritePixel(expected, x: 2, y: 1, red: 255, green: 255, blue: 255);
        WritePixel(expected, x: 1, y: 2, red: 255, green: 0, blue: 0);
        WritePixel(expected, x: 2, y: 2, red: 0, green: 255, blue: 0);

        LogicalRenderSize logicalRenderSize = new(TargetWidth, TargetHeight);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(width: 2, height: 2, texturePixels);

        renderer.BeginFrame();
        renderer.DrawRepeatedSprite(texture, s_wrappedSource, x: 1, y: 0, width: 2, height: 3);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        FramebufferVerifier.VerifyExact("Sprite repeated out-of-range source", expected, actual);

        Console.WriteLine("Sprite repeated source: 2x2 texture, source (0,0)-(2,3), destination 2x3");
        Console.WriteLine($"Sprite repeated source SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static void RunDegenerateSourceCase(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize)
    {
        ReadOnlySpan<byte> texturePixels =
        [
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 255, 255,
        ];

        byte[] expected = FramebufferVerifier.CreateOpaqueBlack(TargetWidth, TargetHeight);

        for (int y = 1; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                WritePixel(expected, x, y, red: 255, green: 0, blue: 0);
            }
        }

        LogicalRenderSize logicalRenderSize = new(TargetWidth, TargetHeight);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(width: 2, height: 2, texturePixels);

        renderer.BeginFrame();
        renderer.DrawRepeatedSprite(texture, s_degenerateSource, x: 0, y: 1, width: 3, height: 2);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        FramebufferVerifier.VerifyExact("Sprite repeated degenerate source", expected, actual);

        Console.WriteLine("Sprite repeated degenerate source: source (0,0)-(0,0), destination 3x2");
        Console.WriteLine($"Sprite repeated degenerate source SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static void WritePixel(Span<byte> framebuffer, int x, int y, byte red, byte green, byte blue)
    {
        int offset = checked(((y * TargetWidth) + x) * 4);

        framebuffer[offset] = red;
        framebuffer[offset + 1] = green;
        framebuffer[offset + 2] = blue;
        framebuffer[offset + 3] = byte.MaxValue;
    }
}
