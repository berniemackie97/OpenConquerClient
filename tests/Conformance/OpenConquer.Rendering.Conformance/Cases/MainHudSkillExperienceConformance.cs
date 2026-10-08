using System.Runtime.ExceptionServices;
using OpenConquer.Client.UI.Hud.SkillExperience;
using OpenConquer.Content;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Reference;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class MainHudSkillExperienceConformance
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string SkillFrame0Path = "data/main/ProgressPower.dds";
    private const string SkillFrame2Path = "data/main/ProgressPowerH.dds";

    private const string ExpectedControlAniSha256 = "a1db47baaeb2f75eea3b5216378f97d713c2afeaefe05ec02e6f584794532d27";
    private const string ExpectedSkillFrame0Sha256 = "c32b0c502b7ab00aff2308a5708ac777fe6ee29ff101bfad056bd4c54ad94248";
    private const string ExpectedSkillFrame2Sha256 = "e4d10a40d7ead3d91a3b29cf5339baca487df1f41a40c8afa8c892c80f351868";

    private static readonly LogicalRenderSize[] s_logicalRenderSizes = [new(800, 600), new(1024, 768)];

    private static readonly RenderCase[] s_cases =
    [
        new("skill normal 50", CaseKind.SkillNormal50, new(50, 0, 0)),
        new("skill alternate 50", CaseKind.SkillAlternate50, new(50, 0, 0), ActivateAlternate: true),
        new("skill highlight whole", CaseKind.SkillHighlightWhole, new(50, 0, 0), ArmHighlight: true),
        new("skill positive zero-pixel", CaseKind.SkillPositiveZeroPixel, new(1, 0, 0)),
        new("experience 50", CaseKind.Experience50, new(0, 50, 100)),
        new("experience positive zero-pixel", CaseKind.ExperiencePositiveZeroPixel, new(0, 1, 1000)),
        new("experience negative", CaseKind.ExperienceNegative, new(0, uint.MaxValue, 100)),
        new("experience shift 16", CaseKind.ExperienceShift16, new(0, 0x8000_0000ul, 0x1_0000_0000ul)),
    ];

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PackagedClientContentSource contentSource, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        _ = ReadLooseOnlyAsset(contentSource, ControlAniPath, ExpectedControlAniSha256);

        byte[] skillFrame0Reference = Dxt3ReferenceDecoder.Decode(ReadPackageOnlyAsset(contentSource, SkillFrame0Path, ExpectedSkillFrame0Sha256), 128, 128);
        byte[] skillFrame2Reference = Dxt3ReferenceDecoder.Decode(ReadPackageOnlyAsset(contentSource, SkillFrame2Path, ExpectedSkillFrame2Sha256), 128, 128);

        MainHudSkillAssets assets = MainHudSkillAssets.Load(contentSource);

        if (!assets.HasSkill)
        {
            throw new InvalidDataException("Verified retail Progress42 assets did not produce an available main-HUD skill set.");
        }

        VerifyProductionDecode("Progress42 frame 0", assets.GetSkillFrame(0)?.Pixels, skillFrame0Reference);
        VerifyProductionDecode("Progress42 frame 1", assets.GetSkillFrame(1)?.Pixels, skillFrame0Reference);
        VerifyProductionDecode("Progress42 frame 2", assets.GetSkillFrame(2)?.Pixels, skillFrame2Reference);

        using ReferenceTextures referenceTextures = new(graphicsDevice, skillFrame0Reference, skillFrame2Reference);

        foreach (LogicalRenderSize logicalRenderSize in s_logicalRenderSizes)
        {
            using MainHudSkillExperienceRenderer skillExperienceRenderer = new(graphicsDevice, assets, logicalRenderSize);

            foreach (RenderCase testCase in s_cases)
            {
                RunRenderCase(graphicsDevice, skillExperienceRenderer, referenceTextures, logicalRenderSize, framebufferSize, colorFormat, testCase);
            }
        }
    }

    private static void RunRenderCase(OpenGLGraphicsDevice graphicsDevice, MainHudSkillExperienceRenderer skillExperienceRenderer, ReferenceTextures referenceTextures,
        LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, string colorFormat, RenderCase testCase)
    {
        MainHudSkillExperienceState state = CreateState(testCase);
        byte[] actual = RenderProduction(graphicsDevice, skillExperienceRenderer, state, logicalRenderSize, framebufferSize);
        byte[] expected = RenderReference(graphicsDevice, referenceTextures, logicalRenderSize, framebufferSize, testCase.Kind);

        FramebufferVerifier.VerifyExact($"Main HUD skill/experience {testCase.Name} {logicalRenderSize.Width}x{logicalRenderSize.Height}", expected, actual);

        Console.WriteLine($"Main HUD skill/experience: {testCase.Name}, {logicalRenderSize.Width}x{logicalRenderSize.Height}, {colorFormat}");
        Console.WriteLine($"Main HUD skill/experience framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static MainHudSkillExperienceState CreateState(RenderCase testCase)
    {
        MainHudSkillExperienceState state = new();

        if (testCase.ActivateAlternate)
        {
            state.ActivateSkillSubVariant();
        }

        if (testCase.ArmHighlight)
        {
            state.ArmSkillHighlight();
            state.AdvanceAfterHudDraw(1000);
        }

        state.SetSnapshot(testCase.Snapshot);

        return state;
    }

    private static byte[] RenderProduction(OpenGLGraphicsDevice graphicsDevice, MainHudSkillExperienceRenderer skillExperienceRenderer,
        MainHudSkillExperienceState state, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();
        skillExperienceRenderer.Draw(renderer, state);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static byte[] RenderReference(OpenGLGraphicsDevice graphicsDevice, ReferenceTextures textures, LogicalRenderSize logicalRenderSize,
        PixelSize framebufferSize, CaseKind kind)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        int skillY = logicalRenderSize.Height - 91;
        int experienceY = logicalRenderSize.Height - 45;

        renderer.BeginFrame();

        switch (kind)
        {
            case CaseKind.SkillNormal50:
                DrawSkill(renderer, textures.SkillFrame0, top: 46, skillY + 46, height: 46);
                break;

            case CaseKind.SkillAlternate50:
                DrawSkill(renderer, textures.SkillFrame2, top: 46, skillY + 46, height: 46);
                break;

            case CaseKind.SkillHighlightWhole:
                renderer.DrawSprite(textures.SkillFrame2, x: 0, y: skillY);
                break;

            case CaseKind.SkillPositiveZeroPixel:
                DrawSkill(renderer, textures.SkillFrame0, top: 92, skillY + 92, height: 128);
                break;

            case CaseKind.Experience50:
            case CaseKind.ExperienceShift16:
                DrawExperiencePositive(renderer, experienceY, width: 199);
                break;

            case CaseKind.ExperiencePositiveZeroPixel:
                DrawExperiencePositive(renderer, experienceY, width: 0);
                break;

            case CaseKind.ExperienceNegative:
                renderer.DrawSolidRectangle(x: 99, y: experienceY, width: 3, height: 4, new SpriteColor(0xFF, 0x00, 0x00, 0xFF));
                break;

            default:
                throw new InvalidOperationException($"Unhandled main-HUD skill/experience conformance case '{kind}'.");
        }

        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static void DrawSkill(OpenGLRenderer renderer, OpenGLTexture2D texture, int top, int y, int height)
    {
        renderer.DrawRepeatedSprite(texture, new SpriteSourceBounds(left: 0, top, right: 92, bottom: 92), x: 0, y, width: 92, height);
    }

    private static void DrawExperiencePositive(OpenGLRenderer renderer, int y, int width)
    {
        renderer.DrawSolidRectangle(x: 99, y, width, height: 1, new SpriteColor(0xF1, 0xD0, 0x6E, 0xFF));
        renderer.DrawSolidRectangle(x: 99, y: y + 1, width, height: 2, new SpriteColor(0xE8, 0xA3, 0x26, 0xFF));
        renderer.DrawSolidRectangle(x: 99, y: y + 3, width, height: 1, new SpriteColor(0xAB, 0x91, 0x6C, 0xFF));
    }

    private static void VerifyProductionDecode(string assetName, ReadOnlyMemory<byte>? productionPixels, ReadOnlySpan<byte> referencePixels)
    {
        if (!productionPixels.HasValue)
        {
            throw new InvalidDataException($"{assetName} was unavailable through the production HUD skill asset loader.");
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
            throw new InvalidDataException($"Retail HUD skill asset '{contentPath}' unexpectedly resolves as a loose file; verified 5517 requires package-backed lookup.");
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
            throw new InvalidDataException($"Retail HUD skill asset '{contentPath}' unexpectedly resolves from a package; verified 5517 requires loose lookup.");
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
            throw new InvalidDataException($"Retail HUD skill asset '{contentPath}' has SHA256 {actualSha256}; expected {expectedSha256}.");
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
        SkillNormal50,
        SkillAlternate50,
        SkillHighlightWhole,
        SkillPositiveZeroPixel,
        Experience50,
        ExperiencePositiveZeroPixel,
        ExperienceNegative,
        ExperienceShift16,
    }

    private readonly record struct RenderCase(string Name, CaseKind Kind, MainHudSkillExperienceSnapshot Snapshot, bool ActivateAlternate = false, bool ArmHighlight = false);

    private sealed class ReferenceTextures : IDisposable
    {
        private bool _disposed;

        public ReferenceTextures(OpenGLGraphicsDevice graphicsDevice, ReadOnlySpan<byte> skillFrame0, ReadOnlySpan<byte> skillFrame2)
        {
            OpenGLTexture2D? frame0 = null;
            OpenGLTexture2D? frame2 = null;

            try
            {
                frame0 = graphicsDevice.CreateTexture2D(128, 128, skillFrame0);
                frame2 = graphicsDevice.CreateTexture2D(128, 128, skillFrame2);

                SkillFrame0 = frame0;
                SkillFrame2 = frame2;
            }
            catch
            {
                DisposeCreatedTextures(frame2, frame0);
                throw;
            }
        }

        public OpenGLTexture2D SkillFrame0
        {
            get;
        }

        public OpenGLTexture2D SkillFrame2
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

            DisposeTexture(SkillFrame2, ref firstFailure);
            DisposeTexture(SkillFrame0, ref firstFailure);

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
