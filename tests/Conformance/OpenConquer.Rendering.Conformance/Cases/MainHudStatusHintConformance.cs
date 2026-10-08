using System.Security.Cryptography;
using System.Text;
using OpenConquer.Client.UI.Hud.CheckControls;
using OpenConquer.Client.UI.Hud.SkillExperience;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Client.UI.Hud.Vitals;
using OpenConquer.Content;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Reference;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Glyphs;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class MainHudStatusHintConformance
{
    private const string BackdropPath = "data/main/MsgDlg.dds";
    private const string BackdropSha256 = "c91b686fa8e9758dd8c8825e401503825434a0ebaca3be30e8e0263b52985719";
    private const string StringsSha256 = "efe9f5f1d3e29bb79b9970c25dcb238179e3ab6e059e94d981b37236157cd072";
    private const int TextureSize = 256;

    private static readonly LogicalRenderSize[] s_resolutions = [new(800, 600), new(1024, 768)];

    private static readonly (string Path, string Sha256)[] s_regions =
    [
        ("ini/ProgressXp.rgn", "8bbc96e993105cb97ff01d6ea5201c406d88aa0b91a5055b95617b89f28c62bc"),
        ("ini/ProgressMp.rgn", "3aefd750231895a99d062276d460164d1004fbde7e3bbe3e922f1dea5bf743f4"),
        ("ini/ProgressHp.rgn", "b7e8ed940da85bbe09e2eb96f7a074ca29f5d29d9cd121fb326373cb5540cea1"),
    ];

    private static readonly RenderCase[] s_cases =
    [
        new("walk", MainHudStatusHintKind.WalkRun, "Walk/Run", 0, 23, false),
        new("map", MainHudStatusHintKind.Map, "Map On/Off", 72, 23, false),
        new("screen-off", MainHudStatusHintKind.ScreenShift, "Shift Screen: Off", 50, 11, false),
        new("screen-on", MainHudStatusHintKind.ScreenShift, "Shift Screen: On", 50, 11, true),
        new("equipment", MainHudStatusHintKind.Equipment, "View Equipment", 22, 11, false),
        new("skill", MainHudStatusHintKind.Skill, "58/100", 0, 50, false),
        new("mana", MainHudStatusHintKind.Mana, "13/25", 52, 54, false),
        new("life", MainHudStatusHintKind.Life, "42/100", 4, 54, false),
    ];

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PackagedClientContentSource contentSource, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        VerifyRetailFile(contentSource, "ini/StrRes.ini", StringsSha256);

        foreach ((string path, string sha256) in s_regions)
        {
            VerifyRetailFile(contentSource, path, sha256);
        }

        byte[] encodedBackdrop = ReadVerifiedPackagedBackdrop(contentSource);
        byte[] referencePixels = Dxt3ReferenceDecoder.Decode(encodedBackdrop, TextureSize, TextureSize);

        MainHudStatusHintAssets assets = MainHudStatusHintAssets.Load(contentSource);

        if (assets.Strings.UsesArabicLayout)
        {
            throw new InvalidDataException("Clean retail 5517 unexpectedly selected the alternate Arabic text layout.");
        }

        if (assets.Backdrop is null || !assets.Backdrop.Pixels.Span.SequenceEqual(referencePixels))
        {
            throw new InvalidDataException("The production Dialog21 decoder disagrees with the independent DXT3 reference decoder.");
        }

        using OpenGLTexture2D referenceTexture = graphicsDevice.CreateTexture2D(TextureSize, TextureSize, referencePixels);

        foreach (LogicalRenderSize logicalRenderSize in s_resolutions)
        {
            RunResolution(graphicsDevice, assets, referenceTexture, logicalRenderSize, framebufferSize, colorFormat);
        }
    }

    private static void RunResolution(OpenGLGraphicsDevice graphicsDevice, MainHudStatusHintAssets assets, OpenGLTexture2D referenceTexture, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, string colorFormat)
    {
        OpenGLTexture2D? productionTexture = null;
        OpenGLTextContext? textContext = null;

        try
        {
            productionTexture = graphicsDevice.CreateTexture2D(TextureSize, TextureSize, assets.Backdrop!.Pixels.Span);
            textContext = new OpenGLTextContext(graphicsDevice, new SyntheticHintGlyphRasterizer(), nominalPixelHeight: 1, effectiveCodePage: 936);

            NativeTextRenderOptions options = new(
                NativeTextRenderStyle.Normal,
                SpriteColor.White,
                SpriteColor.White,
                cornerOffsetXPixels: 1,
                cornerOffsetYPixels: 1,
                NativeTextVertexColors.Solid(SpriteColor.White));

            MainHudStatusHintRenderer hintRenderer = new(
                assets,
                MainHudStatusHintLayout.Create(logicalRenderSize),
                productionTexture,
                textContext,
                options);

            productionTexture = null;
            textContext = null;

            using (hintRenderer)
            {
                VerifyReferenceTextSensitivity(graphicsDevice, referenceTexture, logicalRenderSize, framebufferSize, options);

                foreach (RenderCase testCase in s_cases)
                {
                    RunCase(graphicsDevice, hintRenderer, referenceTexture, logicalRenderSize, framebufferSize, colorFormat, options, testCase);
                }

                VerifySuppressedCase(graphicsDevice, hintRenderer, logicalRenderSize, framebufferSize, "mana-uninitialized", MainHudStatusHintKind.Mana, skillDrag: false, initializeMana: false);
                VerifySuppressedCase(graphicsDevice, hintRenderer, logicalRenderSize, framebufferSize, "skill-drag", MainHudStatusHintKind.Life, skillDrag: true, initializeMana: true);
                VerifySuppressedCase(graphicsDevice, hintRenderer, logicalRenderSize, framebufferSize, "zero-hotspot", MainHudStatusHintKind.None, skillDrag: false, initializeMana: true);
            }
        }
        finally
        {
            try
            {
                textContext?.Dispose();
            }
            finally
            {
                productionTexture?.Dispose();
            }
        }
    }

    private static void RunCase(OpenGLGraphicsDevice graphicsDevice, MainHudStatusHintRenderer hintRenderer, OpenGLTexture2D referenceTexture, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, string colorFormat, NativeTextRenderOptions textOptions, RenderCase testCase)
    {
        MainHudStatusHintState state = new();
        MainHudCheckControlsState checks = new();
        MainHudVitalsState vitals = new();
        MainHudSkillExperienceState skill = new();

        state.Select((int)testCase.Kind);

        if (testCase.ScreenShift)
        {
            checks.GetControl(MainHudCheckControlId.Check46).SetState(1);
        }

        vitals.SetSnapshot(new MainHudVitalsSnapshot(42, 42, 100, 13, 13, 25, 0, 100, false));
        skill.SetSnapshot(new MainHudSkillExperienceSnapshot(58, 0, 1));

        byte[] actual = RenderProduction(graphicsDevice, hintRenderer, state, checks, vitals, skill, logicalRenderSize, framebufferSize);
        byte[] expected = RenderReference(graphicsDevice, referenceTexture, logicalRenderSize, framebufferSize, textOptions, testCase);

        string label = $"Main HUD category-8 status hint {testCase.Name} {logicalRenderSize.Width}x{logicalRenderSize.Height}";

        FramebufferVerifier.VerifyExact(label, expected, actual);

        Console.WriteLine($"{label}, {colorFormat}");
        Console.WriteLine($"Main HUD status-hint framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static void VerifyReferenceTextSensitivity(OpenGLGraphicsDevice graphicsDevice, OpenGLTexture2D referenceTexture, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, NativeTextRenderOptions textOptions)
    {
        RenderCase manaCase = s_cases.Single(static testCase => testCase.Kind == MainHudStatusHintKind.Mana);

        byte[] expected = RenderReference(graphicsDevice, referenceTexture, logicalRenderSize, framebufferSize, textOptions, manaCase);
        byte[] incorrect = RenderReference(graphicsDevice, referenceTexture, logicalRenderSize, framebufferSize, textOptions, manaCase with
        {
            Text = "12/25"
        });

        if (expected.AsSpan().SequenceEqual(incorrect))
        {
            throw new InvalidDataException("The status-hint reference renderer cannot distinguish different same-length gauge values.");
        }
    }

    private static void VerifySuppressedCase(OpenGLGraphicsDevice graphicsDevice, MainHudStatusHintRenderer hintRenderer, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, string name, MainHudStatusHintKind kind, bool skillDrag, bool initializeMana)
    {
        MainHudStatusHintState state = new();
        MainHudCheckControlsState checks = new();
        MainHudVitalsState vitals = new();
        MainHudSkillExperienceState skill = new();

        state.Select((int)kind);
        state.SetSkillDragActive(skillDrag);

        vitals.SetSnapshot(new MainHudVitalsSnapshot(
            42, 42, 100,
            initializeMana ? 13 : 0,
            initializeMana ? 13 : 0,
            25, 0, 100, false));

        byte[] actual = RenderProduction(graphicsDevice, hintRenderer, state, checks, vitals, skill, logicalRenderSize, framebufferSize);
        byte[] expected = FramebufferVerifier.CreateOpaqueBlack(logicalRenderSize.Width, logicalRenderSize.Height);

        string label = $"Main HUD category-8 status hint {name} {logicalRenderSize.Width}x{logicalRenderSize.Height}";

        FramebufferVerifier.VerifyExact(label, expected, actual);

        Console.WriteLine($"{label}: suppressed as required");
    }

    private static byte[] RenderProduction(OpenGLGraphicsDevice graphicsDevice, MainHudStatusHintRenderer hintRenderer, MainHudStatusHintState state, MainHudCheckControlsState checks, MainHudVitalsState vitals, MainHudSkillExperienceState skill, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();
        hintRenderer.Draw(renderer, state, checks, vitals, skill);
        byte[] pixels = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return pixels;
    }

    private static byte[] RenderReference(OpenGLGraphicsDevice graphicsDevice, OpenGLTexture2D backdrop, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, NativeTextRenderOptions textOptions, RenderCase testCase)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);
        using OpenGLTextContext textContext = new(graphicsDevice, new SyntheticHintGlyphRasterizer(), nominalPixelHeight: 1, effectiveCodePage: 936);

        int x = testCase.LocalX;
        int y = logicalRenderSize.Height - 161 + testCase.LocalY;
        OpenGLTextLayout textLayout = textContext.Layout(Encoding.ASCII.GetBytes(testCase.Text));

        renderer.BeginFrame();
        renderer.DrawSprite(backdrop, new SpriteSourceRectangle(0, 0, 100, 200), x, y, textLayout.WidthPixels, textLayout.HeightPixels);
        textContext.Draw(renderer, textLayout, textOptions, x, y);
        byte[] pixels = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return pixels;
    }

    private static void VerifyRetailFile(PackagedClientContentSource source, string path, string expectedSha256)
    {
        using Stream stream = source.OpenRequiredRead(path, ContentLookupMode.LooseOnly);
        string actual = Convert.ToHexStringLower(SHA256.HashData(stream));

        if (!string.Equals(actual, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Retail status-hint file '{path}' has SHA256 {actual}; expected {expectedSha256}.");
        }
    }

    private static byte[] ReadVerifiedPackagedBackdrop(PackagedClientContentSource source)
    {
        bool foundLoose = source.TryOpenRead(BackdropPath, ContentLookupMode.LooseOnly, out Stream? looseStream);
        bool foundPackage = source.TryOpenRead(BackdropPath, ContentLookupMode.PackageOnly, out Stream? packageStream);

        try
        {
            if (foundLoose || !foundPackage)
            {
                throw new InvalidDataException($"Retail status-hint frame '{BackdropPath}' must resolve exclusively from its declared WDF package.");
            }

            using MemoryStream buffer = new();
            packageStream!.CopyTo(buffer);

            byte[] bytes = buffer.ToArray();
            string actual = Convert.ToHexStringLower(SHA256.HashData(bytes));

            if (!string.Equals(actual, BackdropSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Retail status-hint frame '{BackdropPath}' has SHA256 {actual}; expected {BackdropSha256}.");
            }

            return bytes;
        }
        finally
        {
            looseStream?.Dispose();
            packageStream?.Dispose();
        }
    }

    private readonly record struct RenderCase(string Name, MainHudStatusHintKind Kind, string Text, int LocalX, int LocalY, bool ScreenShift);

    private sealed class SyntheticHintGlyphRasterizer : IGlyphRasterizer
    {
        private bool _disposed;

        public bool AntialiasEnabled => true;

        public bool TryRasterizeGlyph(Rune rune, out RasterizedGlyph? glyph)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (rune.Value is < 0x20 or > 0x7E)
            {
                glyph = null;
                return false;
            }

            byte coverage = (byte)(64 + rune.Value * 73 % 192);

            glyph = new RasterizedGlyph(widthPixels: 1, heightPixels: 1, bearingLeftPixels: 0, topOffsetPixels: 0, advancePixels: 1, [coverage]);
            return true;
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
