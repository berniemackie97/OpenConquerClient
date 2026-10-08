using System.Runtime.ExceptionServices;
using OpenConquer.Client.UI.Hud.CheckControls;
using OpenConquer.Content;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Reference;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class MainHudCheckControlsConformance
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string ExpectedControlAniSha256 = "a1db47baaeb2f75eea3b5216378f97d713c2afeaefe05ec02e6f584794532d27";
    private const int FrameWidth = 32;
    private const int FrameHeight = 32;

    private static readonly LogicalRenderSize[] s_logicalRenderSizes = [new(800, 600), new(1024, 768)];

    private static readonly CheckFixture[] s_controls =
    [
        C(MainHudCheckControlId.Check40, "Check40", 0, 482, 650,
            F("data/main/RunChk1.dds", "d4e5ee9cfc3afb45f803bcb589f21a6b10ec65d8295e2554171e3e40dd8dcf58"),
            F("data/main/RunChk2.dds", "70b01df50ab75f0cc3f7ecc8958844171924b80d7993e7c81c22a0ca134b03cb")),

        C(MainHudCheckControlId.Check43, "Check43", 72, 482, 650,
            F("data/main/MapChk2.dds", "1662cc91c12d3c8b7fdf193c7855374f84b6fa9fd522e778e38c8fd6fa721e2e"),
            F("data/main/MapChk1.dds", "f11580826bad44a032f67a622e728448957dd648588b005c0afebb90627a835f")),

        C(MainHudCheckControlId.Check46, "Check46", 50, 470, 638,
            F("data/main/ScreenMoveChk1.dds", "a9a6fa6e7ab47211f52074b524a2f4044a16cc0c07dd05867755b8384b62911e"),
            F("data/main/ScreenMoveChk2.dds", "6f06ef1bbf0a1f4a0cdb0b759fea78289bc55773897215e71d460f6c99ca927f")),

        C(MainHudCheckControlId.Button411, "Button411", 22, 470, 638,
            F("data/main/NpcEquip.dds", "ce4605c39ad53462db6d62ee16d26d9481e50eacf85513d6cbbae61abe3fc45c"),
            F("data/main/NpcEquipClick.dds", "0c2b8b6f9e2fc330a056a36b1021d7f67866bae5cee6066df741d92ad1ce6b80")),
    ];

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PackagedClientContentSource contentSource, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        _ = ReadVerifiedAsset(contentSource, ControlAniPath, ExpectedControlAniSha256, ContentLookupMode.LooseOnly);

        Dictionary<string, byte[]> referencePixels = DecodeReferenceFrames(contentSource);
        MainHudCheckControlAssets assets = MainHudCheckControlAssets.Load(contentSource);

        VerifyProductionDecode(assets, referencePixels);

        using ReferenceTextures referenceTextures = new(graphicsDevice, referencePixels);

        foreach (LogicalRenderSize logicalRenderSize in s_logicalRenderSizes)
        {
            using MainHudCheckControlRenderer checkRenderer = new(graphicsDevice, assets, logicalRenderSize);

            foreach (RenderCase testCase in BuildRenderCases())
            {
                RunRenderCase(graphicsDevice, checkRenderer, referenceTextures, logicalRenderSize, framebufferSize, colorFormat, testCase);
            }
        }
    }

    private static void RunRenderCase(
        OpenGLGraphicsDevice graphicsDevice,
        MainHudCheckControlRenderer checkRenderer,
        ReferenceTextures referenceTextures,
        LogicalRenderSize logicalRenderSize,
        PixelSize framebufferSize,
        string colorFormat,
        RenderCase testCase)
    {
        MainHudCheckControlsState state = new();

        if (testCase.ControlId is { } id)
        {
            state.GetControl(id).SetState(testCase.State);
        }

        byte[] actual = RenderProduction(graphicsDevice, checkRenderer, state, logicalRenderSize, framebufferSize);
        byte[] expected = RenderReference(graphicsDevice, referenceTextures, logicalRenderSize, framebufferSize, testCase);

        string label = $"Main HUD check controls {testCase.Name} {logicalRenderSize.Width}x{logicalRenderSize.Height}";
        FramebufferVerifier.VerifyExact(label, expected, actual);

        Console.WriteLine($"{label}, {colorFormat}");
        Console.WriteLine($"Main HUD check-controls framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static byte[] RenderProduction(
        OpenGLGraphicsDevice graphicsDevice,
        MainHudCheckControlRenderer checkRenderer,
        MainHudCheckControlsState state,
        LogicalRenderSize logicalRenderSize,
        PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();
        checkRenderer.Draw(renderer, state);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static byte[] RenderReference(
        OpenGLGraphicsDevice graphicsDevice,
        ReferenceTextures referenceTextures,
        LogicalRenderSize logicalRenderSize,
        PixelSize framebufferSize,
        RenderCase testCase)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();

        foreach (CheckFixture fixture in s_controls)
        {
            int state = fixture.Id == testCase.ControlId ? testCase.State : 0;
            OpenGLTexture2D texture = referenceTextures.Get(fixture.Frames[state]);
            int y = logicalRenderSize.Height == 600 ? fixture.Y800 : fixture.Y1024;

            renderer.DrawSprite(texture, fixture.X, y);
        }

        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static Dictionary<string, byte[]> DecodeReferenceFrames(PackagedClientContentSource contentSource)
    {
        Dictionary<string, byte[]> pixels = new(StringComparer.Ordinal);

        foreach (CheckFixture control in s_controls)
        {
            foreach (FrameFixture frame in control.Frames)
            {
                byte[] encoded = ReadVerifiedRetailFrame(contentSource, frame.Path, frame.Sha256);
                pixels.Add(frame.Path, Dxt3ReferenceDecoder.Decode(encoded, FrameWidth, FrameHeight));
            }
        }

        return pixels;
    }

    private static void VerifyProductionDecode(MainHudCheckControlAssets assets, Dictionary<string, byte[]> referencePixels)
    {
        foreach (CheckFixture control in s_controls)
        {
            if (!assets.IsAvailable(control.Id))
            {
                throw new InvalidDataException($"Verified retail [{control.SectionName}] assets were unavailable through the production check-control loader.");
            }

            for (int state = 0; state < MainHudCheckControlDefinitions.FrameCount; state++)
            {
                FrameFixture expected = control.Frames[state];
                VerifyPixels($"{control.SectionName} state {state}", assets.GetFrame(control.Id, state)?.Pixels, referencePixels[expected.Path]);
            }
        }
    }

    private static RenderCase[] BuildRenderCases()
    {
        List<RenderCase> cases =
        [
            new("all-state-0", null, 0),
        ];

        foreach (CheckFixture control in s_controls)
        {
            cases.Add(new RenderCase($"{control.SectionName}-state-1", control.Id, 1));
        }

        return cases.ToArray();
    }

    private static byte[] ReadVerifiedRetailFrame(PackagedClientContentSource contentSource, string contentPath, string expectedSha256)
    {
        bool foundLoose = contentSource.TryOpenRead(contentPath, ContentLookupMode.LooseOnly, out Stream? looseStream);
        bool foundPackage = contentSource.TryOpenRead(contentPath, ContentLookupMode.PackageOnly, out Stream? packageStream);

        try
        {
            if (foundLoose == foundPackage)
            {
                string resolution = foundLoose ? "both loose and package storage" : "neither loose nor package storage";
                throw new InvalidDataException($"Retail HUD check-control asset '{contentPath}' resolves from {resolution}; exactly one source is required.");
            }

            Stream stream = foundLoose ? looseStream! : packageStream!;
            using MemoryStream buffer = new();
            stream.CopyTo(buffer);

            byte[] bytes = buffer.ToArray();
            VerifyEncodedHash(contentPath, bytes, expectedSha256);
            return bytes;
        }
        finally
        {
            looseStream?.Dispose();
            packageStream?.Dispose();
        }
    }

    private static byte[] ReadVerifiedAsset(PackagedClientContentSource contentSource, string contentPath, string expectedSha256, ContentLookupMode lookupMode)
    {
        ContentLookupMode unexpectedMode = lookupMode switch
        {
            ContentLookupMode.LooseOnly => ContentLookupMode.PackageOnly,
            ContentLookupMode.PackageOnly => ContentLookupMode.LooseOnly,
            _ => throw new ArgumentOutOfRangeException(nameof(lookupMode), lookupMode, "Conformance assets require exact loose-or-package provenance."),
        };

        if (contentSource.TryOpenRead(contentPath, unexpectedMode, out Stream? unexpected))
        {
            unexpected.Dispose();
            throw new InvalidDataException($"Retail HUD check-control asset '{contentPath}' unexpectedly resolves using {unexpectedMode}; verified 5517 requires {lookupMode} lookup.");
        }

        using Stream stream = contentSource.OpenRequiredRead(contentPath, lookupMode);
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);

        byte[] bytes = buffer.ToArray();
        VerifyEncodedHash(contentPath, bytes, expectedSha256);
        return bytes;
    }

    private static void VerifyEncodedHash(string contentPath, ReadOnlySpan<byte> bytes, string expectedSha256)
    {
        string actualSha256 = ConformanceHash.Sha256(bytes);

        if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Retail HUD check-control asset '{contentPath}' has SHA256 {actualSha256}; expected {expectedSha256}.");
        }
    }

    private static void VerifyPixels(string assetName, ReadOnlyMemory<byte>? productionPixels, ReadOnlySpan<byte> referencePixels)
    {
        if (!productionPixels.HasValue)
        {
            throw new InvalidDataException($"{assetName} was unavailable through the production HUD check-control asset loader.");
        }

        ReadOnlySpan<byte> pixels = productionPixels.Value.Span;

        if (!pixels.SequenceEqual(referencePixels))
        {
            throw new InvalidDataException($"{assetName} production RGBA SHA256 {ConformanceHash.Sha256(pixels)} does not match independent DXT3 reference SHA256 {ConformanceHash.Sha256(referencePixels)}.");
        }
    }

    private static CheckFixture C(MainHudCheckControlId id, string sectionName, int x, int y800, int y1024, params FrameFixture[] frames) => new(id, sectionName, x, y800, y1024, frames);

    private static FrameFixture F(string path, string sha256) => new(path, sha256);

    private readonly record struct CheckFixture(MainHudCheckControlId Id, string SectionName, int X, int Y800, int Y1024, FrameFixture[] Frames);

    private readonly record struct FrameFixture(string Path, string Sha256);

    private readonly record struct RenderCase(string Name, MainHudCheckControlId? ControlId, int State);

    private sealed class ReferenceTextures : IDisposable
    {
        private readonly Dictionary<string, OpenGLTexture2D> _textures = new(StringComparer.Ordinal);
        private readonly List<OpenGLTexture2D> _ownedTextures = [];
        private bool _disposed;

        public ReferenceTextures(OpenGLGraphicsDevice graphicsDevice, Dictionary<string, byte[]> referencePixels)
        {
            try
            {
                foreach ((string path, byte[] pixels) in referencePixels)
                {
                    OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(FrameWidth, FrameHeight, pixels);
                    _textures.Add(path, texture);
                    _ownedTextures.Add(texture);
                }
            }
            catch
            {
                DisposeCreatedTextures();
                throw;
            }
        }

        public OpenGLTexture2D Get(FrameFixture fixture)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _textures[fixture.Path];
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ExceptionDispatchInfo? firstFailure = null;

            for (int index = _ownedTextures.Count - 1; index >= 0; index--)
            {
                try
                {
                    _ownedTextures[index].Dispose();
                }
                catch (Exception exception)
                {
                    firstFailure ??= ExceptionDispatchInfo.Capture(exception);
                }
            }

            _disposed = true;
            firstFailure?.Throw();
        }

        private void DisposeCreatedTextures()
        {
            for (int index = _ownedTextures.Count - 1; index >= 0; index--)
            {
                try
                {
                    _ownedTextures[index].Dispose();
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
