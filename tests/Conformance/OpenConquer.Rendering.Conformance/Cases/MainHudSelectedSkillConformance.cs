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

internal static class MainHudSelectedSkillConformance
{
    private const int FrameWidth = 64;
    private const int FrameHeight = 64;

    private static readonly LogicalRenderSize[] s_logicalRenderSizes = [new(800, 600), new(1024, 768)];

    private static readonly FrameFixture s_selected = new(
        "data/main/MainImgMagic.dds",
        "601f604e28138c0e4d15c262d7448613703585a1f3b20b1999acc2c257b99a99");

    private static readonly FrameFixture s_cover = new(
        "data/main/ImageDisable.dds",
        "f12c8e2ae84fc5ed7b8527a64dc9942b1ab807102e9ef5c24b9c14c5be68e494");

    private static readonly RenderCase[] s_cases =
    [
        new("cleared", false, false),
        new("selected", true, false),
        new("selected-covered", true, true),
        new("cleared-covered", false, true),
    ];

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PackagedClientContentSource contentSource, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        Dictionary<string, byte[]> referencePixels = DecodeReferenceFrames(contentSource);
        MainHudSelectedSkillAssets assets = new(contentSource);

        VerifyProductionDecode(assets, referencePixels);

        using ReferenceTextures referenceTextures = new(graphicsDevice, referencePixels);

