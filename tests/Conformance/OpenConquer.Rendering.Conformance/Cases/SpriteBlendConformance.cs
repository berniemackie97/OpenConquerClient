using OpenConquer.Platform;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class SpriteBlendConformance
{
    public static void Run(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        LogicalRenderSize logicalRenderSize = new(1, 1);
        ReadOnlySpan<byte> whitePixel = [byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue];
        ReadOnlySpan<byte> expected = [byte.MaxValue, 0, byte.MaxValue, byte.MaxValue];

        SpriteColor destinationBlue = new(0, 0, byte.MaxValue, byte.MaxValue);
        SpriteColor additiveRed = new(byte.MaxValue, 0, 0, 128);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(width: 1, height: 1, whitePixel);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, x: 0, y: 0, destinationBlue);
        renderer.DrawSprite(texture, x: 0, y: 0, additiveRed, SpriteBlendMode.Additive);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        FramebufferVerifier.VerifyExact("Sprite additive blending", expected, actual);

        Console.WriteLine($"Sprite additive blending: destination blue + red alpha {additiveRed.Alpha}, One/One");
        Console.WriteLine($"Sprite additive blending SHA256: {ConformanceHash.Sha256(actual)}");
    }
}
