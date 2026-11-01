using System.Security.Cryptography;
using System.Text;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Client.UI.Hud.StatusHints.Equipment;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Text;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Reference;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class MainHudContextHintConformance
{
    private const int MagicFontHeight = 12;
    private const int MagicWrapPixelLimit = 180;
    private const int MagicWrapCharacterLimit = 30;
    private const int DataIconWidth = 21;
    private const int Padding = 5;

    private const string BackdropPath = "data/main/MsgDlg.dds";
    private const string BackdropHash = "c91b686fa8e9758dd8c8825e401503825434a0ebaca3be30e8e0263b52985719";
    private const string MainEquipmentText = "Switch to your Main Equipment.";
    private const string AlternateEquipmentText = "Switch to your Alternate Equipment.";

    private static readonly SpriteColor s_red = new(255, 0, 0, 255);
    private static readonly SpriteColor s_outline = new(204, 204, 204, 255);
    private static readonly SpriteColor s_equipmentBackground = new(80, 144, 200, 255);
    private static readonly SpriteSourceRectangle s_magicSource = new(0, 0, 100, 200);
    private static readonly SpriteSourceRectangle s_equipmentSource = new(0, 0, 100, 100);
    private static readonly LogicalRenderSize[] s_resolutions = [new(800, 600), new(1024, 768)];

    private static readonly EquipmentCase[] s_equipmentCases =
    [
        new("main", 1, false, 0, false, false, false),
        new("alternate-zero", 0, false, 0, false, false, false),
        new("alternate-two", 2, false, 0, false, false, false),
        new("alternate-max", uint.MaxValue, false, 0, false, false, false),
        new("main-last-slot", 1, false, 9, false, false, false),
        new("alternate-layout-main", 1, true, 0, false, false, false),
        new("alternate-layout-sub", 0, true, 0, false, false, false),
        new("alternate-layout-last-slot-main", 1, true, 9, false, false, false),
        new("alternate-layout-last-slot-sub", 0, true, 9, false, false, false),
        new("missing-localization", 1, false, 0, true, false, false),
        new("missing-backdrop", 1, false, 0, false, true, false),
        new("missing-state", null, false, 0, false, false, false),
        new("drag-suppressed", 1, false, 0, false, false, true),
    ];

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PackagedClientContentSource contentSource, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        MainHudMagicHintContent content = MainHudMagicHintContent.Load(contentSource);
        MainHudStatusHintAssets assets = MainHudStatusHintAssets.Load(contentSource);
        ClientFontSettingsConfiguration fontSettings = ClientFontSettingsConfiguration.Load(contentSource);
        ClientFontSizeConfiguration fontSize = ClientFontSizeConfiguration.Load(contentSource);
        ClientCodePageConfiguration codePage = ClientCodePageConfiguration.Load(contentSource);

        VerifyRetailInputs(content);

        using Stream stream = contentSource.OpenRequiredRead(BackdropPath, ContentLookupMode.PackageOnly);
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);

        byte[] encodedBackdrop = buffer.ToArray();
        string actualHash = Convert.ToHexStringLower(SHA256.HashData(encodedBackdrop));

        if (!string.Equals(actualHash, BackdropHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"HUD context-hint backdrop hash mismatch: {actualHash}.");
        }

        byte[] independentPixels = Dxt3ReferenceDecoder.Decode(encodedBackdrop, 256, 256);

        if (assets.Backdrop is not { } decodedBackdrop || !decodedBackdrop.Pixels.Span.SequenceEqual(independentPixels))
        {
            throw new InvalidDataException("HUD context-hint backdrop differs from independently decoded content.");
        }

        using OpenGLTexture2D referenceBackdrop = graphicsDevice.CreateTexture2D(256, 256, independentPixels);

        foreach (LogicalRenderSize resolution in s_resolutions)
        {
            RunResolution(graphicsDevice, content, assets, fontSettings, fontSize, codePage, referenceBackdrop, resolution, framebufferSize, colorFormat);
        }
    }

    private static void VerifyRetailInputs(MainHudMagicHintContent content)
    {
        if (content.Types.TryGet(3, 0, out _) || content.Types.TryGet(1411, 0, out _) ||
            !content.Types.TryGet(1380, 0, out _) || !content.Types.TryGet(1415, 0, out _))
        {
            throw new InvalidDataException("HUD dance catalog does not match the established selection contract.");
        }

        if (!content.KeyedStrings.GetEncoded("STR_SWAP_MAJOR_WEAPONTIP").Span.SequenceEqual("Switch to your Main Equipment."u8) ||
            !content.KeyedStrings.GetEncoded("STR_SWAP_SUB_WEAPONTIP").Span.SequenceEqual("Switch to your Alternate Equipment."u8))
        {
            throw new InvalidDataException("HUD equipment-switch localization differs from the verified retail strings.");
        }

        if (!content.KeyedStrings.GetEncoded("STR_NOTSTUDY_DANCE").Span.SequenceEqual("You haven`t learned a dance skill."u8) ||
            !content.KeyedStrings.GetEncoded("STR_NOT_COOL_DANCE_STATE").Span.SequenceEqual("Usable only as a Performer."u8))
        {
            throw new InvalidDataException("HUD dance warning localization differs from the verified retail strings.");
        }
    }

    private static void RunResolution(OpenGLGraphicsDevice graphicsDevice, MainHudMagicHintContent content, MainHudStatusHintAssets assets,
        ClientFontSettingsConfiguration fontSettings, ClientFontSizeConfiguration fontSize, ClientCodePageConfiguration codePage,
        OpenGLTexture2D referenceBackdrop, LogicalRenderSize resolution, PixelSize framebufferSize, string colorFormat)
    {
        using MainHudStatusHintRenderer shared = new(graphicsDevice, assets, fontSettings, fontSize, codePage, resolution);
        using MainHudMagicHintRenderer magic = new(graphicsDevice, shared, content, assets.Strings, fontSettings, codePage, resolution);
        using OpenGLTextContext referenceMagicText = new(graphicsDevice, fontSettings.GuiFontFaceName, MagicFontHeight, codePage.EffectiveCodePage, fontSettings.AntialiasEnabled);
        using OpenGLTextContext referenceEquipmentText = new(graphicsDevice, fontSettings.GuiFontFaceName, fontSize.NormalFontHeightPixels, codePage.EffectiveCodePage, fontSettings.AntialiasEnabled);

        NativeTextRenderOptions white = CreateOptions(fontSettings, SpriteColor.White);
        NativeTextRenderOptions red = CreateOptions(fontSettings, s_red);

        RunDanceCases(graphicsDevice, magic, referenceMagicText, referenceBackdrop, resolution, framebufferSize, colorFormat, white, red);
        VerifyEquipmentBackdropSensitivity(graphicsDevice, referenceEquipmentText, referenceBackdrop, resolution, framebufferSize, fontSize.NormalFontHeightPixels, white);

        foreach (EquipmentCase testCase in s_equipmentCases)
        {
            RunEquipmentCase(graphicsDevice, shared, content.KeyedStrings, assets, fontSettings, fontSize, codePage,
                referenceEquipmentText, referenceBackdrop, resolution, framebufferSize, colorFormat, white, testCase);
        }
    }

    private static void RunDanceCases(OpenGLGraphicsDevice graphicsDevice, MainHudMagicHintRenderer production, OpenGLTextContext referenceText,
        OpenGLTexture2D backdrop, LogicalRenderSize resolution, PixelSize framebufferSize, string colorFormat,
        NativeTextRenderOptions white, NativeTextRenderOptions red)
    {
        DanceCase[] cases =
        [
            new("quickbar-context-no-record", 3, 90, resolution.Height - 43, null, 0, 0, 0, []),
            new("missing-catalog-dance", 1411, 90, resolution.Height - 43, null, 0, 0, 0, []),
            new("unlearned-dance2", 1380, 105, 205, null, 0, 0, 0,
            [
                ExpectedGroup.White("Dance2"),
                ExpectedGroup.White("Level: Elementary"),
                ExpectedGroup.White("Dance"),
                ExpectedGroup.White("Fixed"),
                ExpectedGroup.Red("You haven`t learned a dance skill."),
            ]),
            new("learned-dance2-static-preview", 1380, 105, 205, 4, 0, 0, 0,
            [
                ExpectedGroup.White("Dance2"),
                ExpectedGroup.White("Level: Elementary"),
                ExpectedGroup.White("Dance"),
                ExpectedGroup.White("Fixed"),
            ]),
            new("unlearned-battle-dance", 1415, 105, 205, null, 0, 0, 0,
            [
                ExpectedGroup.White("BattleDance"),
                ExpectedGroup.White("Level: Elementary"),
                ExpectedGroup.Red("Requires: P1 Performer(Switch to P1 Performer to use BattleDance)"),
                ExpectedGroup.White("Unique Dance"),
                ExpectedGroup.White("Fixed"),
                ExpectedGroup.Red("You haven`t learned a dance skill."),
            ]),
            new("learned-battle-dance-nonperformer", 1415, 450, resolution.Height - 43, 0, 0, 0, 0,
            [
                ExpectedGroup.White("BattleDance"),
                ExpectedGroup.White("Level: Elementary"),
                ExpectedGroup.Red("Requires: P1 Performer(Switch to P1 Performer to use BattleDance)"),
                ExpectedGroup.White("Unique Dance"),
                ExpectedGroup.White("Fixed"),
                ExpectedGroup.Red("Usable only as a Performer."),
            ]),
            new("learned-battle-dance-performer", 1415, 90, resolution.Height - 43, 2, 6, 1, 100000,
            [
                ExpectedGroup.White("BattleDance"),
                ExpectedGroup.White("Level: Elementary"),
                ExpectedGroup.White("Requires: P1 Performer"),
                ExpectedGroup.White("Unique Dance"),
                ExpectedGroup.White("Fixed"),
            ]),
        ];

        foreach (DanceCase testCase in cases)
        {
            MainHudStatusHintState state = new();
            state.SetMagicAnchor(testCase.AnchorX, testCase.AnchorY);
            state.SelectDance(testCase.Type);

            MainHudLearnedMagic[] learned = testCase.LearnedLevel is uint level
                ? [new MainHudLearnedMagic(testCase.Type, level, 999999)]
                : [];

            MainHudMagicHintRuntimeSnapshot runtime = new(learned, 130, testCase.ActiveClass, testCase.ActivePhase, testCase.PackedPhases, null, default);

            byte[] actual = Capture(graphicsDevice, resolution, framebufferSize, renderer => production.Draw(renderer, state, runtime));
            byte[] expected = testCase.Groups.Length == 0
                ? FramebufferVerifier.CreateOpaqueBlack(resolution.Width, resolution.Height)
                : Capture(graphicsDevice, resolution, framebufferSize, renderer => DrawDanceReference(renderer, referenceText, backdrop, resolution,
                    testCase.AnchorX, testCase.AnchorY, testCase.Groups, white, red));

            VerifyFrame($"dance {testCase.Name}", resolution, colorFormat, expected, actual);
        }

        MainHudStatusHintState suppressed = new();
        suppressed.SetMagicAnchor(105, 205);
        suppressed.SelectDance(1380);
        suppressed.SetSkillDragActive(true);

        MainHudMagicHintRuntimeSnapshot available = new([], 130, 0, 0, 0, null, default);
        byte[] black = FramebufferVerifier.CreateOpaqueBlack(resolution.Width, resolution.Height);

        byte[] dragged = Capture(graphicsDevice, resolution, framebufferSize, renderer => production.Draw(renderer, suppressed, available));
        FramebufferVerifier.VerifyExact($"HUD dance drag suppression {resolution.Width}x{resolution.Height}", black, dragged);

        suppressed.SetSkillDragActive(false);

        byte[] missingRuntime = Capture(graphicsDevice, resolution, framebufferSize, renderer => production.Draw(renderer, suppressed, null));
        FramebufferVerifier.VerifyExact($"HUD dance unavailable runtime {resolution.Width}x{resolution.Height}", black, missingRuntime);
    }

    private static void DrawDanceReference(OpenGLRenderer renderer, OpenGLTextContext text, OpenGLTexture2D backdrop, LogicalRenderSize resolution,
        int anchorX, int anchorY, IReadOnlyList<ExpectedGroup> groups, NativeTextRenderOptions white, NativeTextRenderOptions red)
    {
        int fontWidth = text.Layout("M"u8).WidthPixels;
        int maximumWidth = 0;
        int totalLines = 0;

        foreach (ExpectedGroup group in groups)
        {
            IReadOnlyList<byte[]> lines = WrapReference(text, group.Encoded, fontWidth);
            totalLines += lines.Count;

            foreach (byte[] line in lines)
            {
                maximumWidth = Math.Max(maximumWidth, text.Layout(line, recognizeDataIcons: true, dataIconWidthPixels: DataIconWidth).WidthPixels);
            }
        }

        int width = maximumWidth + Padding * 2;
        int height = totalLines * MagicFontHeight + Padding * 2;
        int x = (long)anchorX + width + 2 <= resolution.Width ? anchorX : resolution.Width - width - 2;
        int y = Math.Max(0, anchorY - height);

        renderer.DrawSprite(backdrop, s_magicSource, x, y, width, height);
        renderer.DrawLineRectangle(x, y, x + width, y + height, s_outline);

        int textX = x + Padding;
        int textY = y + Padding;

        foreach (ExpectedGroup group in groups)
        {
            NativeTextRenderOptions options = group.Color == s_red ? red : white;

            if (group.Encoded.Length <= MagicWrapCharacterLimit)
            {
                DrawReferenceLine(renderer, text, group.Encoded, options, textX, textY);
                textY += MagicFontHeight;
                continue;
            }

            foreach (byte[] line in WrapReference(text, group.Encoded, fontWidth))
            {
                DrawReferenceLine(renderer, text, line, options, textX, textY);
                textY += MagicFontHeight;
            }
        }
    }

    private static IReadOnlyList<byte[]> WrapReference(OpenGLTextContext text, ReadOnlySpan<byte> encoded, int fontWidth)
        => MagicHintTextWrapper.Wrap(encoded, MagicWrapPixelLimit, fontWidth, span => text.Layout(span).WidthPixels);

    private static void DrawReferenceLine(OpenGLRenderer renderer, OpenGLTextContext text, ReadOnlySpan<byte> encoded,
        NativeTextRenderOptions options, int x, int y)
    {
        if (encoded.IsEmpty)
        {
            return;
        }

        text.Draw(renderer, text.Layout(encoded), options, x, y);
    }

    private static void VerifyEquipmentBackdropSensitivity(OpenGLGraphicsDevice graphicsDevice, OpenGLTextContext referenceText,
        OpenGLTexture2D backdrop, LogicalRenderSize resolution, PixelSize framebufferSize, int fontHeight, NativeTextRenderOptions options)
    {
        int anchorX = 90;
        int anchorY = resolution.Height - 43;

        byte[] withBackdrop = Capture(graphicsDevice, resolution, framebufferSize,
            renderer => DrawEquipmentReference(renderer, referenceText, backdrop, resolution, anchorX, anchorY,
                MainEquipmentText, fontHeight, false, options), coloredBackground: true);

        byte[] withoutBackdrop = Capture(graphicsDevice, resolution, framebufferSize,
            renderer => DrawEquipmentReference(renderer, referenceText, null, resolution, anchorX, anchorY,
                MainEquipmentText, fontHeight, false, options), coloredBackground: true);

        if (withBackdrop.AsSpan().SequenceEqual(withoutBackdrop))
        {
            throw new InvalidDataException($"HUD equipment backdrop reference is visually insensitive at {resolution.Width}x{resolution.Height}.");
        }

        byte[] emptyTextBackdrop = Capture(graphicsDevice, resolution, framebufferSize,
            renderer => DrawEquipmentReference(renderer, referenceText, backdrop, resolution, anchorX, anchorY,
                string.Empty, fontHeight, false, options), coloredBackground: true);

        byte[] backgroundOnly = Capture(graphicsDevice, resolution, framebufferSize,
            _ => { }, coloredBackground: true);

        if (emptyTextBackdrop.AsSpan().SequenceEqual(backgroundOnly))
        {
            throw new InvalidDataException($"HUD equipment empty-localization backdrop is visually insensitive at {resolution.Width}x{resolution.Height}.");
        }
    }

    private static void RunEquipmentCase(OpenGLGraphicsDevice graphicsDevice, MainHudStatusHintRenderer shared,
        ClientKeyedStringResources keyedStrings, MainHudStatusHintAssets assets, ClientFontSettingsConfiguration fontSettings,
        ClientFontSizeConfiguration fontSize, ClientCodePageConfiguration codePage, OpenGLTextContext referenceText,
        OpenGLTexture2D referenceBackdrop, LogicalRenderSize resolution, PixelSize framebufferSize, string colorFormat,
        NativeTextRenderOptions options, EquipmentCase testCase)
    {
        using MainHudStatusHintRenderer? withoutBackdrop = testCase.MissingBackdrop
            ? new MainHudStatusHintRenderer(assets, MainHudStatusHintLayout.Create(resolution), null,
                new OpenGLTextContext(graphicsDevice, fontSettings.GuiFontFaceName, fontSize.NormalFontHeightPixels,
                    codePage.EffectiveCodePage, fontSettings.AntialiasEnabled), options)
            : null;

        MainHudStatusHintRenderer activeShared = withoutBackdrop ?? shared;
        ClientKeyedStringResources activeStrings = testCase.MissingLocalization
            ? ClientKeyedStringResources.Parse(ReadOnlySpan<byte>.Empty)
            : keyedStrings;

        MainHudEquipmentHintRenderer production = new(activeShared, activeStrings, fontSize, resolution, testCase.AlternateLayout);

        MainHudStatusHintState state = new();
        int anchorX = 90 + 40 * testCase.SlotIndex;
        int anchorY = resolution.Height - 43;

        state.SetMagicAnchor(anchorX, anchorY);
        state.SelectEquipmentSwitch(0x5E);
        state.SetSkillDragActive(testCase.Dragged);

        byte[] actual = Capture(graphicsDevice, resolution, framebufferSize,
            renderer => production.Draw(renderer, state, testCase.State), coloredBackground: true);

        byte[] expected;

        if (testCase.Dragged || testCase.State is null)
        {
            expected = Capture(graphicsDevice, resolution, framebufferSize,
                _ => { }, coloredBackground: true);
        }
        else
        {
            string message = testCase.MissingLocalization
                ? string.Empty
                : testCase.State == 1 ? MainEquipmentText : AlternateEquipmentText;

            expected = Capture(graphicsDevice, resolution, framebufferSize,
                renderer => DrawEquipmentReference(renderer, referenceText, testCase.MissingBackdrop ? null : referenceBackdrop,
                    resolution, anchorX, anchorY, message, fontSize.NormalFontHeightPixels, testCase.AlternateLayout, options),
                coloredBackground: true);
        }

        VerifyFrame($"equipment {testCase.Name}", resolution, colorFormat, expected, actual);
    }

    private static void DrawEquipmentReference(OpenGLRenderer renderer, OpenGLTextContext text, OpenGLTexture2D? backdrop,
        LogicalRenderSize resolution, int anchorX, int anchorY, string message, int fontHeight, bool alternateLayout,
        NativeTextRenderOptions options)
    {
        byte[] encoded = Encoding.ASCII.GetBytes(message);
        OpenGLTextLayout layout = text.Layout(encoded, recognizeDataIcons: true, dataIconWidthPixels: DataIconWidth);
        int width = layout.WidthPixels;

        int x = unchecked(anchorX + (alternateLayout ? width : 0) - 3);
        int y = unchecked(anchorY + 42 - 60);
        int verticalExtent = unchecked(-12 - fontHeight);

        if (unchecked(x + width) > resolution.Width)
        {
            x = unchecked(resolution.Width - width);
        }
        else if (x < 0)
        {
            x = 10;
        }

        if (unchecked(y + verticalExtent) > resolution.Height)
        {
            y = unchecked(resolution.Height - verticalExtent);
        }
        else if (y < 0)
        {
            y = 10;
        }

        int backdropHeight = unchecked((int)((uint)unchecked(3 * fontHeight) >> 1) + 10);

        if (backdrop is not null)
        {
            renderer.DrawSprite(backdrop, s_equipmentSource, unchecked(x - 5), unchecked(y - 5), unchecked(width + 10), backdropHeight);
        }

        text.Draw(renderer, layout, options, alternateLayout ? unchecked(x + width) : x, y);
    }

    private static byte[] Capture(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize resolution, PixelSize framebufferSize,
        Action<OpenGLRenderer> draw, bool coloredBackground = false)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(resolution, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();

        if (coloredBackground)
        {
            renderer.DrawSolidRectangle(0, 0, resolution.Width, resolution.Height, s_equipmentBackground);
        }

        draw(renderer);

        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static NativeTextRenderOptions CreateOptions(ClientFontSettingsConfiguration settings, SpriteColor color)
    {
        uint argb = settings.DefaultCornerColorArgb;
        SpriteColor corner = new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));

        return new NativeTextRenderOptions((NativeTextRenderStyle)settings.DefaultRenderTextStyle, color, corner,
            ClientFontSettingsConfiguration.DefaultCornerOffsetXPixels, ClientFontSettingsConfiguration.DefaultCornerOffsetYPixels,
            NativeTextVertexColors.Solid(color));
    }

    private static void VerifyFrame(string name, LogicalRenderSize resolution, string colorFormat, byte[] expected, byte[] actual)
    {
        string label = $"Main HUD context hint {name} {resolution.Width}x{resolution.Height}";
        FramebufferVerifier.VerifyExact(label, expected, actual);

        Console.WriteLine($"{label}, {colorFormat}");
        Console.WriteLine($"Context-hint framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private readonly record struct ExpectedGroup(byte[] Encoded, SpriteColor Color)
    {
        public static ExpectedGroup White(string text) => new(Encoding.Latin1.GetBytes(text), SpriteColor.White);
        public static ExpectedGroup Red(string text) => new(Encoding.Latin1.GetBytes(text), s_red);
    }

    private readonly record struct DanceCase(string Name, uint Type, int AnchorX, int AnchorY, uint? LearnedLevel,
        int ActiveClass, int ActivePhase, long PackedPhases, ExpectedGroup[] Groups);

    private readonly record struct EquipmentCase(string Name, uint? State, bool AlternateLayout, int SlotIndex,
        bool MissingLocalization, bool MissingBackdrop, bool Dragged);
}
