using System.Security.Cryptography;
using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Platform;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Rendering.Conformance;

internal static class Program
{
    private const string AniContentPath = "ani/Common.Ani";
    private const string FrameContentPath = "data/pic/Syndicate.tga";
    private const string SectionName = "Syndicate";

    private const string ExpectedEncodedFrameSha256 = "a813875f120d20908e13c5cdb4410008d5ff1b6f2d6f9186051185f7aa331b3a";
    private const string ExpectedDecodedRgbaSha256 = "1e112db318ecd33cba4b2980d0ed92e502e74bcd0e6a539747733cad718f8c37";
    private const string ExpectedRgb565FramebufferSha256 = "93939cf5e51ea6298b729836b80561627550505e6f0771588082c5a33142833c";
    private const string ExpectedRgb555FramebufferSha256 = "313ec6083e2eb72c7e3e63594859225c09bee573d155afa9e24d0399fd203ed7";

    private const int SyndicateFrameWidth = 14;
    private const int SyndicateFrameHeight = 14;
    private const int SyndicateTargetWidth = 32;
    private const int SyndicateTargetHeight = 32;
    private const int SyndicateX = 7;
    private const int SyndicateY = 9;

    private static readonly LogicalRenderSize[] s_logicalRenderSizes =
    [
        new(800, 600),
        new(1024, 768),
    ];

    private static int Main(string[] args)
    {
        string contentRoot = ParseContentRoot(args);
        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(contentRoot);
        RgbaImage syndicateImage = LoadVerifiedSyndicateImage(contentSource);

        OpenGLGraphicsDevice? graphicsDevice = null;
        bool contextReady = false;
        bool frameRendered = false;
        bool contextReleased = false;

        using StartupWindow window = new(new PixelSize(1280, 720));

        window.OpenGLContextReady += context =>
        {
            if (contextReady)
            {
                throw new InvalidOperationException("The OpenGL context-ready callback was raised more than once.");
            }

            graphicsDevice = new OpenGLGraphicsDevice(context.GetProcAddress);
            contextReady = true;

            Console.WriteLine($"OpenGL version: {graphicsDevice.Version}");
            Console.WriteLine($"GLSL version: {graphicsDevice.ShadingLanguageVersion}");
            Console.WriteLine($"OpenGL vendor: {graphicsDevice.Vendor}");
            Console.WriteLine($"OpenGL renderer: {graphicsDevice.Renderer}");
        };

        window.Rendering += metrics =>
        {
            if (!contextReady || graphicsDevice is null)
            {
                throw new InvalidOperationException("Rendering began before the OpenGL graphics device was ready.");
            }

            if (frameRendered)
            {
                throw new InvalidOperationException("The conformance window rendered more than one frame.");
            }

            PixelSize framebufferSize = metrics.FramebufferSize;

            if (framebufferSize.Width <= 0 || framebufferSize.Height <= 0)
            {
                throw new InvalidOperationException($"The native window reported an invalid framebuffer size of {framebufferSize.Width}x{framebufferSize.Height}.");
            }

            foreach (LogicalRenderSize logicalRenderSize in s_logicalRenderSizes)
            {
                RunPresentationCase(graphicsDevice, logicalRenderSize, framebufferSize);
            }

            RunSyndicateCase(graphicsDevice, syndicateImage, framebufferSize);
            frameRendered = true;
        };

        window.OpenGLContextReleasing += () =>
        {
            if (!contextReady)
            {
                throw new InvalidOperationException("The OpenGL context began releasing before it became ready.");
            }

            if (contextReleased)
            {
                throw new InvalidOperationException("The OpenGL context-release callback was raised more than once.");
            }

            graphicsDevice?.Dispose();
            graphicsDevice = null;
            contextReleased = true;
        };

        window.ShowAndRender();

        if (!frameRendered)
        {
            throw new InvalidOperationException("The production OpenGL renderer did not render the conformance cases.");
        }

        window.Dispose();

        if (!contextReleased)
        {
            throw new InvalidOperationException("The OpenGL context was not released through the production lifetime boundary.");
        }

        Console.WriteLine("OpenGL render-target, presentation, and ANI asset conformance passed.");

        return 0;
    }

