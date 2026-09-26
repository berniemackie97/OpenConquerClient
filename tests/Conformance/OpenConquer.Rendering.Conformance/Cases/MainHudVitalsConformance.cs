using System.Runtime.ExceptionServices;
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

internal static class MainHudVitalsConformance
{
    private const string ControlAniPath = "ani/Control.ani";

    private static readonly LogicalRenderSize[] s_logicalRenderSizes = [new(800, 600), new(1024, 768)];

    private static readonly GaugeCase[] s_cases =
    [
        new("HP normal dual", CaseKind.LifeNormalDual, new(80, 40, 100, 0, 0, 0, 0, 0, false), ForceLifeNormal: true),
        new("HP startup frame 2", CaseKind.LifeStartupAlternate, new(50, 60, 100, 0, 0, 100, 0, 0, false)),
        new("MP normal dual", CaseKind.ManaNormalDual, new(0, 0, 0, 160, 80, 200, 0, 0, false)),
        new("MP frame 2", CaseKind.ManaAlternate, new(0, 0, 0, 80, 40, 100, 0, 0, false), ManaAlternate: true),
        new("stamina", CaseKind.Stamina, new(0, 0, 0, 0, 0, 0, 50, 100, false)),
        new("overflow 25", CaseKind.Overflow25, new(0, 0, 0, 0, 0, 0, 125, 150, true)),
        new("overflow 50", CaseKind.Overflow50, new(0, 0, 0, 0, 0, 0, 150, 150, true)),
        new("overflow positive zero-pixel", CaseKind.OverflowPositiveZeroPixel, new(0, 0, 0, 0, 0, 0, 101, 150, true)),
    ];

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PackagedClientContentSource contentSource, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        _ = ReadLooseOnlyAsset(contentSource, ControlAniPath, "a1db47baaeb2f75eea3b5216378f97d713c2afeaefe05ec02e6f584794532d27");

        ReferenceImages referenceImages = ReferenceImages.Load(contentSource);
        MainHudVitalsAssets assets = MainHudVitalsAssets.Load(contentSource);

        VerifyProductionAssets(assets, referenceImages);

        using ReferenceTextures referenceTextures = new(graphicsDevice, referenceImages);

