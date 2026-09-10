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

    private const int SyndicateStretchX = 2;
    private const int SyndicateStretchY = 2;
    private const int SyndicateStretchWidth = 20;
    private const int SyndicateStretchHeight = 18;

    private const int SyndicateCropX = 6;
    private const int SyndicateCropY = 8;
    private const int SyndicateCropWidth = 20;
    private const int SyndicateCropHeight = 16;

    private const int RotationTargetWidth = 8;
    private const int RotationTargetHeight = 6;
    private const int RotationX = 2;
    private const int RotationY = 2;
    private const int RotationWidth = 4;
    private const int RotationHeight = 2;
    private const int RotationDegrees = 1_440_000_090;

    private static readonly SpriteSourceRectangle s_syndicateFullSource = new(x: 0, y: 0, SyndicateFrameWidth, SyndicateFrameHeight);
    private static readonly SpriteSourceRectangle s_syndicateCropSource = new(x: 1, y: 1, width: 12, height: 12);
    private static readonly SpriteSourceRectangle s_rotationSource = new(x: 1, y: 0, width: 2, height: 1);

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

            SyndicateFramebufferBaseline baseline = RunSyndicateNaturalCase(graphicsDevice, syndicateImage, framebufferSize);
            RunSyndicateExplicitWhiteColorCase(graphicsDevice, syndicateImage, framebufferSize, baseline);
            RunSpriteColorModulationCase(graphicsDevice, framebufferSize, baseline.ColorFormat);
            RunSpriteAdditiveBlendCase(graphicsDevice, framebufferSize);
            RunSyndicateWholeTextureStretchCase(graphicsDevice, syndicateImage, framebufferSize, baseline);
            RunSyndicateCropStretchCase(graphicsDevice, syndicateImage, framebufferSize, baseline);
            RunSpriteRotationCase(graphicsDevice, framebufferSize, baseline.ColorFormat);

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

        Console.WriteLine("OpenGL render-target, presentation, ANI asset, sprite geometry, sprite color, sprite blending, and sprite rotation conformance passed.");

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

    private static SyndicateFramebufferBaseline RunSyndicateNaturalCase(OpenGLGraphicsDevice graphicsDevice, RgbaImage image, PixelSize framebufferSize)
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
            _ => throw new InvalidDataException($"Syndicate framebuffer SHA256 {framebufferHash} does not match a verified retail-compatible 16-bit color layout. Expected RGB565 {ExpectedRgb565FramebufferSha256} or RGB555 {ExpectedRgb555FramebufferSha256}."),
        };

        Console.WriteLine($"Syndicate ANI frame: {image.Width}x{image.Height} at ({SyndicateX}, {SyndicateY})");
        Console.WriteLine($"Syndicate logical target: {SyndicateTargetWidth}x{SyndicateTargetHeight}, {colorFormat}");
        Console.WriteLine($"Syndicate framebuffer SHA256: {framebufferHash}");

        return new SyndicateFramebufferBaseline(framebuffer, colorFormat);
    }

    private static void RunSyndicateExplicitWhiteColorCase(OpenGLGraphicsDevice graphicsDevice, RgbaImage image, PixelSize framebufferSize, SyndicateFramebufferBaseline baseline)
    {
        LogicalRenderSize logicalRenderSize = new(SyndicateTargetWidth, SyndicateTargetHeight);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, SyndicateX, SyndicateY, SpriteColor.White);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        VerifyExactFramebuffer("Syndicate explicit white modulation", baseline.Pixels, actual);

        Console.WriteLine($"Syndicate explicit white modulation SHA256: {ToLowerHex(SHA256.HashData(actual))}");
    }

    private static void RunSpriteColorModulationCase(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize, string colorFormat)
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

        VerifyExactFramebuffer("Sprite RGBA modulation", expected, actual);

        Console.WriteLine($"Sprite RGBA modulation: ({color.Red}, {color.Green}, {color.Blue}, {color.Alpha}), {colorFormat}");
        Console.WriteLine($"Sprite RGBA modulation SHA256: {ToLowerHex(SHA256.HashData(actual))}");
    }

    private static void RunSpriteAdditiveBlendCase(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize)
    {
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

        VerifyExactFramebuffer("Sprite additive blending", expected, actual);

        Console.WriteLine($"Sprite additive blending: destination blue + red alpha {additiveRed.Alpha}, One/One");
        Console.WriteLine($"Sprite additive blending SHA256: {ToLowerHex(SHA256.HashData(actual))}");
    }

    private static void RunSyndicateWholeTextureStretchCase(OpenGLGraphicsDevice graphicsDevice, RgbaImage image, PixelSize framebufferSize, SyndicateFramebufferBaseline baseline)
    {
        LogicalRenderSize logicalRenderSize = new(SyndicateTargetWidth, SyndicateTargetHeight);
        byte[] expected = ComposeNearestSyndicateFramebuffer(baseline.Pixels, s_syndicateFullSource, SyndicateStretchX, SyndicateStretchY, SyndicateStretchWidth, SyndicateStretchHeight);

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, SyndicateStretchX, SyndicateStretchY, SyndicateStretchWidth, SyndicateStretchHeight);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        VerifyExactFramebuffer("Syndicate whole-texture stretch", expected, actual);

        Console.WriteLine($"Syndicate whole-texture stretch: {image.Width}x{image.Height} -> {SyndicateStretchWidth}x{SyndicateStretchHeight} at ({SyndicateStretchX}, {SyndicateStretchY}), {baseline.ColorFormat}");
        Console.WriteLine($"Syndicate whole-texture stretch SHA256: {ToLowerHex(SHA256.HashData(actual))}");
    }

    private static void RunSyndicateCropStretchCase(OpenGLGraphicsDevice graphicsDevice, RgbaImage image, PixelSize framebufferSize, SyndicateFramebufferBaseline baseline)
    {
        LogicalRenderSize logicalRenderSize = new(SyndicateTargetWidth, SyndicateTargetHeight);
        byte[] expected = ComposeNearestSyndicateFramebuffer(baseline.Pixels, s_syndicateCropSource, SyndicateCropX, SyndicateCropY, SyndicateCropWidth, SyndicateCropHeight);
        byte[] wholeTextureExpected = ComposeNearestSyndicateFramebuffer(baseline.Pixels, s_syndicateFullSource, SyndicateStretchX, SyndicateStretchY, SyndicateStretchWidth, SyndicateStretchHeight);

        if (expected.AsSpan().SequenceEqual(wholeTextureExpected))
        {
            throw new InvalidDataException("The Syndicate crop/stretch fixture does not produce a framebuffer distinct from the whole-texture stretch fixture.");
        }

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, s_syndicateCropSource, SyndicateCropX, SyndicateCropY, SyndicateCropWidth, SyndicateCropHeight);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        VerifyExactFramebuffer("Syndicate source-crop stretch", expected, actual);

        Console.WriteLine($"Syndicate source crop: ({s_syndicateCropSource.X}, {s_syndicateCropSource.Y}) {s_syndicateCropSource.Width}x{s_syndicateCropSource.Height} -> {SyndicateCropWidth}x{SyndicateCropHeight} at ({SyndicateCropX}, {SyndicateCropY}), {baseline.ColorFormat}");
        Console.WriteLine($"Syndicate source-crop stretch SHA256: {ToLowerHex(SHA256.HashData(actual))}");
    }

    private static void RunSpriteRotationCase(OpenGLGraphicsDevice graphicsDevice, PixelSize framebufferSize, string colorFormat)
    {
        LogicalRenderSize logicalRenderSize = new(RotationTargetWidth, RotationTargetHeight);

        ReadOnlySpan<byte> texturePixels =
        [
            0, 0, byte.MaxValue, byte.MaxValue,
            byte.MaxValue, 0, 0, byte.MaxValue,
            0, byte.MaxValue, 0, byte.MaxValue,
            byte.MaxValue, byte.MaxValue, 0, byte.MaxValue,
        ];

        byte[] expected = CreateSpriteRotationExpectedFramebuffer();

        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(width: 4, height: 1, texturePixels);

        renderer.BeginFrame();
        renderer.DrawSprite(texture, s_rotationSource, RotationX, RotationY, RotationWidth, RotationHeight, SpriteColor.White, RotationDegrees);
        byte[] actual = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        VerifyExactFramebuffer("Sprite rotation", expected, actual);

        Console.WriteLine($"Sprite rotation: source ({s_rotationSource.X}, {s_rotationSource.Y}) {s_rotationSource.Width}x{s_rotationSource.Height} -> {RotationWidth}x{RotationHeight} at ({RotationX}, {RotationY}), {RotationDegrees} degrees, {colorFormat}");
        Console.WriteLine($"Sprite rotation SHA256: {ToLowerHex(SHA256.HashData(actual))}");
    }

    private static byte[] ComposeNearestSyndicateFramebuffer(ReadOnlySpan<byte> verifiedNaturalFramebuffer, SpriteSourceRectangle sourceRectangle, int destinationX, int destinationY, int destinationWidth, int destinationHeight)
    {
        int expectedFramebufferLength = checked(SyndicateTargetWidth * SyndicateTargetHeight * 4);

        if (verifiedNaturalFramebuffer.Length != expectedFramebufferLength)
        {
            throw new ArgumentException($"Expected a {SyndicateTargetWidth}x{SyndicateTargetHeight} RGBA framebuffer.", nameof(verifiedNaturalFramebuffer));
        }

        long sourceRight = (long)sourceRectangle.X + sourceRectangle.Width;
        long sourceBottom = (long)sourceRectangle.Y + sourceRectangle.Height;

        if (sourceRectangle.X < 0 || sourceRectangle.Y < 0 || sourceRectangle.Width <= 0 || sourceRectangle.Height <= 0 || sourceRight > SyndicateFrameWidth || sourceBottom > SyndicateFrameHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRectangle), sourceRectangle, "The Syndicate oracle source rectangle must fit within the verified frame.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(destinationX);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(destinationWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(destinationHeight);

        long destinationRight = (long)destinationX + destinationWidth;
        long destinationBottom = (long)destinationY + destinationHeight;

        if (destinationRight > SyndicateTargetWidth || destinationBottom > SyndicateTargetHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationWidth), "The Syndicate oracle destination must fit within the logical target.");
        }

        byte[] expected = CreateOpaqueBlackFramebuffer(SyndicateTargetWidth, SyndicateTargetHeight);

        for (int destinationOffsetY = 0; destinationOffsetY < destinationHeight; destinationOffsetY++)
        {
            int sourceOffsetY = GetNearestSourceOffset(destinationOffsetY, destinationHeight, sourceRectangle.Height);
            int verifiedY = SyndicateY + sourceRectangle.Y + sourceOffsetY;

            for (int destinationOffsetX = 0; destinationOffsetX < destinationWidth; destinationOffsetX++)
            {
                int sourceOffsetX = GetNearestSourceOffset(destinationOffsetX, destinationWidth, sourceRectangle.Width);
                int verifiedX = SyndicateX + sourceRectangle.X + sourceOffsetX;
                int sourceOffset = ((verifiedY * SyndicateTargetWidth) + verifiedX) * 4;

                int outputX = destinationX + destinationOffsetX;
                int outputY = destinationY + destinationOffsetY;
                int destinationOffset = ((outputY * SyndicateTargetWidth) + outputX) * 4;

                verifiedNaturalFramebuffer.Slice(sourceOffset, 4).CopyTo(expected.AsSpan(destinationOffset, 4));
            }
        }

        return expected;
    }

    private static byte[] CreateSpriteRotationExpectedFramebuffer()
    {
        byte[] expected = CreateOpaqueBlackFramebuffer(RotationTargetWidth, RotationTargetHeight);

        for (int y = 1; y < 5; y++)
        {
            bool red = y < 3;

            for (int x = 3; x < 5; x++)
            {
                int offset = ((y * RotationTargetWidth) + x) * 4;

                expected[offset] = red ? byte.MaxValue : (byte)0;
                expected[offset + 1] = red ? (byte)0 : byte.MaxValue;
                expected[offset + 2] = 0;
            }
        }

        return expected;
    }

    private static int GetNearestSourceOffset(int destinationOffset, int destinationExtent, int sourceExtent)
    {
        long sourceNumerator = (2L * destinationOffset + 1) * sourceExtent;
        long sourceDenominator = 2L * destinationExtent;

        return (int)(sourceNumerator / sourceDenominator);
    }

    private static byte[] CreateOpaqueBlackFramebuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        byte[] framebuffer = new byte[checked(width * height * 4)];

        for (int offset = 0; offset < framebuffer.Length; offset += 4)
        {
            framebuffer[offset + 3] = byte.MaxValue;
        }

        return framebuffer;
    }

    private static void VerifyExactFramebuffer(string caseName, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        if (actual.SequenceEqual(expected))
        {
            return;
        }

        string expectedHash = ToLowerHex(SHA256.HashData(expected));
        string actualHash = ToLowerHex(SHA256.HashData(actual));

        throw new InvalidDataException($"{caseName} framebuffer SHA256 {actualHash} does not match the independently composed expected framebuffer {expectedHash}.");
    }

    private static string ToLowerHex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private readonly record struct SyndicateFramebufferBaseline(byte[] Pixels, string ColorFormat);
}