        foreach (LogicalRenderSize logicalRenderSize in s_logicalRenderSizes)
        {
            using MainHudSelectedSkillRenderer selectedSkillRenderer = new(graphicsDevice, assets, logicalRenderSize);

            foreach (RenderCase testCase in s_cases)
                RunRenderCase(graphicsDevice, selectedSkillRenderer, referenceTextures, logicalRenderSize, framebufferSize, colorFormat, testCase);
        }
    }

    private static void RunRenderCase(OpenGLGraphicsDevice graphicsDevice, MainHudSelectedSkillRenderer selectedSkillRenderer, ReferenceTextures referenceTextures, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, string colorFormat, RenderCase testCase)
    {
        MainHudSelectedSkillState state = CreateState(testCase);
        byte[] actual = RenderProduction(graphicsDevice, selectedSkillRenderer, state, logicalRenderSize, framebufferSize);
        byte[] expected = RenderReference(graphicsDevice, referenceTextures, logicalRenderSize, framebufferSize, testCase);
        string label = $"Main HUD selected skill {testCase.Name} {logicalRenderSize.Width}x{logicalRenderSize.Height}";

        FramebufferVerifier.VerifyExact(label, expected, actual);

        Console.WriteLine($"{label}, {colorFormat}");
        Console.WriteLine($"Main HUD selected-skill framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static MainHudSelectedSkillState CreateState(RenderCase testCase)
    {
        MainHudSelectedSkillState state = new();

        if (testCase.ImageActive)
        {
            state.SetSectionAndContent(MainHudSelectedSkillDefinition.InitialSectionName, contentId: 0, blockedCover: 0);
        }

        if (testCase.Covered)
        {
            state.SetCoverFlag(1);
        }

        return state;
    }

    private static byte[] RenderProduction(OpenGLGraphicsDevice graphicsDevice, MainHudSelectedSkillRenderer selectedSkillRenderer, MainHudSelectedSkillState state, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();
        selectedSkillRenderer.Draw(renderer, state);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static byte[] RenderReference(OpenGLGraphicsDevice graphicsDevice, ReferenceTextures referenceTextures, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, RenderCase testCase)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        MainHudSelectedSkillBounds bounds = MainHudSelectedSkillLayout.Create(logicalRenderSize).GetBounds();

        renderer.BeginFrame();

        if (testCase.ImageActive)
        {
            renderer.DrawSprite(referenceTextures.Selected, new SpriteSourceRectangle(0, 0, MainHudSelectedSkillDefinition.SourceWidth, MainHudSelectedSkillDefinition.SourceHeight), bounds.X, bounds.Y, MainHudSelectedSkillBounds.Width, MainHudSelectedSkillBounds.Height);
        }

        if (testCase.Covered)
        {
            renderer.DrawSprite(referenceTextures.Cover, new SpriteSourceRectangle(0, 0, MainHudSelectedSkillDefinition.CoverSourceWidth, MainHudSelectedSkillDefinition.CoverSourceHeight), bounds.X, bounds.Y, MainHudSelectedSkillBounds.Width, MainHudSelectedSkillBounds.Height);
        }

        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static Dictionary<string, byte[]> DecodeReferenceFrames(PackagedClientContentSource contentSource)
    {
        Dictionary<string, byte[]> pixels = new(StringComparer.Ordinal);

        foreach (FrameFixture fixture in new[] { s_selected, s_cover })
        {
            byte[] encoded = ReadVerifiedRetailFrame(contentSource, fixture.Path, fixture.Sha256);
            pixels.Add(fixture.Path, Dxt3ReferenceDecoder.Decode(encoded, FrameWidth, FrameHeight));
        }

        return pixels;
    }

    private static void VerifyProductionDecode(MainHudSelectedSkillAssets assets, Dictionary<string, byte[]> referencePixels)
    {
        VerifyPixels("Magic0", assets.GetSelectedFrame(MainHudSelectedSkillDefinition.InitialSectionName, 0)?.Pixels, referencePixels[s_selected.Path]);
        VerifyPixels("Image0", assets.GetCoverFrame()?.Pixels, referencePixels[s_cover.Path]);
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
                throw new InvalidDataException($"Retail selected-skill asset '{contentPath}' resolves from {resolution}; exactly one source is required.");
            }

            Stream stream = foundLoose ? looseStream! : packageStream!;
            using MemoryStream buffer = new();

            stream.CopyTo(buffer);

            byte[] bytes = buffer.ToArray();
            string actualSha256 = ConformanceHash.Sha256(bytes);

            if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Retail selected-skill asset '{contentPath}' has SHA256 {actualSha256}; expected {expectedSha256}.");
            }

            return bytes;
        }
        finally
        {
            looseStream?.Dispose();
            packageStream?.Dispose();
        }
    }

    private static void VerifyPixels(string assetName, ReadOnlyMemory<byte>? productionPixels, ReadOnlySpan<byte> referencePixels)
    {
        if (!productionPixels.HasValue)
        {
            throw new InvalidDataException($"Verified retail selected-skill asset '{assetName}' was unavailable through the production loader.");
        }

        ReadOnlySpan<byte> pixels = productionPixels.Value.Span;

        if (!pixels.SequenceEqual(referencePixels))
        {
            throw new InvalidDataException($"{assetName} production RGBA SHA256 {ConformanceHash.Sha256(pixels)} does not match independent DXT3 reference SHA256 {ConformanceHash.Sha256(referencePixels)}.");
        }
    }

    private readonly record struct FrameFixture(string Path, string Sha256);
    private readonly record struct RenderCase(string Name, bool ImageActive, bool Covered);

    private sealed class ReferenceTextures : IDisposable
    {
        private readonly OpenGLTexture2D _selected;
        private readonly OpenGLTexture2D _cover;
        private bool _disposed;

        public ReferenceTextures(OpenGLGraphicsDevice graphicsDevice, IReadOnlyDictionary<string, byte[]> referencePixels)
        {
            ArgumentNullException.ThrowIfNull(graphicsDevice);
            ArgumentNullException.ThrowIfNull(referencePixels);

            OpenGLTexture2D? selected = null;
            OpenGLTexture2D? cover = null;

            try
            {
                selected = graphicsDevice.CreateTexture2D(FrameWidth, FrameHeight, referencePixels[s_selected.Path]);
                cover = graphicsDevice.CreateTexture2D(FrameWidth, FrameHeight, referencePixels[s_cover.Path]);
                _selected = selected;
                _cover = cover;
            }
            catch
            {
                try
                {
                    cover?.Dispose();
                }
                catch { }

                try
                {
                    selected?.Dispose();
                }
                catch { }
                throw;
            }
        }

        public OpenGLTexture2D Selected
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _selected;
            }
        }

        public OpenGLTexture2D Cover
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _cover;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ExceptionDispatchInfo? firstFailure = null;

            try
            {
                _cover.Dispose();
            }
            catch (Exception exception) { firstFailure = ExceptionDispatchInfo.Capture(exception); }

            try
            {
                _selected.Dispose();
            }
            catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }

            _disposed = true;
            firstFailure?.Throw();
        }
    }
}