    private static string ParseContentRoot(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length != 2 || !string.Equals(args[0], "--content-root", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(args[1]))
        {
            throw new ArgumentException("Usage: OpenConquer.Rendering.Conformance --content-root <retail-5517-root>");
        }

        string contentRoot = Path.GetFullPath(args[1]);

        if (!Directory.Exists(contentRoot))
        {
            throw new DirectoryNotFoundException($"Retail 5517 content root '{contentRoot}' does not exist.");
        }

        return contentRoot;
    }

    private static RgbaImage LoadVerifiedSyndicateImage(PackagedClientContentSource contentSource)
    {
        AniIndexSection section = AniIndexFile.Load(contentSource, AniContentPath, ContentLookupMode.LooseThenPackage).GetRequiredSection(SectionName);

        if (section.FrameCount != 1)
        {
            throw new InvalidDataException($"ANI section [{SectionName}] contains {section.FrameCount} frame(s); verified retail 5517 requires exactly one.");
        }

        if (!string.Equals(section.FramePaths[0], FrameContentPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"ANI section [{SectionName}] references '{section.FramePaths[0]}'; verified retail 5517 requires '{FrameContentPath}'.");
        }

        using (Stream frameStream = contentSource.OpenRequiredRead(FrameContentPath, ContentLookupMode.LooseThenPackage))
        {
            string encodedHash = ToLowerHex(SHA256.HashData(frameStream));

            if (!string.Equals(encodedHash, ExpectedEncodedFrameSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Retail frame '{FrameContentPath}' has SHA256 {encodedHash}; expected {ExpectedEncodedFrameSha256}.");
            }
        }

        RgbaImage image = AniFrameLoader.Load(contentSource, AniContentPath, SectionName, frameIndex: 0, ContentLookupMode.LooseThenPackage);

        if (image.Width != SyndicateFrameWidth || image.Height != SyndicateFrameHeight)
        {
            throw new InvalidDataException($"Retail Syndicate frame decoded as {image.Width}x{image.Height}; expected {SyndicateFrameWidth}x{SyndicateFrameHeight}.");
        }

        string decodedHash = ToLowerHex(SHA256.HashData(image.Pixels.Span));

        if (!string.Equals(decodedHash, ExpectedDecodedRgbaSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Retail Syndicate frame decoded to RGBA SHA256 {decodedHash}; expected {ExpectedDecodedRgbaSha256}.");
        }

        return image;
    }

    private static void RunPresentationCase(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.RenderFrame();

        PresentationViewport viewport = renderer.Viewport;

        Console.WriteLine($"Logical target: {logicalRenderSize.Width}x{logicalRenderSize.Height}");
        Console.WriteLine($"Host framebuffer: {framebufferSize.Width}x{framebufferSize.Height}");
        Console.WriteLine($"Presentation viewport: {viewport.Width}x{viewport.Height} at ({viewport.OffsetX}, {viewport.OffsetY}), {viewport.Filter}");
    }

    private static void RunSyndicateCase(OpenGLGraphicsDevice graphicsDevice, RgbaImage image, PixelSize framebufferSize)
    {
        LogicalRenderSize logicalRenderSize = new(SyndicateTargetWidth, SyndicateTargetHeight);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, SyndicateX, SyndicateY);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        string framebufferHash = ToLowerHex(SHA256.HashData(framebuffer));

        string colorFormat = framebufferHash switch
        {
            ExpectedRgb565FramebufferSha256 => "RGB565",
            ExpectedRgb555FramebufferSha256 => "RGB555",
            _ => throw new InvalidDataException(
                $"Syndicate framebuffer SHA256 {framebufferHash} does not match a verified retail-compatible 16-bit color layout. " +
                $"Expected RGB565 {ExpectedRgb565FramebufferSha256} or RGB555 {ExpectedRgb555FramebufferSha256}."),
        };

        Console.WriteLine($"Syndicate ANI frame: {image.Width}x{image.Height} at ({SyndicateX}, {SyndicateY})");
        Console.WriteLine($"Syndicate logical target: {SyndicateTargetWidth}x{SyndicateTargetHeight}, {colorFormat}");
        Console.WriteLine($"Syndicate framebuffer SHA256: {framebufferHash}");
    }

    private static string ToLowerHex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
