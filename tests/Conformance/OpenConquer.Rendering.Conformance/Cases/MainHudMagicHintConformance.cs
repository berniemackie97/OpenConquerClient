using System.Security.Cryptography;
using System.Text;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Magic;
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

internal static class MainHudMagicHintConformance
{
    private const int FontHeight = 12;
    private const int WrapPixelLimit = 180;
    private const int WrapCharacterLimit = 30;
    private const int DataIconWidth = 21;
    private const int Padding = 5;
    private const string BackdropPath = "data/main/MsgDlg.dds";

    private static readonly SpriteColor s_red = new(255, 0, 0, 255);
    private static readonly SpriteColor s_border = new(204, 204, 204, 255);
    private static readonly SpriteSourceRectangle s_backdropSource = new(0, 0, 100, 200);
    private static readonly LogicalRenderSize[] s_resolutions = [new(800, 600), new(1024, 768)];

    private static readonly (string Path, string Hash)[] s_retailFiles =
    [
        ("ini/MagicType.dat", "e8b9559e640c75ed61fc859743f18bd372e6c5d9051457a639cedff9ba2a6e3d"),
        ("ini/MagicEffect.ini", "092091f8ff22ce700e3fe7562a767baa51fde3516d9df635e7dfb1a60ad1ad0b"),
        ("ini/Cn_Res.ini", "23c23e12a954a97c202bb10cab86c6c7253c55232aa7491cdadbc52070db6026"),
        ("ini/SubProfessionInfo.ini", "a5bf8d02082f429f082c4b3ac2614b292f8eb0f2493118b07c1b16eb9152c63e"),
        ("ini/StrRes.ini", "efe9f5f1d3e29bb79b9970c25dcb238179e3ab6e059e94d981b37236157cd072"),
    ];

