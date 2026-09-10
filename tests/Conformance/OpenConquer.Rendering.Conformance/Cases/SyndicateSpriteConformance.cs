using OpenConquer.Content.Images;
using OpenConquer.Platform;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class SyndicateSpriteConformance
{
    private const string ExpectedRgb565FramebufferSha256 = "93939cf5e51ea6298b729836b80561627550505e6f0771588082c5a33142833c";
    private const string ExpectedRgb555FramebufferSha256 = "313ec6083e2eb72c7e3e63594859225c09bee573d155afa9e24d0399fd203ed7";

    private const int TargetWidth = 32;
    private const int TargetHeight = 32;
    private const int X = 7;
    private const int Y = 9;

    public static SyndicateSpriteBaseline Run(OpenGLGraphicsDevice graphicsDevice, RgbaImage image, PixelSize framebufferSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(image);

        LogicalRenderSize logicalRenderSize = new(TargetWidth, TargetHeight);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, X, Y);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        string framebufferHash = ConformanceHash.Sha256(framebuffer);

        string colorFormat = framebufferHash switch
        {
            ExpectedRgb565FramebufferSha256 => "RGB565",
            ExpectedRgb555FramebufferSha256 => "RGB555",
            _ => throw new InvalidDataException($"Syndicate framebuffer SHA256 {framebufferHash} does not match a verified retail-compatible 16-bit color layout. Expected RGB565 {ExpectedRgb565FramebufferSha256} or RGB555 {ExpectedRgb555FramebufferSha256}."),
        };

        Console.WriteLine($"Syndicate ANI frame: {image.Width}x{image.Height} at ({X}, {Y})");
        Console.WriteLine($"Syndicate logical target: {TargetWidth}x{TargetHeight}, {colorFormat}");
        Console.WriteLine($"Syndicate framebuffer SHA256: {framebufferHash}");

        return new SyndicateSpriteBaseline(framebuffer, colorFormat);
    }
}

internal readonly record struct SyndicateSpriteBaseline(byte[] Pixels, string ColorFormat);
