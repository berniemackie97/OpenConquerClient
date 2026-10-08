using System.Runtime.ExceptionServices;
using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Content;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.Conformance.Reference;
using OpenConquer.Rendering.Conformance.Support;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Conformance.Cases;

internal static class MainHudQuickbarConformance
{
    private static readonly LogicalRenderSize[] s_logicalRenderSizes = [new(800, 600), new(1024, 768)];

    private static readonly FrameFixture s_cover = F("data/interface/compose/CoverPic.dds", "18d15fd7460489c7230e3b172cf5f3ea994f1c5075054319f8e4df33526ddc1f", 64, 64, ContentLookupMode.LooseOnly);
    private static readonly FrameFixture s_action = F("data/main/Act6.dds", "a84ac9ea0696fd0ec23da79fdda3b422d1d43f23c0f8241c2799c5d33ac8385c", 64, 64, ContentLookupMode.PackageOnly);
    private static readonly FrameFixture s_dance = F("data/interface/Style01/Action/Dance2BtnNormal.dds", "3b278735d273b7c47e595899bba11ce938ff1ae49e35c5cf5540bec00081fdd1", 64, 64, ContentLookupMode.LooseOnly);
    private static readonly FrameFixture s_swapUse = F("data/main/UsemainbBtnNormal.dds", "6514395fd6998c34c17f094cd32e1f117d2acfb44272a49370d9b770f62f5d6c", 64, 64, ContentLookupMode.LooseOnly);
    private static readonly FrameFixture s_swapAlternate = F("data/main/SwapmainbBtnNormal.dds", "b343b08647c13a30c4cb67b9e86659041a515413a9fc367c32a4d90a3b7e4f8a", 64, 64, ContentLookupMode.LooseOnly);
    private static readonly FrameFixture s_magic = F("data/main/MagicSkillType1000.dds", "280c23dce938f4d0b125efe23f94b6f5587e01d9758b37067683df15cdc11949", 64, 64, ContentLookupMode.PackageOnly);
    private static readonly FrameFixture s_fire0 = F("Data/Pic/FireLight/01.dds", "b652fc8d38df5cbf34068b6fc3654831c676faf833b0faeb1da8739eee3c88b5", 64, 64, ContentLookupMode.PackageOnly);
    private static readonly FrameFixture s_fire1 = F("Data/Pic/FireLight/02.dds", "3946613a1b860374598cb5a42b2f40f34cd09264aa7f1b8061a4f947f4177a48", 64, 64, ContentLookupMode.PackageOnly);
    private static readonly FrameFixture s_itemDefault = F("data/ItemMinIcon/Default.dds", "5b1ee36b778c39fcc1a8a802169d74fc56a7c5e8a7179ff101406e4d2cf59e50", 64, 64, ContentLookupMode.PackageOnly);
    private static readonly FrameFixture s_quantity1 = F("data/main/Num1Pic.dds", "ce5ab89698f3c4a661904cd3face3eda5a718ded7c47cd3309b44c0724a6057b", 16, 16, ContentLookupMode.LooseOnly);
    private static readonly FrameFixture s_quantity2 = F("data/main/Num2Pic.dds", "2560724ada5915cac0f2f8e1c777a1fb5c9bb0064b7994483a0c5742c6569d36", 16, 16, ContentLookupMode.LooseOnly);
    private static readonly FrameFixture s_add3 = F("data/interface/Style01/Equip/Num/3.dds", "a21c5ed5f70e61c757ec8b0f263e0bbd9690f83294eda11cdaa48d7de0c10ba1", 16, 16, ContentLookupMode.LooseOnly);
    private static readonly FrameFixture s_addPlus = F("data/interface/Style01/Equip/Num/AddPic.dds", "41f9afdaca7aa53f379996cb98c135a727d11a6d5cea85398dda8aaa6bc45377", 16, 16, ContentLookupMode.LooseOnly);

    private static readonly FrameFixture[] s_frames =
    [
        s_cover, s_action, s_dance, s_swapUse, s_swapAlternate, s_magic, s_fire0, s_fire1,
        s_itemDefault, s_quantity1, s_quantity2, s_add3, s_addPlus,
    ];

    private static readonly RenderCase[] s_cases =
    [
        new("cover", RenderCaseKind.Cover, 0, false, 1000, 0),
        new("action-slot-2", RenderCaseKind.Action, 1, false, 1000, 0),
        new("magic", RenderCaseKind.Magic, 0, false, 1000, 0),
        new("dance", RenderCaseKind.Dance, 0, false, 1000, 0),
        new("swap-use", RenderCaseKind.Swap, 0, false, 1000, 0),
        new("swap-alternate", RenderCaseKind.Swap, 0, true, 1000, 0),
        new("item-quantity-plus", RenderCaseKind.Item, 0, false, 1000, 0),
        new("firelight-frame-0", RenderCaseKind.Glow0, 0, false, 1000, 0),
        new("firelight-frame-1", RenderCaseKind.Glow1, 0, false, 1000, 900),
    ];