    private static readonly LearnedCase[] s_learnedCases =
    [
        new("thunder-half", 1000, 1000, 0, 0, 0, 0,
        [
            ExpectedGroup.White("Thunder"),
            ExpectedGroup.White("Level: Elementary"),
            ExpectedGroup.White("Magic attack"),
            ExpectedGroup.White("EXP: 50.000% "),
        ]),
        new("thunder-overfull", 1000, 3000, 0, 0, 0, 0,
        [
            ExpectedGroup.White("Thunder"),
            ExpectedGroup.White("Level: Elementary"),
            ExpectedGroup.White("Magic attack"),
            ExpectedGroup.White("EXP: 150.000% "),
        ]),
        new("thunder-last-slot", 1000, 1000, 9, 0, 0, 0,
        [
            ExpectedGroup.White("Thunder"),
            ExpectedGroup.White("Level: Elementary"),
            ExpectedGroup.White("Magic attack"),
            ExpectedGroup.White("EXP: 50.000% "),
        ]),
        new("dance-requirement-satisfied", 1415, 0, 0, 6, 1, 100000,
        [
            ExpectedGroup.White("BattleDance"),
            ExpectedGroup.White("Level: Elementary"),
            ExpectedGroup.White("Requires: P1 Performer"),
            ExpectedGroup.White("Unique Dance"),
            ExpectedGroup.White("Fixed"),
        ]),
        new("dance-requirement-switch-warning", 1415, 0, 0, 0, 0, 0,
        [
            ExpectedGroup.White("BattleDance"),
            ExpectedGroup.White("Level: Elementary"),
            ExpectedGroup.Red("Requires: P1 Performer(Switch to P1 Performer to use BattleDance)"),
            ExpectedGroup.White("Unique Dance"),
            ExpectedGroup.White("Fixed"),
            ExpectedGroup.Red("Usable only as a Performer."),
        ]),
        new("mount-unequipped", 7001, 0, 0, 0, 0, 0,
        [
            ExpectedGroup.White("Riding"),
            ExpectedGroup.White("Level: Elementary"),
            ExpectedGroup.White("Unequipped\u00A1\u00A3"),
            ExpectedGroup.White("Fixed"),
        ]),
    ];

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PackagedClientContentSource contentSource, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        foreach ((string path, string hash) in s_retailFiles)
        {
            using Stream stream = contentSource.OpenRequiredRead(path, ContentLookupMode.LooseOnly);
            string actual = Convert.ToHexStringLower(SHA256.HashData(stream));

            if (!string.Equals(actual, hash, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Category-9 retail input '{path}' has SHA256 {actual}; expected {hash}.");
            }
        }

        MainHudMagicHintContent content = MainHudMagicHintContent.Load(contentSource);
        MainHudStatusHintAssets assets = MainHudStatusHintAssets.Load(contentSource);
        ClientFontSettingsConfiguration fontSettings = ClientFontSettingsConfiguration.Load(contentSource);
        ClientFontSizeConfiguration fontSize = ClientFontSizeConfiguration.Load(contentSource);
        ClientCodePageConfiguration codePage = ClientCodePageConfiguration.Load(contentSource);

        if (assets.Strings.UsesArabicLayout)
        {
            throw new InvalidDataException("Clean English 5517 unexpectedly selected the alternate category-9 text layout.");
        }

        VerifyRetailRecords(content);

        if (content.TryWrapTip.OffsetX != -5 || content.TryWrapTip.OffsetY != -75)
        {
            throw new InvalidDataException("Clean 5517 TryWrapTip offsets do not match the verified -5/-75 contract.");
        }

        byte[] encodedBackdrop;

        using (Stream stream = contentSource.OpenRequiredRead(BackdropPath, ContentLookupMode.PackageOnly))
        using (MemoryStream buffer = new())
        {
            stream.CopyTo(buffer);
            encodedBackdrop = buffer.ToArray();
        }

        string backdropHash = ConformanceHash.Sha256(encodedBackdrop);

        if (!string.Equals(backdropHash, "c91b686fa8e9758dd8c8825e401503825434a0ebaca3be30e8e0263b52985719", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Category-9 Dialog21 backdrop has unexpected SHA256 {backdropHash}.");
        }

        byte[] independentPixels = Dxt3ReferenceDecoder.Decode(encodedBackdrop, 256, 256);

        if (assets.Backdrop is null || !assets.Backdrop.Pixels.Span.SequenceEqual(independentPixels))
        {
            throw new InvalidDataException("Category-9 production backdrop decoding differs from independent DXT3 decoding.");
        }

        using OpenGLTexture2D referenceBackdrop = graphicsDevice.CreateTexture2D(256, 256, independentPixels);

        foreach (LogicalRenderSize resolution in s_resolutions)
        {
            RunResolution(graphicsDevice, content, assets, fontSettings, fontSize, codePage,
                referenceBackdrop, resolution, framebufferSize, colorFormat);
        }
    }

    private static void VerifyRetailRecords(MainHudMagicHintContent content)
    {
        VerifyRecord(content.Types, 1000, 2000, 0, 0);
        VerifyRecord(content.Types, 1415, 0, 6, 1);
        VerifyRecord(content.Types, 7001, 0, 0, 0);

        if (!content.Effects.GetEncoded(1000, 0, MagicEffectTextKind.Name).Span.SequenceEqual("Thunder"u8) ||
            !content.Effects.GetEncoded(1000, 0, MagicEffectTextKind.ExtendedDescription).Span.SequenceEqual("Magic attack"u8) ||
            !content.Effects.GetEncoded(1415, 0, MagicEffectTextKind.Name).Span.SequenceEqual("BattleDance"u8))
        {
            throw new InvalidDataException("Clean 5517 magic-effect records differ from the native category-9 evidence.");
        }

        if (!content.Subprofessions.TryGetTitle(6, out ReadOnlyMemory<byte> title) || !title.Span.SequenceEqual("Performer"u8))
        {
            throw new InvalidDataException("The native base-class subprofession title is unavailable.");
        }

        if (!content.KeyedStrings.GetEncoded("STR_NOT_COOL_DANCE_STATE").Span.SequenceEqual("Usable only as a Performer."u8))
        {
            throw new InvalidDataException("The native dance-state warning is unavailable.");
        }
    }

    private static void VerifyRecord(MagicTypeFile types, uint type, uint experience, uint requiredClass, uint requiredPhase)
    {
        if (!types.TryGet(type, 0, out MagicTypeRecord? record) ||
            record.RequiredExperience != experience ||
            record.RequiredSubprofessionClass != requiredClass ||
            record.RequiredSubprofessionPhase != requiredPhase)
        {
            throw new InvalidDataException($"Clean retail magic record {type}/0 does not match the verified native contract.");
        }
    }

    private static void RunResolution(OpenGLGraphicsDevice graphicsDevice, MainHudMagicHintContent content, MainHudStatusHintAssets assets, ClientFontSettingsConfiguration fontSettings, ClientFontSizeConfiguration fontSize, ClientCodePageConfiguration codePage, OpenGLTexture2D referenceBackdrop, LogicalRenderSize resolution, PixelSize framebufferSize, string colorFormat)
    {
        using MainHudStatusHintRenderer shared = new(graphicsDevice, assets, fontSettings, fontSize, codePage, resolution);
        using MainHudMagicHintRenderer production = new(graphicsDevice, shared, content, assets.Strings, fontSettings, codePage, resolution);
        using OpenGLTextContext referenceMagicText = new(graphicsDevice, fontSettings.GuiFontFaceName, FontHeight, codePage.EffectiveCodePage, fontSettings.AntialiasEnabled);
        using OpenGLTextContext referenceNormalText = new(graphicsDevice, fontSettings.GuiFontFaceName, fontSize.NormalFontHeightPixels, codePage.EffectiveCodePage, fontSettings.AntialiasEnabled);

        NativeTextRenderOptions white = CreateOptions(fontSettings, SpriteColor.White);
        NativeTextRenderOptions red = CreateOptions(fontSettings, s_red);

        _ = referenceMagicText.Layout("M"u8);

        foreach (LearnedCase testCase in s_learnedCases)
        {
            RunLearnedCase(graphicsDevice, production, referenceMagicText, referenceBackdrop, resolution, framebufferSize, colorFormat, white, red, testCase);
        }

        RunZeroCases(graphicsDevice, production, referenceNormalText, referenceBackdrop, resolution, framebufferSize, colorFormat, white);

        VerifySuppression(graphicsDevice, production, resolution, framebufferSize, colorFormat);
        VerifyLiveExperienceRefresh(graphicsDevice, production, resolution, framebufferSize);
        VerifyOutlineSensitivity(graphicsDevice, referenceBackdrop, resolution, framebufferSize);
    }

    private static void RunLearnedCase(OpenGLGraphicsDevice graphicsDevice, MainHudMagicHintRenderer production, OpenGLTextContext referenceText, OpenGLTexture2D backdrop, LogicalRenderSize resolution, PixelSize framebufferSize, string colorFormat, NativeTextRenderOptions white, NativeTextRenderOptions red, LearnedCase testCase)
    {
        MainHudStatusHintState state = CreateSelectedState(testCase.Type, testCase.SlotIndex, resolution);

        MainHudMagicHintRuntimeSnapshot snapshot = new([new MainHudLearnedMagic(testCase.Type, 0, testCase.Experience)], 130, testCase.ActiveClass, testCase.ActivePhase, testCase.PackedPhases, null, default);

        byte[] actual = Capture(graphicsDevice, resolution, framebufferSize, renderer => production.Draw(renderer, state, snapshot));

        byte[] expected = Capture(graphicsDevice, resolution, framebufferSize, renderer => DrawLearnedReference(renderer, referenceText, backdrop, resolution, state.MagicAnchorX, testCase.Groups, white, red));

        string label = $"Main HUD category-9 {testCase.Name} {resolution.Width}x{resolution.Height}";

        FramebufferVerifier.VerifyExact(label, expected, actual);

        Console.WriteLine($"{label}, {colorFormat}");
        Console.WriteLine($"Category-9 framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static void DrawLearnedReference(OpenGLRenderer renderer, OpenGLTextContext text, OpenGLTexture2D backdrop, LogicalRenderSize resolution, int anchorX, IReadOnlyList<ExpectedGroup> groups, NativeTextRenderOptions white, NativeTextRenderOptions red)
    {
        int fontWidth = text.Layout("M"u8).WidthPixels;
        int maximumWidth = 0;
        int lineCount = 0;

        foreach (ExpectedGroup group in groups)
        {
            IReadOnlyList<byte[]> lines = WrapReferenceGroup(text, group.Encoded, fontWidth);
            lineCount = checked(lineCount + lines.Count);

            foreach (byte[] line in lines)
            {
                maximumWidth = Math.Max(maximumWidth, text.Layout(line, recognizeDataIcons: true, dataIconWidthPixels: DataIconWidth).WidthPixels);
            }
        }

        int width = checked(maximumWidth + Padding * 2);
        int height = checked(lineCount * FontHeight + Padding * 2);
        int x = (long)anchorX + width + 2 <= resolution.Width ? anchorX : resolution.Width - width - 2;
        int y = Math.Max(0, resolution.Height - 43 - height);

        renderer.DrawSprite(backdrop, s_backdropSource, x, y, width, height);
        renderer.DrawLineRectangle(x, y, x + width, y + height, s_border);

        int textX = x + Padding;
        int textY = y + Padding;

        foreach (ExpectedGroup group in groups)
        {
            NativeTextRenderOptions options = group.Color == s_red ? red : white;

            if (group.Encoded.Length <= WrapCharacterLimit)
            {
                DrawReferenceLine(renderer, text, group.Encoded, options, textX, textY);
                textY += FontHeight;
                continue;
            }

            foreach (byte[] line in WrapReferenceGroup(text, group.Encoded, fontWidth))
            {
                DrawReferenceLine(renderer, text, line, options, textX, textY);
                textY += FontHeight;
            }
        }
    }

    private static IReadOnlyList<byte[]> WrapReferenceGroup(OpenGLTextContext text, ReadOnlySpan<byte> encoded, int fontWidth)
    {
        return NativeMagicEnglishWrapper.Wrap(encoded, WrapPixelLimit, fontWidth, span => text.Layout(span).WidthPixels);
    }

    private static void DrawReferenceLine(OpenGLRenderer renderer, OpenGLTextContext text, ReadOnlySpan<byte> encoded, NativeTextRenderOptions options, int x, int y)
    {
        if (encoded.IsEmpty)
        {
            return;
        }

        OpenGLTextLayout layout = text.Layout(encoded);
        text.Draw(renderer, layout, options, x, y);
    }

    private static void RunZeroCases(OpenGLGraphicsDevice graphicsDevice, MainHudMagicHintRenderer production, OpenGLTextContext referenceText, OpenGLTexture2D backdrop, LogicalRenderSize resolution, PixelSize framebufferSize, string colorFormat, NativeTextRenderOptions white)
    {
        ZeroCase[] cases =
        [
            new("revive", new MainHudZeroMagicState(0, true, false, false, false, 0, default), "Revive", ZeroKind.Revive),
            new("settle", new MainHudZeroMagicState(2, false, false, false, false, 0, default), "Settle", ZeroKind.Settle),
            new("restore", new MainHudZeroMagicState(0, false, true, false, false, 0, default), "Restore Appearance", ZeroKind.Restore),
            new("descend", new MainHudZeroMagicState(0, false, false, true, false, 0, default), "Descend", ZeroKind.Descend),
            new("tryout", new MainHudZeroMagicState(0, false, false, false, true, 20, "Hat"u8.ToArray()), "You are trying on Hat\nRemaining Time: 20 s", ZeroKind.Tryout),
        ];

        foreach (ZeroCase testCase in cases)
        {
            MainHudStatusHintState state = CreateSelectedState(0, 0, resolution);
            MainHudMagicHintRuntimeSnapshot snapshot = new([], 130, 0, 0, 0, null, testCase.State);

            byte[] actual = Capture(graphicsDevice, resolution, framebufferSize, renderer => production.Draw(renderer, state, snapshot));

            byte[] expected = Capture(graphicsDevice, resolution, framebufferSize, renderer => DrawZeroReference(renderer, referenceText, backdrop, resolution, testCase.Text, testCase.Kind, white));

            string label = $"Main HUD category-9 zero-ID {testCase.Name} {resolution.Width}x{resolution.Height}";

            FramebufferVerifier.VerifyExact(label, expected, actual);

            Console.WriteLine($"{label}, {colorFormat}");
            Console.WriteLine($"Category-9 zero-ID framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
        }
    }

    private static void DrawZeroReference(OpenGLRenderer renderer, OpenGLTextContext text, OpenGLTexture2D backdrop, LogicalRenderSize resolution, string label, ZeroKind kind, NativeTextRenderOptions options)
    {
        OpenGLTextLayout layout = text.Layout(Encoding.ASCII.GetBytes(label));
        int width = layout.WidthPixels;
        int height = layout.HeightPixels;
        int anchorX = 90;
        int storedAnchorY = resolution.Height - 1;
        int backgroundX;
        int textX;
        int y = storedAnchorY - 60;

        switch (kind)
        {
            case ZeroKind.Revive:
                backgroundX = anchorX + width <= resolution.Width ? anchorX - 3 : resolution.Width - 3 - width;
                textX = backgroundX;
                break;

            case ZeroKind.Settle:
            case ZeroKind.Descend:
                backgroundX = anchorX;
                textX = anchorX;
                break;

            case ZeroKind.Restore:
                backgroundX = anchorX - 4;
                textX = anchorX;
                break;

            case ZeroKind.Tryout:
                backgroundX = anchorX + width >= resolution.Width ? resolution.Width - width : anchorX;
                backgroundX -= 5;
                textX = backgroundX;
                y -= 75;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown native zero-ID category-9 branch.");
        }

        if (width > 0 && height > 0)
        {
            renderer.DrawSprite(backdrop, s_backdropSource, backgroundX, y, width, height);
        }

        text.Draw(renderer, layout, options, textX, y);
    }

    private static void VerifySuppression(OpenGLGraphicsDevice graphicsDevice, MainHudMagicHintRenderer production, LogicalRenderSize resolution, PixelSize framebufferSize, string colorFormat)
    {
        MainHudMagicHintRuntimeSnapshot learned = new([new MainHudLearnedMagic(1000, 0, 1000)], 130, 0, 0, 0, null, default);
        MainHudMagicHintRuntimeSnapshot missing = new([], 130, 0, 0, 0, null, default);
        byte[] expected = FramebufferVerifier.CreateOpaqueBlack(resolution.Width, resolution.Height);

        foreach (string name in new[] { "drag", "missing-learned-magic", "missing-runtime", "zero-without-flags" })
        {
            uint type = name == "zero-without-flags" ? 0u : 1000u;
            MainHudStatusHintState state = CreateSelectedState(type, 0, resolution);

            if (name == "drag")
            {
                state.SetSkillDragActive(true);
            }

            MainHudMagicHintRuntimeSnapshot? runtime = name switch
            {
                "missing-runtime" => null,
                "missing-learned-magic" or "zero-without-flags" => missing,
                _ => learned,
            };

            byte[] actual = Capture(graphicsDevice, resolution, framebufferSize, renderer => production.Draw(renderer, state, runtime));

            string label = $"Main HUD category-9 suppressed {name} {resolution.Width}x{resolution.Height}";

            FramebufferVerifier.VerifyExact(label, expected, actual);
            Console.WriteLine($"{label}, {colorFormat}");
        }
    }

    private static void VerifyLiveExperienceRefresh(OpenGLGraphicsDevice graphicsDevice, MainHudMagicHintRenderer production, LogicalRenderSize resolution, PixelSize framebufferSize)
    {
        MainHudStatusHintState state = CreateSelectedState(1000, 0, resolution);
        MainHudMagicHintRuntimeSnapshot first = new([new MainHudLearnedMagic(1000, 0, 1000)], 130, 0, 0, 0, null, default);
        MainHudMagicHintRuntimeSnapshot updated = new([new MainHudLearnedMagic(1000, 0, 3000)], 130, 0, 0, 0, null, default);

        byte[] before = Capture(graphicsDevice, resolution, framebufferSize, renderer => production.Draw(renderer, state, first));

        byte[] after = Capture(graphicsDevice, resolution, framebufferSize, renderer => production.Draw(renderer, state, updated));

        if (before.AsSpan().SequenceEqual(after))
        {
            throw new InvalidDataException("Native category-9 EXP text did not refresh for changed learned-magic data without mouse movement.");
        }
    }

    private static void VerifyOutlineSensitivity(OpenGLGraphicsDevice graphicsDevice, OpenGLTexture2D backdrop, LogicalRenderSize resolution, PixelSize framebufferSize)
    {
        const int x = 90;
        const int width = 200;
        const int height = 60;
        int y = resolution.Height - 43 - height;

        byte[] withoutOutline = Capture(graphicsDevice, resolution, framebufferSize,
            renderer => renderer.DrawSprite(backdrop, s_backdropSource, x, y, width, height));

        byte[] withOutline = Capture(graphicsDevice, resolution, framebufferSize,
            renderer =>
            {
                renderer.DrawSprite(backdrop, s_backdropSource, x, y, width, height);
                renderer.DrawLineRectangle(x, y, x + width, y + height, s_border);
            });

        if (withoutOutline.AsSpan().SequenceEqual(withOutline))
        {
            throw new InvalidDataException("Native category-9 line-list outline produced no visible framebuffer change.");
        }
    }

    private static MainHudStatusHintState CreateSelectedState(uint type, int slotIndex, LogicalRenderSize resolution)
    {
        MainHudStatusHintState state = new();
        state.SetMagicAnchor(90 + 40 * slotIndex, resolution.Height - 43);
        state.SelectMagic(type);
        return state;
    }

    private static byte[] Capture(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize resolution, PixelSize framebufferSize, Action<OpenGLRenderer> draw)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(resolution, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();
        draw(renderer);

        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static NativeTextRenderOptions CreateOptions(ClientFontSettingsConfiguration settings, SpriteColor color)
    {
        uint argb = settings.DefaultCornerColorArgb;
        SpriteColor corner = new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));

        return new NativeTextRenderOptions((NativeTextRenderStyle)settings.DefaultRenderTextStyle, color, corner, ClientFontSettingsConfiguration.DefaultCornerOffsetXPixels, ClientFontSettingsConfiguration.DefaultCornerOffsetYPixels, NativeTextVertexColors.Solid(color));
    }

    private readonly record struct ExpectedGroup(byte[] Encoded, SpriteColor Color)
    {
        public static ExpectedGroup White(string text) => new(Encoding.Latin1.GetBytes(text), SpriteColor.White);
        public static ExpectedGroup Red(string text) => new(Encoding.Latin1.GetBytes(text), s_red);
    }

    private readonly record struct LearnedCase(string Name, uint Type, uint Experience, int SlotIndex, int ActiveClass, int ActivePhase, long PackedPhases, ExpectedGroup[] Groups);

    private readonly record struct ZeroCase(string Name, MainHudZeroMagicState State, string Text, ZeroKind Kind);

    private enum ZeroKind
    {
        Revive,
        Settle,
        Restore,
        Descend,
        Tryout,
    }
}
