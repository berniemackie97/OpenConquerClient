using System.Runtime.ExceptionServices;
using OpenConquer.Client.UI.Hud.ActionButtons;
using OpenConquer.Content;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Reference;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class MainHudActionButtonStripConformance
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string ExpectedControlAniSha256 = "a1db47baaeb2f75eea3b5216378f97d713c2afeaefe05ec02e6f584794532d27";
    private const int FrameWidth = 64;
    private const int FrameHeight = 32;

    private static readonly LogicalRenderSize[] s_logicalRenderSizes = [new(800, 600), new(1024, 768)];

    private static readonly ButtonFixture[] s_buttons =
    [
        B(MainHudActionButtonId.Button40, "Button40", 502, 553, 721,
            F("data/main/QueryBtn.dds", "53bf14f30d91a6771e2b3545444461b9597cd491960677b9b9178844c647f7ef", ContentLookupMode.PackageOnly),
            F("data/main/QueryBtnClick.dds", "b89ebf6d68b9f28350d10996a3d4d9af1204a02404498f2e4d035b165a4ca475", ContentLookupMode.PackageOnly)),

        B(MainHudActionButtonId.Button410, "Button410", 702, 578, 746,
            F("data/main/LevWordBtn.dds", "43b1f295d5a5c6ce459aee2ba9a9e1eaad0b99f95ff657b3d167e33cd6ec6426", ContentLookupMode.PackageOnly),
            F("data/main/LevWordBtnClick.dds", "43c7a289ba014b9a839eae158e02bf60c1f7e14f0cea713c6e87c08bb9ebe35d", ContentLookupMode.PackageOnly)),

        B(MainHudActionButtonId.Button42, "Button42", 552, 553, 721,
            F("data/main/GoodBtn.dds", "40f86b049069750334dba62bf186ba8436e5f94223dd436bdd4ac5960c37991f", ContentLookupMode.PackageOnly),
            F("data/main/GoodBtnClick.dds", "5b79b1afec4a75c2b49529fc43e4bab06d7c284bbd2f0f87e0a3d985a8e1888e", ContentLookupMode.PackageOnly)),

        B(MainHudActionButtonId.Button43, "Button43", 652, 578, 746,
            F("data/main/SetBtn.dds", "f6d62cbfa76a1e3050ff5a4fe1538449d2e78da4fe2fa5250f463da3e7129639", ContentLookupMode.PackageOnly),
            F("data/main/SetBtnClick.dds", "00bbafdab160f15cd0bea5ae180031fc8d672bf8e43ed0c3a4cfa52d5ad8fc75", ContentLookupMode.PackageOnly)),

        B(MainHudActionButtonId.Main3MissionBtn, "Main3_MissionBtn", 502, 578, 746,
            F("data/interface/Style01/Action/MissionBtnNormal.dds", "e22897c47610ab53c8a40fb8b8fc2d096a754deedf264f4694a47bb17dc75e6e", ContentLookupMode.LooseOnly),
            F("data/interface/Style01/Action/MissionBtnClick.dds", "8b5d4fea6621ea46711018bf33ad60b346340cb181108c388922283b75e6cc29", ContentLookupMode.LooseOnly),
            F("data/interface/Style01/Action/MissionBtnEmboss.dds", "152b11d5239b22b4992d85e8b4526c478f4b72c34bba277d52b8613764fe9eb0", ContentLookupMode.LooseOnly)),

        B(MainHudActionButtonId.Button45, "Button45", 552, 578, 746,
            F("data/main/ChatBtn.dds", "a4f63e5d6c3041d0b2b80031aa6cf36dc052ce65549ba9958b1f91927f932517", ContentLookupMode.PackageOnly),
            F("data/main/ChatBtnClick.dds", "0ee1172fb56963a3fdd77beddba91d32d73645dbfe8f516ebd4c0f2697de89ef", ContentLookupMode.PackageOnly)),

        B(MainHudActionButtonId.Button46, "Button46", 602, 578, 746,
            F("data/main/GroupBtn.dds", "5e2a9a8d60daf06c6a3f12b295611b0a12433b92481d4acecfe92d3fc5e438f3", ContentLookupMode.PackageOnly),
            F("data/main/GroupBtnClick.dds", "b9749ddbcf025e8777a176e70dce507124e7d43bfda9bb77eb9902c72d77c109", ContentLookupMode.PackageOnly)),

        B(MainHudActionButtonId.Button47, "Button47", 652, 553, 721,
            F("data/main/PkFree.dds", "daa715ebbd96119a8977ff82e0fde8abaac87633dac55f340231f6b33b994a08", ContentLookupMode.PackageOnly),
            F("data/main/PkFreeClick.dds", "651f6acd3db42f07278608752737d6b713d58e0a8e496d6b263c74d8b70e6e4b", ContentLookupMode.PackageOnly)),

        B(MainHudActionButtonId.Main3OrganiseBtn, "Main3_OrganiseBtn", 702, 553, 721,
            F("data/main/OrganiseBtnNormal.dds", "0b4f52919c0506bf86881e4201859459128df5736552189737751118aa66b360", ContentLookupMode.LooseOnly),
            F("data/main/OrganiseBtnClick.dds", "d6291032e2fc79aa6ca3a1715f32c2f3e73dc402b9fb927c9100dc7cae98b03a", ContentLookupMode.LooseOnly),
            F("data/main/OrganiseBtnUnClick.dds", "f9382f214d194c56e320933d69d00a637621b015b757401e29e260dbc818aaa3", ContentLookupMode.LooseOnly),
            F("data/main/OrganiseBtnEmboss.dds", "276d5d92563a4ae97825a0a2dafb7910788e2902c87ca7a96af2b2b7acdc2e16", ContentLookupMode.LooseOnly)),

        B(MainHudActionButtonId.Button41, "Button41", 602, 553, 721,
            F("data/main/SkillBtn.dds", "db5a32f59014e65403402003f403c76bcce7e934c8442f7f760d48279a1e0f4a", ContentLookupMode.PackageOnly),
            F("data/main/SkillBtnClick.dds", "5a2f7810b5a99b8a5c0a8ee01c8da2a7bfc167b511bf90f782cbe220f4cb361a", ContentLookupMode.PackageOnly),
            F("data/main/SkillBtnL.dds", "d04c0e8c75a3809ac7c27046a818fa2d0510cd8d414cc2a0e5590911f1a5d19a", ContentLookupMode.PackageOnly)),
    ];

    private static readonly Dictionary<MainHudPkButtonSkin, FrameFixture[]> s_pkSkins = new()
    {
        [MainHudPkButtonSkin.Button47] =
        [
            F("data/main/PkFree.dds", "daa715ebbd96119a8977ff82e0fde8abaac87633dac55f340231f6b33b994a08", ContentLookupMode.PackageOnly),
            F("data/main/PkFreeClick.dds", "651f6acd3db42f07278608752737d6b713d58e0a8e496d6b263c74d8b70e6e4b", ContentLookupMode.PackageOnly),
        ],
        [MainHudPkButtonSkin.Button49] =
        [
            F("data/main/PkSafe.dds", "b4a08b57138750cb2df7f654c039994dde3e6889597303a206dac6ca0d388bbd", ContentLookupMode.PackageOnly),
            F("data/main/PkSafeClick.dds", "3c0bbf942e62d6e0aef98a6ef3a1284e39eaff309ebcc3abea7479d4dd053969", ContentLookupMode.PackageOnly),
        ],
        [MainHudPkButtonSkin.Button48] =
        [
            F("data/main/PkGroup.dds", "779db2abffe0803ddc4e0ad9373c7de59ef565643a2daf89fff4f92d949d3a8e", ContentLookupMode.PackageOnly),
            F("data/main/PkGroupClick.dds", "f1a71c88d3cda9dd74c141467b269bf3af1e20d7ff246b91daaae8d160a310c0", ContentLookupMode.PackageOnly),
        ],
        [MainHudPkButtonSkin.Button412] =
        [
            F("data/main/PkArre.dds", "b76d5cb6f912557c53f5400b4c2521c5299a05adcd5ddb1e3d4356aa41b4b37f", ContentLookupMode.PackageOnly),
            F("data/main/PkArreClick.dds", "374842c513a49a198ee9e67f7bb1543ad541b19eb8e249ed0bf769e9385c5224", ContentLookupMode.PackageOnly),
        ],
    };

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PackagedClientContentSource contentSource, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        _ = ReadVerifiedAsset(contentSource, ControlAniPath, ExpectedControlAniSha256, ContentLookupMode.LooseOnly);

        Dictionary<string, byte[]> referencePixels = DecodeReferenceFrames(contentSource);
        MainHudActionButtonAssets assets = MainHudActionButtonAssets.Load(contentSource);

        VerifyProductionDecode(assets, referencePixels);

        using ReferenceTextures referenceTextures = new(graphicsDevice, referencePixels);

        foreach (LogicalRenderSize logicalRenderSize in s_logicalRenderSizes)
        {
            using MainHudActionButtonStripRenderer stripRenderer = new(graphicsDevice, assets, logicalRenderSize);

            foreach (RenderCase testCase in BuildRenderCases())
            {
                RunRenderCase(graphicsDevice, stripRenderer, referenceTextures, logicalRenderSize, framebufferSize, colorFormat, testCase);
            }
        }
    }

    private static void RunRenderCase(OpenGLGraphicsDevice graphicsDevice, MainHudActionButtonStripRenderer stripRenderer, ReferenceTextures referenceTextures, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, string colorFormat, RenderCase testCase)
    {
        MainHudActionButtonStripState state = new();

        if (testCase.PkSkin is { } skin && skin != MainHudPkButtonSkin.Button47)
        {
            state.ApplyPkMode(GetPkMode(skin));
        }

        state.GetButton(testCase.ButtonId).SetCurrentFrame(testCase.LogicalFrame);

        byte[] actual = RenderProduction(graphicsDevice, stripRenderer, state, logicalRenderSize, framebufferSize);
        byte[] expected = RenderReference(graphicsDevice, referenceTextures, logicalRenderSize, framebufferSize, testCase);

        string label = $"Main HUD action strip {testCase.Name} {logicalRenderSize.Width}x{logicalRenderSize.Height}";
        FramebufferVerifier.VerifyExact(label, expected, actual);

        Console.WriteLine($"{label}, {colorFormat}");
        Console.WriteLine($"Main HUD action strip framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static byte[] RenderProduction(OpenGLGraphicsDevice graphicsDevice, MainHudActionButtonStripRenderer stripRenderer, MainHudActionButtonStripState state, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();
        stripRenderer.Draw(renderer, state);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static byte[] RenderReference(OpenGLGraphicsDevice graphicsDevice, ReferenceTextures referenceTextures, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, RenderCase testCase)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();

        foreach (ButtonFixture fixture in s_buttons)
        {
            int logicalFrame = fixture.Id == testCase.ButtonId ? testCase.LogicalFrame : 0;
            FrameFixture[] frames = fixture.Id == MainHudActionButtonId.Button47
                ? s_pkSkins[testCase.PkSkin ?? MainHudPkButtonSkin.Button47]
                : fixture.Frames;

            OpenGLTexture2D texture = referenceTextures.Get(frames[logicalFrame % frames.Length]);
            int y = logicalRenderSize.Height == 600 ? fixture.Y800 : fixture.Y1024;
            renderer.DrawSprite(texture, fixture.X, y);
        }

        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static Dictionary<string, byte[]> DecodeReferenceFrames(PackagedClientContentSource contentSource)
    {
        Dictionary<string, FrameFixture> fixtures = new(StringComparer.Ordinal);

        foreach (ButtonFixture button in s_buttons)
        {
            foreach (FrameFixture frame in button.Frames)
            {
                fixtures.TryAdd(frame.Path, frame);
            }
        }

        foreach (FrameFixture[] frames in s_pkSkins.Values)
        {
            foreach (FrameFixture frame in frames)
            {
                fixtures.TryAdd(frame.Path, frame);
            }
        }

        Dictionary<string, byte[]> pixels = new(StringComparer.Ordinal);

        foreach (FrameFixture fixture in fixtures.Values)
        {
            byte[] encoded = ReadVerifiedAsset(contentSource, fixture.Path, fixture.Sha256, fixture.LookupMode);
            pixels.Add(fixture.Path, Dxt3ReferenceDecoder.Decode(encoded, FrameWidth, FrameHeight));
        }

        return pixels;
    }

    private static void VerifyProductionDecode(MainHudActionButtonAssets assets, Dictionary<string, byte[]> referencePixels)
    {
        foreach (ButtonFixture button in s_buttons)
        {
            if (!assets.IsAvailable(button.Id))
            {
                throw new InvalidDataException($"Verified retail [{button.SectionName}] assets were unavailable through the production action-button loader.");
            }

            for (int logicalFrame = 0; logicalFrame <= MainHudActionButtonState.HoverFrame; logicalFrame++)
            {
                FrameFixture expected = button.Frames[logicalFrame % button.Frames.Length];
                VerifyPixels($"{button.SectionName} logical frame {logicalFrame}", assets.GetFrame(button.Id, logicalFrame)?.Pixels, referencePixels[expected.Path]);
            }
        }

        foreach ((MainHudPkButtonSkin skin, FrameFixture[] frames) in s_pkSkins)
        {
            if (!assets.IsPkSkinAvailable(skin))
            {
                throw new InvalidDataException($"Verified retail PK skin [{MainHudPkButtonSkins.GetAniSectionName(skin)}] was unavailable through the production action-button loader.");
            }

            for (int logicalFrame = 0; logicalFrame <= MainHudActionButtonState.HoverFrame; logicalFrame++)
            {
                FrameFixture expected = frames[logicalFrame % frames.Length];
                VerifyPixels($"{MainHudPkButtonSkins.GetAniSectionName(skin)} logical frame {logicalFrame}", assets.GetPkFrame(skin, logicalFrame)?.Pixels, referencePixels[expected.Path]);
            }
        }
    }

    private static RenderCase[] BuildRenderCases()
    {
        List<RenderCase> cases = [];

        foreach (ButtonFixture button in s_buttons)
        {
            for (int frame = 0; frame <= MainHudActionButtonState.HoverFrame; frame++)
            {
                MainHudPkButtonSkin? pkSkin = button.Id == MainHudActionButtonId.Button47 ? MainHudPkButtonSkin.Button47 : null;
                cases.Add(new RenderCase($"{button.SectionName} logical-frame-{frame}", button.Id, frame, pkSkin));
            }
        }

        foreach (MainHudPkButtonSkin skin in new[] { MainHudPkButtonSkin.Button49, MainHudPkButtonSkin.Button48, MainHudPkButtonSkin.Button412 })
        {
            for (int frame = 0; frame <= MainHudActionButtonState.HoverFrame; frame++)
            {
                cases.Add(new RenderCase($"{MainHudPkButtonSkins.GetAniSectionName(skin)} logical-frame-{frame}", MainHudActionButtonId.Button47, frame, skin));
            }
        }

        return cases.ToArray();
    }

    private static int GetPkMode(MainHudPkButtonSkin skin) => skin switch
    {
        MainHudPkButtonSkin.Button49 => 1,
        MainHudPkButtonSkin.Button48 => 2,
        MainHudPkButtonSkin.Button412 => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(skin), skin, "Only alternate PK skins require an applied PK mode."),
    };

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
            throw new InvalidDataException($"Retail HUD action-strip asset '{contentPath}' unexpectedly resolves using {unexpectedMode}; verified 5517 requires {lookupMode} lookup.");
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
            throw new InvalidDataException($"Retail HUD action-strip asset '{contentPath}' has SHA256 {actualSha256}; expected {expectedSha256}.");
        }
    }

    private static void VerifyPixels(string assetName, ReadOnlyMemory<byte>? productionPixels, ReadOnlySpan<byte> referencePixels)
    {
        if (!productionPixels.HasValue)
        {
            throw new InvalidDataException($"{assetName} was unavailable through the production HUD action-button asset loader.");
        }

        ReadOnlySpan<byte> pixels = productionPixels.Value.Span;

        if (!pixels.SequenceEqual(referencePixels))
        {
            throw new InvalidDataException($"{assetName} production RGBA SHA256 {ConformanceHash.Sha256(pixels)} does not match independent DXT3 reference SHA256 {ConformanceHash.Sha256(referencePixels)}.");
        }
    }

    private static ButtonFixture B(MainHudActionButtonId id, string sectionName, int x, int y800, int y1024, params FrameFixture[] frames) => new(id, sectionName, x, y800, y1024, frames);

    private static FrameFixture F(string path, string sha256, ContentLookupMode lookupMode) => new(path, sha256, lookupMode);

    private readonly record struct ButtonFixture(MainHudActionButtonId Id, string SectionName, int X, int Y800, int Y1024, FrameFixture[] Frames);

    private readonly record struct FrameFixture(string Path, string Sha256, ContentLookupMode LookupMode);

    private readonly record struct RenderCase(string Name, MainHudActionButtonId ButtonId, int LogicalFrame, MainHudPkButtonSkin? PkSkin);

    private sealed class ReferenceTextures : IDisposable
    {
        private readonly Dictionary<string, OpenGLTexture2D> _textures = new(StringComparer.Ordinal);
        private readonly List<OpenGLTexture2D> _ownedTextures = [];
        private bool _disposed;

        public ReferenceTextures(OpenGLGraphicsDevice graphicsDevice, IReadOnlyDictionary<string, byte[]> referencePixels)
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
                catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }
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