    public static void Run(OpenGLGraphicsDevice graphicsDevice, PackagedClientContentSource contentSource, PixelSize framebufferSize, string colorFormat)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(colorFormat);

        Dictionary<string, byte[]> referencePixels = DecodeReferenceFrames(contentSource);
        MainHudQuickbarAssets assets = new(contentSource);

        using ReferenceTextures referenceTextures = new(graphicsDevice, referencePixels);

        foreach (LogicalRenderSize logicalRenderSize in s_logicalRenderSizes)
        {
            foreach (RenderCase testCase in s_cases)
            {
                RunRenderCase(graphicsDevice, assets, referenceTextures, logicalRenderSize, framebufferSize, colorFormat, testCase);
            }
        }
    }

    private static void RunRenderCase(OpenGLGraphicsDevice graphicsDevice, MainHudQuickbarAssets assets, ReferenceTextures referenceTextures, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, string colorFormat, RenderCase testCase)
    {
        MainHudQuickbarState state = BuildState(testCase);

        using MainHudQuickbarRenderer quickbarRenderer = new(
            graphicsDevice,
            assets,
            logicalRenderSize,
            itemIconKeyResolver: testCase.Kind == RenderCaseKind.Item ? FixedItemIconKeyResolver.Instance : null,
            useAlternateSwapIcon: () => testCase.AlternateSwap,
            readTickCount: () => testCase.Tick);

        byte[] actual = RenderProduction(graphicsDevice, quickbarRenderer, state, logicalRenderSize, framebufferSize);
        byte[] expected = RenderReference(graphicsDevice, referenceTextures, logicalRenderSize, framebufferSize, testCase);

        string label = $"Main HUD quickbar {testCase.Name} {logicalRenderSize.Width}x{logicalRenderSize.Height}";
        FramebufferVerifier.VerifyExact(label, expected, actual);

        Console.WriteLine($"{label}, {colorFormat}");
        Console.WriteLine($"Main HUD quickbar framebuffer SHA256: {ConformanceHash.Sha256(actual)}");
    }

    private static MainHudQuickbarState BuildState(RenderCase testCase)
    {
        MainHudQuickbarState state = new();
        int column = testCase.SlotIndex + 1;
        MainHudQuickbarSlotMetadata metadata = new(0, 0, 1, 0);

        switch (testCase.Kind)
        {
            case RenderCaseKind.Cover:
                state.Slots.SetCoverFlag(testCase.SlotIndex, 1);
                break;

            case RenderCaseKind.Action:
                state.Slots.Populate(column, 1, 1, 0, 3, (byte)MainHudQuickbarContentKind.Action, 0, metadata);
                break;

            case RenderCaseKind.Magic:
                state.Slots.Populate(column, 1, 1000, 0, 3, (byte)MainHudQuickbarContentKind.Magic, 0, metadata);
                break;

            case RenderCaseKind.Dance:
                state.Slots.Populate(column, 1, 2, 0, 3, (byte)MainHudQuickbarContentKind.Dance, 0, metadata);
                break;

            case RenderCaseKind.Swap:
                state.Slots.Populate(column, 1, 0, 0, 3, (byte)MainHudQuickbarContentKind.WeaponSwap, 0, metadata);
                break;

            case RenderCaseKind.Item:
                state.Slots.Populate(column, 1, 100, 0, 3, (byte)MainHudQuickbarContentKind.Item, 0, new MainHudQuickbarSlotMetadata(3, 0, 1, 0));
                state.Slots.SetAggregateQuantity(testCase.SlotIndex, 12);
                break;

            case RenderCaseKind.Glow0:
            case RenderCaseKind.Glow1:
                state.Slots.Populate(column, 1, 1, 0, 3, (byte)MainHudQuickbarContentKind.Action, 1, metadata);
                state.Slots.SetGlowStartTime(testCase.SlotIndex, testCase.GlowStart);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase, "Unknown quickbar conformance case.");
        }

        return state;
    }

    private static byte[] RenderProduction(OpenGLGraphicsDevice graphicsDevice, MainHudQuickbarRenderer quickbarRenderer, MainHudQuickbarState state, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        renderer.BeginFrame();
        quickbarRenderer.Draw(renderer, state);
        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static byte[] RenderReference(OpenGLGraphicsDevice graphicsDevice, ReferenceTextures textures, LogicalRenderSize logicalRenderSize, PixelSize framebufferSize, RenderCase testCase)
    {
        using OpenGLRenderer renderer = graphicsDevice.CreateRenderer(logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

        int x = 90 + 41 * testCase.SlotIndex;
        int y = logicalRenderSize.Height - 43;

        renderer.BeginFrame();

        switch (testCase.Kind)
        {
            case RenderCaseKind.Cover:
                renderer.DrawSprite(textures.Get(s_cover), x, y, 40, 40);
                break;

            case RenderCaseKind.Action:
                renderer.DrawSprite(textures.Get(s_action), x + 3, y + 5);
                break;

            case RenderCaseKind.Magic:
                renderer.DrawSprite(textures.Get(s_magic), new SpriteSourceRectangle(0, 0, 50, 50), x + 2, y + 4, 37, 37);
                break;

            case RenderCaseKind.Dance:
                renderer.DrawSprite(textures.Get(s_dance), x + 3, y + 5);
                break;

            case RenderCaseKind.Swap:
                renderer.DrawSprite(textures.Get(testCase.AlternateSwap ? s_swapAlternate : s_swapUse), x + 3, y + 5);
                break;

            case RenderCaseKind.Item:
                renderer.DrawSprite(textures.Get(s_itemDefault), x, y, 40, 40);
                renderer.DrawSprite(textures.Get(s_quantity1), x + 2, y + 2, 16, 16);
                renderer.DrawSprite(textures.Get(s_quantity2), x + 10, y + 2, 16, 16);
                renderer.DrawSprite(textures.Get(s_add3), x + 29, y);
                renderer.DrawSprite(textures.Get(s_addPlus), x + 20, y);
                break;

            case RenderCaseKind.Glow0:
                renderer.DrawSprite(textures.Get(s_fire0), x, y, 40, 40);
                renderer.DrawSprite(textures.Get(s_action), x + 3, y + 5);
                break;

            case RenderCaseKind.Glow1:
                renderer.DrawSprite(textures.Get(s_fire1), x, y, 40, 40);
                renderer.DrawSprite(textures.Get(s_action), x + 3, y + 5);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(testCase), testCase, "Unknown quickbar conformance case.");
        }

        byte[] framebuffer = renderer.ReadFrameTopLeftRgba();
        renderer.EndFrame();

        return framebuffer;
    }

    private static Dictionary<string, byte[]> DecodeReferenceFrames(PackagedClientContentSource contentSource)
    {
        Dictionary<string, byte[]> pixels = new(StringComparer.Ordinal);

        foreach (FrameFixture fixture in s_frames)
        {
            byte[] encoded = ReadVerifiedFrame(contentSource, fixture);
            pixels.Add(fixture.Path, Dxt3ReferenceDecoder.Decode(encoded, fixture.Width, fixture.Height));
        }

        return pixels;
    }

    private static byte[] ReadVerifiedFrame(PackagedClientContentSource contentSource, FrameFixture fixture)
    {
        using Stream stream = contentSource.OpenRequiredRead(fixture.Path, fixture.LookupMode);
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);

        byte[] bytes = buffer.ToArray();
        string actualSha256 = ConformanceHash.Sha256(bytes);

        if (!string.Equals(actualSha256, fixture.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Retail quickbar asset '{fixture.Path}' has SHA256 {actualSha256}; expected {fixture.Sha256}.");
        }

        return bytes;
    }

    private static FrameFixture F(string path, string sha256, int width, int height, ContentLookupMode lookupMode) => new(path, sha256, width, height, lookupMode);

    private enum RenderCaseKind
    {
        Cover,
        Action,
        Magic,
        Dance,
        Swap,
        Item,
        Glow0,
        Glow1,
    }

    private readonly record struct FrameFixture(string Path, string Sha256, int Width, int Height, ContentLookupMode LookupMode);
    private readonly record struct RenderCase(string Name, RenderCaseKind Kind, int SlotIndex, bool AlternateSwap, uint Tick, uint GlowStart);

    private sealed class FixedItemIconKeyResolver : IMainHudQuickbarItemIconKeyResolver
    {
        public static FixedItemIconKeyResolver Instance { get; } = new();

        public bool TryResolve(uint contentId, uint selector, out uint iconKey)
        {
            iconKey = 999999;
            return true;
        }
    }

    private sealed class ReferenceTextures : IDisposable
    {
        private readonly Dictionary<string, OpenGLTexture2D> _textures = new(StringComparer.Ordinal);
        private readonly List<OpenGLTexture2D> _ownedTextures = [];
        private bool _disposed;

        public ReferenceTextures(OpenGLGraphicsDevice graphicsDevice, IReadOnlyDictionary<string, byte[]> referencePixels)
        {
            try
            {
                foreach (FrameFixture fixture in s_frames)
                {
                    OpenGLTexture2D texture = graphicsDevice.CreateTexture2D(fixture.Width, fixture.Height, referencePixels[fixture.Path]);
                    _textures.Add(fixture.Path, texture);
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
                catch { }
            }
        }
    }
}
