using OpenConquer.Client.UI.Hud;
using OpenConquer.Content;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Reference;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class MainHudChromeConformance
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string ProgressBackgroundPath = "data/main/ProgressBk.dds";
    private const string DialogFrame0Path = "data/main/mainDialog1.dds";
    private const string DialogFrame1Path = "data/main/mainDialog2.dds";

    private const string ExpectedControlAniSha256 = "a1db47baaeb2f75eea3b5216378f97d713c2afeaefe05ec02e6f584794532d27";
    private const string ExpectedProgressBackgroundSha256 = "9b91a28e0170142a48dc03332691959eca01c179b9d8c592aa5b11af4f5d966c";
    private const string ExpectedDialogFrame0Sha256 = "505a4655c398e41bd25698b57caa50f48376cff713e1c8c6f287a03866038fe1";
    private const string ExpectedDialogFrame1Sha256 = "818d13f62509ac859fb0ed72d36ec6aeb2b12f9ed3dee3c6172171d99ba86f41";

    private static readonly LogicalRenderSize[] s_logicalRenderSizes = [new(800, 600), new(1024, 768)];

    private static readonly SpriteSourceRectangle s_dialogPanelASource = new(0, 112, 256, 144);
    private static readonly SpriteSourceRectangle s_dialogPanelBSource = new(0, 0, 256, 54);
    private static readonly SpriteSourceRectangle s_dialogPanelCSource = new(0, 0, 256, 54);
    private static readonly SpriteSourceRectangle s_dialogPanelDSource = new(0, 64, 256, 54);

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PackagedClientContentSource contentSource, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        _ = ReadLooseAsset(contentSource, ControlAniPath, ExpectedControlAniSha256);

        byte[] progressBackgroundDds = ReadPackageOnlyAsset(contentSource, ProgressBackgroundPath, ExpectedProgressBackgroundSha256);
        byte[] dialogFrame0Dds = ReadPackageOnlyAsset(contentSource, DialogFrame0Path, ExpectedDialogFrame0Sha256);
        byte[] dialogFrame1Dds = ReadLooseAsset(contentSource, DialogFrame1Path, ExpectedDialogFrame1Sha256);

        byte[] progressBackgroundReference = Dxt3ReferenceDecoder.Decode(progressBackgroundDds, expectedWidth: 256, expectedHeight: 256);
        byte[] dialogFrame0Reference = Dxt3ReferenceDecoder.Decode(dialogFrame0Dds, expectedWidth: 256, expectedHeight: 256);
        byte[] dialogFrame1Reference = Dxt3ReferenceDecoder.Decode(dialogFrame1Dds, expectedWidth: 256, expectedHeight: 128);

        MainHudChromeAssets assets = MainHudChromeAssets.Load(contentSource);

        VerifyProductionDecode("Progress45 frame 0", assets.ProgressBackground?.Pixels, progressBackgroundReference);
        VerifyProductionDecode("Dialog4 frame 0", assets.DialogFrame0?.Pixels, dialogFrame0Reference);
        VerifyProductionDecode("Dialog4 frame 1", assets.DialogFrame1?.Pixels, dialogFrame1Reference);

        if (!assets.HasDialogPanels)
        {
            throw new InvalidDataException("Verified retail Dialog4 assets did not produce an available main-HUD panel set.");
        }

        foreach (LogicalRenderSize logicalRenderSize in s_logicalRenderSizes)
        {
            RunRenderCase(graphicsDevice, assets, logicalRenderSize, framebufferSize, colorFormat, progressBackgroundReference, dialogFrame0Reference, dialogFrame1Reference);
        }
    }

    private static void RunRenderCase(OpenGLGraphicsDevice graphicsDevice, MainHudChromeAssets assets, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, string colorFormat,
        ReadOnlySpan<byte> progressBackground, ReadOnlySpan<byte> dialogFrame0, ReadOnlySpan<byte> dialogFrame1)
    {
        byte[] actual = RenderProductionHud(graphicsDevice, assets, logicalRenderSize, framebufferSize);
        byte[] expected = RenderNativeReference(graphicsDevice, logicalRenderSize, framebufferSize, progressBackground, dialogFrame0, dialogFrame1);

        FramebufferVerifier.VerifyExact($"Main HUD chrome {logicalRenderSize.Width}x{logicalRenderSize.Height}", expected, actual);

        Console.WriteLine($"Main HUD chrome: {logicalRenderSize.Width}x{logicalRenderSize.Height}, {colorFormat}");
        Console.WriteLine($"Main HUD chrome framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static byte[] RenderProductionHud(OpenGLGraphicsDevice graphicsDevice, MainHudChromeAssets assets, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using MainHudChromeRenderer hudRenderer = new(graphicsDevice, assets, logicalRenderSize);

        renderer.BeginFrame();
        hudRenderer.DrawBackground(renderer);

        if (!hudRenderer.DrawPanels(renderer))
        {
            throw new InvalidDataException("Verified retail Dialog4 assets became unavailable during production HUD rendering.");
        }

        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static byte[] RenderNativeReference(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize,
        ReadOnlySpan<byte> progressBackground, ReadOnlySpan<byte> dialogFrame0, ReadOnlySpan<byte> dialogFrame1)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTexture2D progressTexture = graphicsDevice.CreateTexture2D(width: 256, height: 256, progressBackground);
        using OpenGLTexture2D dialogFrame0Texture = graphicsDevice.CreateTexture2D(width: 256, height: 256, dialogFrame0);
        using OpenGLTexture2D dialogFrame1Texture = graphicsDevice.CreateTexture2D(width: 256, height: 128, dialogFrame1);

        int originY = logicalRenderSize.Height - 141;

        renderer.BeginFrame();

        renderer.DrawSprite(progressTexture, x: 0, y: originY);
        renderer.DrawSprite(dialogFrame0Texture, s_dialogPanelASource, x: 0, y: originY - 3, width: 256, height: 144);
        renderer.DrawSprite(dialogFrame0Texture, s_dialogPanelBSource, x: 256, y: originY + 88, width: 256, height: 54);
        renderer.DrawSprite(dialogFrame1Texture, s_dialogPanelCSource, x: 512, y: originY + 88, width: 256, height: 54);
        renderer.DrawSprite(dialogFrame1Texture, s_dialogPanelDSource, x: 768, y: originY + 88, width: 256, height: 54);

        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static byte[] ReadPackageOnlyAsset(PackagedClientContentSource contentSource, string contentPath, string expectedSha256)
    {
        if (contentSource.TryOpenRead(contentPath, ContentLookupMode.LooseOnly, out Stream? unexpectedLoose))
        {
            unexpectedLoose.Dispose();
            throw new InvalidDataException($"Retail HUD asset '{contentPath}' unexpectedly resolves as a loose file; verified 5517 requires package-backed lookup.");
        }

        using Stream stream = contentSource.OpenRequiredRead(contentPath, ContentLookupMode.PackageOnly);
        byte[] bytes = ReadAllBytes(stream);

        VerifyEncodedHash(contentPath, bytes, expectedSha256);

        return bytes;
    }

    private static byte[] ReadLooseAsset(PackagedClientContentSource contentSource, string contentPath, string expectedSha256)
    {
        using Stream stream = contentSource.OpenRequiredRead(contentPath, ContentLookupMode.LooseOnly);
        byte[] bytes = ReadAllBytes(stream);

        VerifyEncodedHash(contentPath, bytes, expectedSha256);

        return bytes;
    }

    private static void VerifyEncodedHash(string contentPath, ReadOnlySpan<byte> bytes, string expectedSha256)
    {
        string actualSha256 = ConformanceHash.Sha256(bytes);

        if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Retail HUD asset '{contentPath}' has SHA256 {actualSha256}; expected {expectedSha256}.");
        }
    }

    private static void VerifyProductionDecode(string assetName, ReadOnlyMemory<byte>? productionPixels, ReadOnlySpan<byte> referencePixels)
    {
        if (!productionPixels.HasValue)
        {
            throw new InvalidDataException($"{assetName} was unavailable through the production HUD asset loader.");
        }

        ReadOnlySpan<byte> pixels = productionPixels.Value.Span;

        if (!pixels.SequenceEqual(referencePixels))
        {
            throw new InvalidDataException($"{assetName} production RGBA SHA256 {ConformanceHash.Sha256(pixels)} does not match independent DXT3 reference SHA256 {ConformanceHash.Sha256(referencePixels)}.");
        }
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
