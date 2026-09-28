using System.Runtime.ExceptionServices;
using OpenConquer.Client.UI.Hud;
using OpenConquer.Content;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Reference;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class MainHudOrganiseButtonConformance
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string NormalFramePath = "data/main/OrganiseBtnNormal.dds";
    private const string PressedFramePath = "data/main/OrganiseBtnClick.dds";
    private const string DisabledFramePath = "data/main/OrganiseBtnUnClick.dds";
    private const string HoverFramePath = "data/main/OrganiseBtnEmboss.dds";

    private const string ExpectedControlAniSha256 = "a1db47baaeb2f75eea3b5216378f97d713c2afeaefe05ec02e6f584794532d27";
    private const string ExpectedNormalFrameSha256 = "0b4f52919c0506bf86881e4201859459128df5736552189737751118aa66b360";
    private const string ExpectedPressedFrameSha256 = "d6291032e2fc79aa6ca3a1715f32c2f3e73dc402b9fb927c9100dc7cae98b03a";
    private const string ExpectedDisabledFrameSha256 = "f9382f214d194c56e320933d69d00a637621b015b757401e29e260dbc818aaa3";
    private const string ExpectedHoverFrameSha256 = "276d5d92563a4ae97825a0a2dafb7910788e2902c87ca7a96af2b2b7acdc2e16";

    private const int FrameWidth = 64;
    private const int FrameHeight = 32;
    private const int NativeX = 702;
    private const int NativeY800 = 553;
    private const int NativeY1024 = 721;

    private static readonly LogicalRenderSize[] s_logicalRenderSizes = [new(800, 600), new(1024, 768)];

    private static readonly RenderCase[] s_cases =
    [
        new("normal", MainHudOrganiseButtonState.NormalFrame),
        new("pressed", MainHudOrganiseButtonState.PressedFrame),
        new("disabled", MainHudOrganiseButtonState.DisabledFrame),
        new("hover", MainHudOrganiseButtonState.HoverFrame),
    ];

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PackagedClientContentSource contentSource, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        _ = ReadLooseOnlyAsset(contentSource, ControlAniPath, ExpectedControlAniSha256);

        byte[] normalReference = DecodeLooseFrame(contentSource, NormalFramePath, ExpectedNormalFrameSha256);
        byte[] pressedReference = DecodeLooseFrame(contentSource, PressedFramePath, ExpectedPressedFrameSha256);
        byte[] disabledReference = DecodeLooseFrame(contentSource, DisabledFramePath, ExpectedDisabledFrameSha256);
        byte[] hoverReference = DecodeLooseFrame(contentSource, HoverFramePath, ExpectedHoverFrameSha256);

        MainHudOrganiseButtonAssets assets = MainHudOrganiseButtonAssets.Load(contentSource);

        if (!assets.IsAvailable)
        {
            throw new InvalidDataException("Verified retail Main3_OrganiseBtn assets did not produce an available main-HUD organise-button set.");
        }

        VerifyProductionDecode("Main3_OrganiseBtn frame 0", assets.GetFrame(0)?.Pixels, normalReference);
        VerifyProductionDecode("Main3_OrganiseBtn frame 1", assets.GetFrame(1)?.Pixels, pressedReference);
        VerifyProductionDecode("Main3_OrganiseBtn frame 2", assets.GetFrame(2)?.Pixels, disabledReference);
        VerifyProductionDecode("Main3_OrganiseBtn frame 3", assets.GetFrame(3)?.Pixels, hoverReference);

        using ReferenceTextures referenceTextures = new(graphicsDevice, normalReference, pressedReference, disabledReference, hoverReference);

        foreach (LogicalRenderSize logicalRenderSize in s_logicalRenderSizes)
        {
            using MainHudOrganiseButtonRenderer organiseButtonRenderer = new(graphicsDevice, assets, logicalRenderSize);

            foreach (RenderCase testCase in s_cases)
            {
                RunRenderCase(graphicsDevice, organiseButtonRenderer, referenceTextures, logicalRenderSize, framebufferSize, colorFormat, testCase);
            }
        }
    }

    private static void RunRenderCase(OpenGLGraphicsDevice graphicsDevice, MainHudOrganiseButtonRenderer organiseButtonRenderer, ReferenceTextures referenceTextures,
        LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, string colorFormat, RenderCase testCase)
    {
        MainHudOrganiseButtonState state = new();
        state.SetCurrentFrame(testCase.FrameIndex);

        byte[] actual = RenderProduction(graphicsDevice, organiseButtonRenderer, state, logicalRenderSize, framebufferSize);
        byte[] expected = RenderReference(graphicsDevice, referenceTextures.GetFrame(testCase.FrameIndex), logicalRenderSize, framebufferSize);

        FramebufferVerifier.VerifyExact($"Main HUD organise button {testCase.Name} {logicalRenderSize.Width}x{logicalRenderSize.Height}", expected, actual);

        Console.WriteLine($"Main HUD organise button: {testCase.Name}, {logicalRenderSize.Width}x{logicalRenderSize.Height}, {colorFormat}");
        Console.WriteLine($"Main HUD organise button framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static byte[] RenderProduction(OpenGLGraphicsDevice graphicsDevice, MainHudOrganiseButtonRenderer organiseButtonRenderer,
        MainHudOrganiseButtonState state, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();
        organiseButtonRenderer.Draw(renderer, state);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static byte[] RenderReference(OpenGLGraphicsDevice graphicsDevice, OpenGLTexture2D texture, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        (int x, int y) = logicalRenderSize switch
        {
            { Width: 800, Height: 600 } => (NativeX, NativeY800),
            { Width: 1024, Height: 768 } => (NativeX, NativeY1024),
            _ => throw new ArgumentOutOfRangeException(nameof(logicalRenderSize), logicalRenderSize, "Unsupported Main3_OrganiseBtn conformance resolution."),
        };

        renderer.BeginFrame();
        renderer.DrawSprite(texture, x, y);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static byte[] DecodeLooseFrame(PackagedClientContentSource contentSource, string contentPath, string expectedSha256)
    {
        byte[] encoded = ReadLooseOnlyAsset(contentSource, contentPath, expectedSha256);
        return Dxt3ReferenceDecoder.Decode(encoded, FrameWidth, FrameHeight);
    }

    private static byte[] ReadLooseOnlyAsset(PackagedClientContentSource contentSource, string contentPath, string expectedSha256)
    {
        if (contentSource.TryOpenRead(contentPath, ContentLookupMode.PackageOnly, out Stream? unexpectedPackaged))
        {
            unexpectedPackaged.Dispose();
            throw new InvalidDataException($"Retail HUD organise-button asset '{contentPath}' unexpectedly resolves from a package; verified 5517 requires loose lookup.");
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
            throw new InvalidDataException($"Retail HUD organise-button asset '{contentPath}' has SHA256 {actualSha256}; expected {expectedSha256}.");
        }
    }

    private static void VerifyProductionDecode(string assetName, ReadOnlyMemory<byte>? productionPixels, ReadOnlySpan<byte> referencePixels)
    {
        if (!productionPixels.HasValue)
        {
            throw new InvalidDataException($"{assetName} was unavailable through the production HUD organise-button asset loader.");
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

    private readonly record struct RenderCase(string Name, int FrameIndex);

    private sealed class ReferenceTextures : IDisposable
    {
        private bool _disposed;

        public ReferenceTextures(OpenGLGraphicsDevice graphicsDevice, ReadOnlySpan<byte> normal, ReadOnlySpan<byte> pressed, ReadOnlySpan<byte> disabled, ReadOnlySpan<byte> hover)
        {
            OpenGLTexture2D? frame0 = null;
            OpenGLTexture2D? frame1 = null;
            OpenGLTexture2D? frame2 = null;
            OpenGLTexture2D? frame3 = null;

            try
            {
                frame0 = graphicsDevice.CreateTexture2D(FrameWidth, FrameHeight, normal);
                frame1 = graphicsDevice.CreateTexture2D(FrameWidth, FrameHeight, pressed);
                frame2 = graphicsDevice.CreateTexture2D(FrameWidth, FrameHeight, disabled);
                frame3 = graphicsDevice.CreateTexture2D(FrameWidth, FrameHeight, hover);

                Frame0 = frame0;
                Frame1 = frame1;
                Frame2 = frame2;
                Frame3 = frame3;
            }
            catch
            {
                DisposeCreatedTextures(frame3, frame2, frame1, frame0);
                throw;
            }
        }

        public OpenGLTexture2D Frame0
        {
            get;
        }

        public OpenGLTexture2D Frame1
        {
            get;
        }

        public OpenGLTexture2D Frame2
        {
            get;
        }

        public OpenGLTexture2D Frame3
        {
            get;
        }

        public OpenGLTexture2D GetFrame(int frameIndex)
        {
            return frameIndex switch
            {
                MainHudOrganiseButtonState.NormalFrame => Frame0,
                MainHudOrganiseButtonState.PressedFrame => Frame1,
                MainHudOrganiseButtonState.DisabledFrame => Frame2,
                MainHudOrganiseButtonState.HoverFrame => Frame3,
                _ => throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, "Unsupported Main3_OrganiseBtn frame."),
            };
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ExceptionDispatchInfo? firstFailure = null;

            DisposeTexture(Frame3, ref firstFailure);
            DisposeTexture(Frame2, ref firstFailure);
            DisposeTexture(Frame1, ref firstFailure);
            DisposeTexture(Frame0, ref firstFailure);

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
