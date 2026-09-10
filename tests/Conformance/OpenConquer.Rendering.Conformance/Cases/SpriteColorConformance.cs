using OpenConquer.Content.Images;
using OpenConquer.Platform;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class SpriteColorConformance
{
    private const int SyndicateTargetWidth = 32;
    private const int SyndicateTargetHeight = 32;
    private const int SyndicateX = 7;
    private const int SyndicateY = 9;

    public static void Run(OpenGLGraphicsDevice graphicsDevice, RgbaImage syndicateImage, PixelSize framebufferSize, SyndicateSpriteBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(syndicateImage);

        RunExplicitWhiteCase(graphicsDevice, syndicateImage, framebufferSize, baseline);
        RunModulationCase(graphicsDevice, framebufferSize, baseline.ColorFormat);
    }

    private static void RunExplicitWhiteCase(OpenGLGraphicsDevice graphicsDevice, RgbaImage image, PixelSize framebufferSize, SyndicateSpriteBaseline baseline)
    {
        LogicalRenderSize logicalRenderSize = new(SyndicateTargetWidth, SyndicateTargetHeight);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, SyndicateX, SyndicateY, SpriteColor.White);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        FramebufferVerifier.VerifyExact("Syndicate explicit white modulation", baseline.Pixels, actual);

        Console.WriteLine($"Syndicate explicit white modulation SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static void RunModulationCase(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize, string colorFormat)
    {
        LogicalRenderSize logicalRenderSize = new(3, 1);
        ReadOnlySpan<byte> whitePixel = [byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue];
        SpriteColor color = new(byte.MaxValue, 128, 64, 128);
        SpriteSourceRectangle sourceRectangle = new(x: 0, y: 0, width: 1, height: 1);

        byte[] expected = colorFormat switch
        {
            "RGB565" => [132, 65, 33, byte.MaxValue, 132, 65, 33, byte.MaxValue, 132, 65, 33, byte.MaxValue],
            "RGB555" => [132, 66, 33, byte.MaxValue, 132, 66, 33, byte.MaxValue, 132, 66, 33, byte.MaxValue],
            _ => throw new ArgumentOutOfRangeException(nameof(colorFormat), colorFormat, "Unknown logical color format."),
        };

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(width: 1, height: 1, whitePixel);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, x: 0, y: 0, color);
        renderer.DrawSprite(texture, x: 1, y: 0, width: 1, height: 1, color);
        renderer.DrawSprite(texture, sourceRectangle, x: 2, y: 0, width: 1, height: 1, color);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        FramebufferVerifier.VerifyExact("Sprite RGBA modulation", expected, actual);

        Console.WriteLine($"Sprite RGBA modulation: ({color.Red}, {color.Green}, {color.Blue}, {color.Alpha}), {colorFormat}");
        Console.WriteLine($"Sprite RGBA modulation SHA256: {ConformanceHash.Sha256(actual)}");
    }
}