        foreach (LogicalRenderSize logicalRenderSize in s_logicalRenderSizes)
        {
            using MainHudVitalsRenderer vitalsRenderer = new(graphicsDevice, assets, logicalRenderSize);

            foreach (GaugeCase testCase in s_cases)
            {
                RunRenderCase(graphicsDevice, vitalsRenderer, referenceTextures, logicalRenderSize, framebufferSize, colorFormat, testCase);
            }
        }
    }

    private static void RunRenderCase(OpenGLGraphicsDevice graphicsDevice, MainHudVitalsRenderer vitalsRenderer, ReferenceTextures referenceTextures, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, string colorFormat, GaugeCase testCase)
    {
        MainHudVitalsState state = CreateState(testCase);
        byte[] actual = RenderProduction(graphicsDevice, vitalsRenderer, state, logicalRenderSize, framebufferSize);
        byte[] expected = RenderReference(graphicsDevice, referenceTextures, logicalRenderSize, framebufferSize, testCase.Kind);

        FramebufferVerifier.VerifyExact($"Main HUD vitals {testCase.Name} {logicalRenderSize.Width}x{logicalRenderSize.Height}", expected, actual);

        Console.WriteLine($"Main HUD vitals: {testCase.Name}, {logicalRenderSize.Width}x{logicalRenderSize.Height}, {colorFormat}");
        Console.WriteLine($"Main HUD vitals framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static MainHudVitalsState CreateState(GaugeCase testCase)
    {
        MainHudVitalsState state = new();

        if (testCase.ForceLifeNormal)
        {
            state.SetSnapshot(new MainHudVitalsSnapshot(0, 0, 0, 1, 1, 1, 0, 0, false));
        }

        if (testCase.ManaAlternate)
        {
            state.SetManaAlternateSubVariant(enabled: true);
        }

        state.SetSnapshot(testCase.Snapshot);

        return state;
    }

    private static byte[] RenderProduction(OpenGLGraphicsDevice graphicsDevice, MainHudVitalsRenderer vitalsRenderer, MainHudVitalsState state, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();
        vitalsRenderer.Draw(renderer, state);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static byte[] RenderReference(OpenGLGraphicsDevice graphicsDevice, ReferenceTextures textures, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, CaseKind kind)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        int originY = logicalRenderSize.Height - 141;

        renderer.BeginFrame();

        switch (kind)
        {
            case CaseKind.LifeNormalDual:
                Draw(renderer, textures.Life1, left: 0, top: 15, right: 36, bottom: 74, x: 4, y: originY + 69, width: 36, height: 59);
                Draw(renderer, textures.Life0, left: 0, top: 44, right: 36, bottom: 74, x: 4, y: originY + 98, width: 36, height: 30);
                break;

            case CaseKind.LifeStartupAlternate:
                Draw(renderer, textures.Life2, left: 0, top: 37, right: 86, bottom: 74, x: 4, y: originY + 91, width: 86, height: 37);
                break;

            case CaseKind.ManaNormalDual:
                Draw(renderer, textures.Mana1, left: 0, top: 15, right: 34, bottom: 74, x: 52, y: originY + 69, width: 34, height: 59);
                Draw(renderer, textures.Mana0, left: 0, top: 44, right: 34, bottom: 74, x: 52, y: originY + 98, width: 34, height: 30);
                break;

            case CaseKind.ManaAlternate:
                Draw(renderer, textures.Mana2, left: 0, top: 45, right: 34, bottom: 74, x: 52, y: originY + 99, width: 34, height: 30);
                break;

            case CaseKind.Stamina:
                Draw(renderer, textures.Stamina0, left: 0, top: 35, right: 8, bottom: 70, x: 42, y: originY + 93, width: 8, height: 35);
                break;

            case CaseKind.Overflow25:
                Draw(renderer, textures.Stamina0, left: 0, top: 12, right: 8, bottom: 70, x: 42, y: originY + 70, width: 8, height: 58);
                Draw(renderer, textures.Overflow0, left: 0, top: 18, right: 8, bottom: 35, x: 42, y: originY + 70, width: 8, height: 17);
                break;

            case CaseKind.Overflow50:
                Draw(renderer, textures.Stamina0, left: 0, top: 0, right: 8, bottom: 70, x: 42, y: originY + 58, width: 8, height: 70);
                Draw(renderer, textures.Overflow0, left: 0, top: 0, right: 8, bottom: 35, x: 42, y: originY + 52, width: 8, height: 35);
                break;

            case CaseKind.OverflowPositiveZeroPixel:
                Draw(renderer, textures.Stamina0, left: 0, top: 23, right: 8, bottom: 70, x: 42, y: originY + 81, width: 8, height: 47);
                Draw(renderer, textures.Overflow0, left: 0, top: 35, right: 8, bottom: 35, x: 42, y: originY + 87, width: 8, height: 32);
                break;

            default:
                throw new InvalidOperationException($"Unhandled main-HUD vitals conformance case '{kind}'.");
        }

        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static void Draw(OpenGLRenderer renderer, OpenGLTexture2D texture, int left, int top, int right, int bottom, int x, int y, int width, int height)
    {
        renderer.DrawRepeatedSprite(texture, new SpriteSourceBounds(left, top, right, bottom), x, y, width, height);
    }

    private static void VerifyProductionAssets(MainHudVitalsAssets assets, ReferenceImages referenceImages)
    {
        if (!assets.HasLife || !assets.HasMana || !assets.HasStamina || !assets.HasExtendedStamina)
        {
            throw new InvalidDataException("Verified retail gauge assets did not produce all four available main-HUD vitals sets.");
        }

        VerifyProductionDecode("Progress40 frame 0", assets.GetLifeFrame(0)?.Pixels, referenceImages.Life0);
        VerifyProductionDecode("Progress40 frame 1", assets.GetLifeFrame(1)?.Pixels, referenceImages.Life1);
        VerifyProductionDecode("Progress40 frame 2", assets.GetLifeFrame(2)?.Pixels, referenceImages.Life2);

        VerifyProductionDecode("Progress41 frame 0", assets.GetManaFrame(0)?.Pixels, referenceImages.Mana0);
        VerifyProductionDecode("Progress41 frame 1", assets.GetManaFrame(1)?.Pixels, referenceImages.Mana1);
        VerifyProductionDecode("Progress41 frame 2", assets.GetManaFrame(2)?.Pixels, referenceImages.Mana2);

        VerifyProductionDecode("Progress46 frame 0", assets.GetStaminaFrame(0)?.Pixels, referenceImages.Stamina0);
        VerifyProductionDecode("Progress46 frame 1", assets.GetStaminaFrame(1)?.Pixels, referenceImages.Stamina1);

        VerifyProductionDecode("Progress47 frame 0", assets.GetExtendedStaminaFrame(0)?.Pixels, referenceImages.Overflow0);
        VerifyProductionDecode("Progress47 frame 1", assets.GetExtendedStaminaFrame(1)?.Pixels, referenceImages.Overflow1);
    }

    private static void VerifyProductionDecode(string assetName, ReadOnlyMemory<byte>? productionPixels, ReadOnlySpan<byte> referencePixels)
    {
        if (!productionPixels.HasValue)
        {
            throw new InvalidDataException($"{assetName} was unavailable through the production HUD vitals asset loader.");
        }

        ReadOnlySpan<byte> pixels = productionPixels.Value.Span;

        if (!pixels.SequenceEqual(referencePixels))
        {
            throw new InvalidDataException($"{assetName} production RGBA SHA256 {ConformanceHash.Sha256(pixels)} does not match independent DXT3 reference SHA256 {ConformanceHash.Sha256(referencePixels)}.");
        }
    }

    private static byte[] ReadPackageOnlyAsset(PackagedClientContentSource contentSource, string contentPath, string expectedSha256)
    {
        if (contentSource.TryOpenRead(contentPath, ContentLookupMode.LooseOnly, out Stream? unexpectedLoose))
        {
            unexpectedLoose.Dispose();
            throw new InvalidDataException($"Retail HUD gauge asset '{contentPath}' unexpectedly resolves as a loose file; verified 5517 requires package-backed lookup.");
        }

        using Stream stream = contentSource.OpenRequiredRead(contentPath, ContentLookupMode.PackageOnly);
        byte[] bytes = ReadAllBytes(stream);

        VerifyEncodedHash(contentPath, bytes, expectedSha256);

        return bytes;
    }

    private static byte[] ReadLooseOnlyAsset(PackagedClientContentSource contentSource, string contentPath, string expectedSha256)
    {
        if (contentSource.TryOpenRead(contentPath, ContentLookupMode.PackageOnly, out Stream? unexpectedPackaged))
        {
            unexpectedPackaged.Dispose();
            throw new InvalidDataException($"Retail HUD gauge asset '{contentPath}' unexpectedly resolves from a package; verified 5517 requires loose lookup.");
        }

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
            throw new InvalidDataException($"Retail HUD gauge asset '{contentPath}' has SHA256 {actualSha256}; expected {expectedSha256}.");
        }
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private enum CaseKind
    {
        LifeNormalDual,
        LifeStartupAlternate,
        ManaNormalDual,
        ManaAlternate,
        Stamina,
        Overflow25,
        Overflow50,
        OverflowPositiveZeroPixel,
    }

    private readonly record struct GaugeCase(string Name, CaseKind Kind, MainHudVitalsSnapshot Snapshot, bool ForceLifeNormal = false, bool ManaAlternate = false);

    private sealed class ReferenceImages
    {
        private ReferenceImages(byte[] life0, byte[] life1, byte[] life2, byte[] mana0, byte[] mana1, byte[] mana2, byte[] stamina0, byte[] stamina1, byte[] overflow0, byte[] overflow1)
        {
            Life0 = life0;
            Life1 = life1;
            Life2 = life2;
            Mana0 = mana0;
            Mana1 = mana1;
            Mana2 = mana2;
            Stamina0 = stamina0;
            Stamina1 = stamina1;
            Overflow0 = overflow0;
            Overflow1 = overflow1;
        }

        public byte[] Life0
        {
            get;
        }

        public byte[] Life1
        {
            get;
        }

        public byte[] Life2
        {
            get;
        }

        public byte[] Mana0
        {
            get;
        }

        public byte[] Mana1
        {
            get;
        }

        public byte[] Mana2
        {
            get;
        }

        public byte[] Stamina0
        {
            get;
        }

        public byte[] Stamina1
        {
            get;
        }

        public byte[] Overflow0
        {
            get;
        }

        public byte[] Overflow1
        {
            get;
        }

        public static ReferenceImages Load(PackagedClientContentSource contentSource)
        {
            byte[] life0 = DecodePackage(contentSource, "data/main/ProgressHP.dds", "ecd40dbdebc5e582860c91deeb28a29b8adaaf9dbc8285e8c5404a5f1ab6be2d", 128);
            byte[] life1 = DecodePackage(contentSource, "data/main/ProgressHPA.dds", "2ad8122875e02d25ff53ed281b9214fcbde1689a773fdb59e0bba355bde0c54d", 128);
            byte[] life2 = DecodePackage(contentSource, "data/main/ProgressHPH.dds", "f85e3d2287f9f3cda60b3494dcb36359479d738c34030d3266530026080d2679", 128);

            byte[] mana0 = DecodePackage(contentSource, "data/main/ProgressMP.dds", "01fbd1e55d5266a823ffb2347a96721f8e9b7ec18c4393f18ee4488ccac605be", 128);
            byte[] mana1 = DecodePackage(contentSource, "data/main/ProgressMPA.dds", "324ddfd751ec47123285a26905d00d56694ffd7461f38f6b3e6e3125095952e1", 128);
            byte[] mana2 = DecodePackage(contentSource, "data/main/ProgressMPH.dds", "b8dfdad6025fbd28201d92d9fe95f8ff504450209d43feeec57aaa0a30f4ebc6", 128);

            byte[] stamina0 = DecodePackage(contentSource, "data/main/ProgressForce.dds", "f030dbbd0823b08d12901ebf803adee687df015d3d017793f67959db9d9e8192", 128);
            byte[] stamina1 = DecodePackage(contentSource, "data/main/ProgressForceA.dds", "3b7177b3bb0405e3c7f0a68aaaa3e6ca5b054ff63d798887eec5687adc76154b", 128);

            byte[] overflow0 = DecodeLoose(contentSource, "data/main/ProgressForce2.dds", "9a67c108613cdf5440a40708bcbd3573ef9b3782da6d997c435b654a7fbbada9", 32);
            byte[] overflow1 = DecodeLoose(contentSource, "data/main/ProgressForce2A.dds", "77ebf0be6f6f4621ecfb28f1e04f39f6f95bce647fc0d27af44bcf4b2c13c789", 32);

            return new ReferenceImages(life0, life1, life2, mana0, mana1, mana2, stamina0, stamina1, overflow0, overflow1);
        }

        private static byte[] DecodePackage(PackagedClientContentSource contentSource, string contentPath, string expectedSha256, int dimension)
        {
            return Dxt3ReferenceDecoder.Decode(ReadPackageOnlyAsset(contentSource, contentPath, expectedSha256), dimension, dimension);
        }

        private static byte[] DecodeLoose(PackagedClientContentSource contentSource, string contentPath, string expectedSha256, int dimension)
        {
            return Dxt3ReferenceDecoder.Decode(ReadLooseOnlyAsset(contentSource, contentPath, expectedSha256), dimension, dimension);
        }
    }

    private sealed class ReferenceTextures : IDisposable
    {
        private bool _disposed;

        public ReferenceTextures(OpenGLGraphicsDevice graphicsDevice, ReferenceImages images)
        {
            OpenGLTexture2D? life0 = null;
            OpenGLTexture2D? life1 = null;
            OpenGLTexture2D? life2 = null;
            OpenGLTexture2D? mana0 = null;
            OpenGLTexture2D? mana1 = null;
            OpenGLTexture2D? mana2 = null;
            OpenGLTexture2D? stamina0 = null;
            OpenGLTexture2D? overflow0 = null;

            try
            {
                life0 = graphicsDevice.CreateTexture2D(128, 128, images.Life0);
                life1 = graphicsDevice.CreateTexture2D(128, 128, images.Life1);
                life2 = graphicsDevice.CreateTexture2D(128, 128, images.Life2);
                mana0 = graphicsDevice.CreateTexture2D(128, 128, images.Mana0);
                mana1 = graphicsDevice.CreateTexture2D(128, 128, images.Mana1);
                mana2 = graphicsDevice.CreateTexture2D(128, 128, images.Mana2);
                stamina0 = graphicsDevice.CreateTexture2D(128, 128, images.Stamina0);
                overflow0 = graphicsDevice.CreateTexture2D(32, 32, images.Overflow0);

                Life0 = life0;
                Life1 = life1;
                Life2 = life2;
                Mana0 = mana0;
                Mana1 = mana1;
                Mana2 = mana2;
                Stamina0 = stamina0;
                Overflow0 = overflow0;
            }
            catch
            {
                DisposeCreatedTextures(overflow0, stamina0, mana2, mana1, mana0, life2, life1, life0);
                throw;
            }
        }

        public OpenGLTexture2D Life0
        {
            get;
        }

        public OpenGLTexture2D Life1
        {
            get;
        }

        public OpenGLTexture2D Life2
        {
            get;
        }

        public OpenGLTexture2D Mana0
        {
            get;
        }

        public OpenGLTexture2D Mana1
        {
            get;
        }

        public OpenGLTexture2D Mana2
        {
            get;
        }

        public OpenGLTexture2D Stamina0
        {
            get;
        }

        public OpenGLTexture2D Overflow0
        {
            get;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ExceptionDispatchInfo? firstFailure = null;

            DisposeTexture(Overflow0, ref firstFailure);
            DisposeTexture(Stamina0, ref firstFailure);
            DisposeTexture(Mana2, ref firstFailure);
            DisposeTexture(Mana1, ref firstFailure);
            DisposeTexture(Mana0, ref firstFailure);
            DisposeTexture(Life2, ref firstFailure);
            DisposeTexture(Life1, ref firstFailure);
            DisposeTexture(Life0, ref firstFailure);

            _disposed = true;
            firstFailure?.Throw();
        }

        private static void DisposeTexture(OpenGLTexture2D texture, ref ExceptionDispatchInfo? firstFailure)
        {
            try
            {
                texture.Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        private static void DisposeCreatedTextures(params OpenGLTexture2D?[] textures)
        {
            foreach (OpenGLTexture2D? texture in textures)
            {
                try
                {
                    texture?.Dispose();
                }
                catch
                {
                    // Preserve the texture-creation failure that initiated cleanup.
                }
            }
        }
    }
}
