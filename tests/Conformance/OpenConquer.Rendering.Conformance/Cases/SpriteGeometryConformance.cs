using OpenConquer.Content.Images;
using OpenConquer.Platform;
using OpenConquer.Rendering.Conformance.Reference;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class SpriteGeometryConformance
{
    private const int TargetWidth = 32;
    private const int TargetHeight = 32;

    private const int StretchX = 2;
    private const int StretchY = 2;
    private const int StretchWidth = 20;
    private const int StretchHeight = 18;

    private const int CropX = 6;
    private const int CropY = 8;
    private const int CropWidth = 20;
    private const int CropHeight = 16;

    private static readonly SpriteSourceRectangle s_fullSource = new(x: 0, y: 0, width: 14, height: 14);
    private static readonly SpriteSourceRectangle s_cropSource = new(x: 1, y: 1, width: 12, height: 12);

    public static void Run(OpenGLGraphicsDevice graphicsDevice, RgbaImage image, PixelSize framebufferSize, SyndicateSpriteBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(image);

        RunWholeTextureStretchCase(graphicsDevice, image, framebufferSize, baseline);
        RunCropStretchCase(graphicsDevice, image, framebufferSize, baseline);
    }

    private static void RunWholeTextureStretchCase(OpenGLGraphicsDevice graphicsDevice, RgbaImage image, PixelSize framebufferSize, SyndicateSpriteBaseline baseline)
    {
        LogicalRenderSize logicalRenderSize = new(TargetWidth, TargetHeight);
        byte[] expected = SyndicateGeometryReference.ComposeNearest(baseline.Pixels, s_fullSource, StretchX, StretchY, StretchWidth, StretchHeight);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, StretchX, StretchY, StretchWidth, StretchHeight);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        FramebufferVerifier.VerifyExact("Syndicate whole-texture stretch", expected, actual);

        Console.WriteLine($"Syndicate whole-texture stretch: {image.Width}x{image.Height} -> {StretchWidth}x{StretchHeight} at ({StretchX}, {StretchY}), {baseline.ColorFormat}");
        Console.WriteLine($"Syndicate whole-texture stretch SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static void RunCropStretchCase(OpenGLGraphicsDevice graphicsDevice, RgbaImage image, PixelSize framebufferSize, SyndicateSpriteBaseline baseline)
    {
        LogicalRenderSize logicalRenderSize = new(TargetWidth, TargetHeight);
        byte[] expected = SyndicateGeometryReference.ComposeNearest(baseline.Pixels, s_cropSource, CropX, CropY, CropWidth, CropHeight);
        byte[] wholeTextureExpected = SyndicateGeometryReference.ComposeNearest(baseline.Pixels, s_fullSource, StretchX, StretchY, StretchWidth, StretchHeight);

        if (expected.AsSpan().SequenceEqual(wholeTextureExpected))
        {
            throw new InvalidDataException("The Syndicate crop/stretch fixture does not produce a framebuffer distinct from the whole-texture stretch fixture.");
        }

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, s_cropSource, CropX, CropY, CropWidth, CropHeight);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        FramebufferVerifier.VerifyExact("Syndicate source-crop stretch", expected, actual);

        Console.WriteLine($"Syndicate source crop: ({s_cropSource.X}, {s_cropSource.Y}) {s_cropSource.Width}x{s_cropSource.Height} -> {CropWidth}x{CropHeight} at ({CropX}, {CropY}), {baseline.ColorFormat}");
        Console.WriteLine($"Syndicate source-crop stretch SHA256: {ConformanceHash.Sha256(actual)}");
    }
}
