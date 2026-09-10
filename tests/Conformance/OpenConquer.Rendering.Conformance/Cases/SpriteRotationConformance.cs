using OpenConquer.Platform;
using OpenConquer.Rendering.Conformance.Reference;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class SpriteRotationConformance
{
    private const int X = 2;
    private const int Y = 2;
    private const int Width = 4;
    private const int Height = 2;
    private const int Degrees = 1_440_000_090;

    private static readonly SpriteSourceRectangle s_source = new(x: 1, y: 0, width: 2, height: 1);

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        LogicalRenderSize logicalRenderSize = new(SpriteRotationReference.TargetWidth, SpriteRotationReference.TargetHeight);

        ReadOnlySpan<byte> texturePixels =
        [
            0, 0, byte.MaxValue, byte.MaxValue,
            byte.MaxValue, 0, 0, byte.MaxValue,
            0, byte.MaxValue, 0, byte.MaxValue,
            byte.MaxValue, byte.MaxValue, 0, byte.MaxValue,
        ];

        byte[] expected = SpriteRotationReference.CreateExpectedFramebuffer();

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(width: 4, height: 1, texturePixels);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, s_source, X, Y, Width, Height, SpriteColor.White, Degrees);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        FramebufferVerifier.VerifyExact("Sprite rotation", expected, actual);

        Console.WriteLine($"Sprite rotation: source ({s_source.X}, {s_source.Y}) {s_source.Width}x{s_source.Height} -> {Width}x{Height} at ({X}, {Y}), {Degrees} degrees, {colorFormat}");
        Console.WriteLine($"Sprite rotation SHA256: {ConformanceHash.Sha256(actual)}");
    }
}
