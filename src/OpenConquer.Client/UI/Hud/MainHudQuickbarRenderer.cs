using System.Globalization;
using System.Runtime.ExceptionServices;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Hud;

internal interface IMainHudQuickbarItemIconKeyResolver
{
    bool TryResolve(uint contentId, uint selector, out uint iconKey);
}

internal interface IMainHudQuickbarCooldownSource
{
    uint GetRemainingMilliseconds(uint skillType, uint now);
}

internal interface IMainHudQuickbarCooldownTextRenderer
{
    void Draw(OpenGLRenderer renderer, string text, int x, int y);
}

internal interface IMainHudQuickbarGlowSectionResolver
{
    string? Resolve(uint contentId);
}

internal sealed class MainHudQuickbarRenderer : IDisposable
{
    private const int QuantitySize = 16;
    private const int QuantityStride = 8;

    private readonly MainHudQuickbarAssets _assets;
    private readonly MainHudQuickbarLayout _layout;
    private readonly OpenGLGraphicsDevice _graphicsDevice;
    private readonly IMainHudQuickbarItemIconKeyResolver? _itemIconKeyResolver;
    private readonly IMainHudQuickbarCooldownSource? _cooldownSource;
    private readonly IMainHudQuickbarCooldownTextRenderer? _cooldownTextRenderer;
    private readonly IMainHudQuickbarGlowSectionResolver? _glowSectionResolver;
    private readonly Func<bool> _useAlternateSwapIcon;
    private readonly Func<uint> _readTickCount;
    private readonly Dictionary<string, OpenGLTexture2D?> _fixedTextures = new(StringComparer.Ordinal);
    private readonly List<OpenGLTexture2D> _fixedOwnedTextures = [];
    private readonly SlotTextureCache[] _slotTextures = CreateSlotTextureCaches();
    private bool _disposed;

    public MainHudQuickbarRenderer(OpenGLGraphicsDevice graphicsDevice, MainHudQuickbarAssets assets, LogicalRenderSize logicalRenderSize, IMainHudQuickbarItemIconKeyResolver? itemIconKeyResolver = null, IMainHudQuickbarCooldownSource? cooldownSource = null, IMainHudQuickbarCooldownTextRenderer? cooldownTextRenderer = null, IMainHudQuickbarGlowSectionResolver? glowSectionResolver = null, Func<bool>? useAlternateSwapIcon = null, Func<uint>? readTickCount = null)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(assets);

        if ((cooldownSource is null) != (cooldownTextRenderer is null))
        {
            throw new ArgumentException("Cooldown state and cooldown text rendering must be supplied together.");
        }

        _graphicsDevice = graphicsDevice;
        _assets = assets;
        _layout = MainHudQuickbarLayout.Create(logicalRenderSize);
        _itemIconKeyResolver = itemIconKeyResolver;
        _cooldownSource = cooldownSource;
        _cooldownTextRenderer = cooldownTextRenderer;
        _glowSectionResolver = glowSectionResolver;
        _useAlternateSwapIcon = useAlternateSwapIcon ?? (static () => false);
        _readTickCount = readTickCount ?? (static () => unchecked((uint)Environment.TickCount64));
    }

    public void Draw(OpenGLRenderer renderer, MainHudQuickbarState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(state);

        for (int slotIndex = 0; slotIndex < MainHudQuickbarDefinition.SlotCount; slotIndex++)
        {
            MainHudQuickbarSlotBounds bounds = _layout.GetSlotBounds(slotIndex);
            MainHudQuickbarSlotSnapshot slot = state.Slots.GetSlot(slotIndex);
            uint cooldown = 0;
            bool itemIconDrawn = false;

            if (slot.IsOccupied)
            {
                uint now = _readTickCount();

                DrawGlow(renderer, state, slotIndex, bounds, slot, now);
                itemIconDrawn = DrawContent(renderer, slotIndex, bounds, slot, now, out cooldown);

                if (itemIconDrawn)
                {
                    DrawQuantity(renderer, bounds, slot.AggregateQuantity);
                    DrawAddLevel(renderer, bounds, slot);
                }
            }
            else
            {
                _slotTextures[slotIndex].ClearDynamic();
            }

            bool covered =
                slot.CoverFlag != 0 ||
                slot.IsOccupied && slot.ContentKind == (byte)MainHudQuickbarContentKind.Item && unchecked((int)slot.AggregateQuantity) <= 0 ||
                slot.IsOccupied && (slot.ContentKind is (byte)MainHudQuickbarContentKind.Magic or (byte)MainHudQuickbarContentKind.XpMagic) && cooldown != 0;

            if (covered)
            {
                DrawCover(renderer, bounds);
            }

            if (cooldown != 0)
            {
                uint seconds = unchecked((cooldown - 1) / 1000 + 1);
                _cooldownTextRenderer!.Draw(renderer, seconds.ToString(CultureInfo.InvariantCulture), bounds.X, bounds.Y);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        for (int index = _slotTextures.Length - 1; index >= 0; index--)
        {
            try
            {
                _slotTextures[index].Dispose();
            }
            catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }
        }

        for (int index = _fixedOwnedTextures.Count - 1; index >= 0; index--)
        {
            try
            {
                _fixedOwnedTextures[index].Dispose();
            }
            catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }
        }

        _fixedTextures.Clear();
        _fixedOwnedTextures.Clear();
        _disposed = true;
        firstFailure?.Throw();
    }

    private bool DrawContent(OpenGLRenderer renderer, int slotIndex, MainHudQuickbarSlotBounds bounds, MainHudQuickbarSlotSnapshot slot, uint now, out uint cooldown)
    {
        cooldown = 0;
        SlotTextureCache cache = _slotTextures[slotIndex];

        switch (slot.ContentKind)
        {
            case (byte)MainHudQuickbarContentKind.Item:
                if (_itemIconKeyResolver is null || !_itemIconKeyResolver.TryResolve(slot.ContentId, slot.Selector, out uint iconKey))
                {
                    cache.ClearContent();
                    return false;
                }

                OpenGLTexture2D? item = cache.GetContent(
                    $"item:{iconKey}",
                    () => CreateTexture($"item:{iconKey}", () => _assets.GetItemFrame(iconKey), exactWidth: 64, exactHeight: 64));

                if (item is null)
                {
                    return false;
                }

                renderer.DrawSprite(item, bounds.X, bounds.Y, MainHudQuickbarDefinition.CellWidth, MainHudQuickbarDefinition.CellHeight);
                return true;

            case (byte)MainHudQuickbarContentKind.Action:
                DrawNaturalDynamic(renderer, cache, $"control:ButtonA{slot.ContentId}", () => _assets.GetControlFrame0($"ButtonA{slot.ContentId}"), bounds.X + 3, bounds.Y + 5, 64, 64);
                return false;

            case (byte)MainHudQuickbarContentKind.Magic:
            case (byte)MainHudQuickbarContentKind.XpMagic:
                string section = slot.ContentKind == (byte)MainHudQuickbarContentKind.Magic ? $"MagicSkillType{slot.ContentId}" : $"XpSkillType{slot.ContentId}";
                OpenGLTexture2D? magic = cache.GetContent(
                    $"magic:{section}",
                    () => CreateTexture($"magic:{section}", () => _assets.GetMagicFrame0(section), minimumWidth: 50, minimumHeight: 50));

                if (magic is not null)
                {
                    renderer.DrawSprite(magic, new SpriteSourceRectangle(0, 0, 50, 50), bounds.X + 2, bounds.Y + 4, 37, 37);
                }

                cooldown = _cooldownSource?.GetRemainingMilliseconds(slot.ContentId, now) ?? 0;
                return false;

            case (byte)MainHudQuickbarContentKind.Dance:
                DrawNaturalDynamic(renderer, cache, $"control:Action_Dance{slot.ContentId}Btn", () => _assets.GetControlFrame0($"Action_Dance{slot.ContentId}Btn"), bounds.X + 3, bounds.Y + 5, 64, 64);
                return false;

            case (byte)MainHudQuickbarContentKind.WeaponSwap:
                string swapSection = _useAlternateSwapIcon() ? "Swapuse_SwapmainbBtn" : "Swapuse_UsemainbBtn";
                DrawNaturalDynamic(renderer, cache, $"control:{swapSection}", () => _assets.GetControlFrame0(swapSection), bounds.X + 3, bounds.Y + 5, 64, 64);
                return false;

            default:
                cache.ClearContent();
                return false;
        }
    }

    private void DrawGlow(OpenGLRenderer renderer, MainHudQuickbarState state, int slotIndex, MainHudQuickbarSlotBounds bounds, MainHudQuickbarSlotSnapshot slot, uint now)
    {
        SlotTextureCache cache = _slotTextures[slotIndex];

        if (slot.GlowKind == 0)
        {
            cache.ClearGlow();
            return;
        }

        string? section = slot.GlowKind switch
        {
            1 => "FireLight",
            2 => "RedLight",
            3 => "BlueLight",
            4 => "RoyalBlueLight",
            5 => _glowSectionResolver?.Resolve(slot.ContentId),
            6 => "YellowLight",
            _ => null,
        };

        if (string.IsNullOrEmpty(section))
        {
            cache.ClearGlow();
            return;
        }

        OpenGLTexture2D[]? textures = cache.GetGlow(section, () => CreateGlowTextures(section));

        if (textures is null || textures.Length == 0)
        {
            return;
        }

        uint start = slot.GlowStartTime;
        uint frameIndex;

        if (start == 0)
        {
            state.Slots.SetGlowStartTime(slotIndex, now);
            frameIndex = 0;
        }
        else
        {
            frameIndex = unchecked(now - start) / 100 % (uint)textures.Length;
        }

        renderer.DrawSprite(textures[frameIndex], bounds.X, bounds.Y, MainHudQuickbarDefinition.CellWidth, MainHudQuickbarDefinition.CellHeight);
    }

    private void DrawQuantity(OpenGLRenderer renderer, MainHudQuickbarSlotBounds bounds, uint rawQuantity)
    {
        string text = unchecked((int)rawQuantity).ToString(CultureInfo.InvariantCulture);
        int x = bounds.X + 2;

        foreach (char character in text)
        {
            int digit = character - '0';
            string section = $"Main3_Num{digit}Pic";
            OpenGLTexture2D? texture = GetFixedTexture($"control:{section}", () => _assets.GetControlFrame0(section), exactWidth: 16, exactHeight: 16);

            if (texture is null)
            {
                break;
            }

            renderer.DrawSprite(texture, x, bounds.Y + 2, QuantitySize, QuantitySize);
            x += QuantityStride;
        }
    }

    private void DrawAddLevel(OpenGLRenderer renderer, MainHudQuickbarSlotBounds bounds, MainHudQuickbarSlotSnapshot slot)
    {
        int addLevel = unchecked((int)slot.Metadata.AddLevel);

        if (addLevel <= 0 || slot.ContentId / 10000 % 100 == 73)
        {
            return;
        }

        string text = addLevel.ToString(CultureInfo.InvariantCulture);
        int x = bounds.X + MainHudQuickbarDefinition.CellWidth - 11;

        for (int index = text.Length - 1; index >= 0; index--)
        {
            string section = $"Equip_Num{text[index]}";
            OpenGLTexture2D? digit = GetFixedTexture($"control:{section}", () => _assets.GetControlFrame0(section), exactWidth: 16, exactHeight: 16);

            if (digit is null)
            {
                break;
            }

            renderer.DrawSprite(digit, x, bounds.Y);
            x -= 9;
        }

        OpenGLTexture2D? plus = GetFixedTexture("control:Equip_AddPic", () => _assets.GetControlFrame0("Equip_AddPic"), exactWidth: 16, exactHeight: 16);

        if (plus is not null)
        {
            renderer.DrawSprite(plus, x, bounds.Y);
        }
    }

    private void DrawCover(OpenGLRenderer renderer, MainHudQuickbarSlotBounds bounds)
    {
        OpenGLTexture2D? texture = GetFixedTexture("control:Compose_CoverPic", _assets.GetCoverFrame, exactWidth: 64, exactHeight: 64);

        if (texture is not null)
        {
            renderer.DrawSprite(texture, bounds.X, bounds.Y, MainHudQuickbarDefinition.CellWidth, MainHudQuickbarDefinition.CellHeight);
        }
    }

    private void DrawNaturalDynamic(OpenGLRenderer renderer, SlotTextureCache cache, string key, Func<RgbaImage?> load, int x, int y, int width, int height)
    {
        OpenGLTexture2D? texture = cache.GetContent(key, () => CreateTexture(key, load, exactWidth: width, exactHeight: height));

        if (texture is not null)
        {
            renderer.DrawSprite(texture, x, y);
        }
    }

    private OpenGLTexture2D[]? CreateGlowTextures(string section)
    {
        AniFrameSet? frames = _assets.GetEffectFrames(section);

        if (frames is null || frames.FrameCount == 0)
        {
            return null;
        }

        OpenGLTexture2D[] textures = new OpenGLTexture2D[frames.FrameCount];
        int createdCount = 0;

        try
        {
            for (int frameIndex = 0; frameIndex < frames.FrameCount; frameIndex++)
            {
                string key = $"effect:{section}:{frameIndex}";
                RgbaImage image = frames.GetFrame((uint)frameIndex);

                ValidateImage(key, image, exactWidth: 64, exactHeight: 64, minimumWidth: null, minimumHeight: null);

                textures[frameIndex] = _graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
                createdCount++;
            }

            return textures;
        }
        catch
        {
            for (int index = createdCount - 1; index >= 0; index--)
            {
                try
                {
                    textures[index].Dispose();
                }
                catch
                {
                    // Preserve the texture-creation failure that initiated cleanup.
                }
            }

            throw;
        }
    }

    private OpenGLTexture2D? GetFixedTexture(string key, Func<RgbaImage?> load, int? exactWidth = null, int? exactHeight = null, int? minimumWidth = null, int? minimumHeight = null)
    {
        if (_fixedTextures.TryGetValue(key, out OpenGLTexture2D? cached))
        {
            return cached;
        }

        OpenGLTexture2D? texture = CreateTexture(key, load, exactWidth, exactHeight, minimumWidth, minimumHeight);

        _fixedTextures.Add(key, texture);

        if (texture is not null)
        {
            _fixedOwnedTextures.Add(texture);
        }

        return texture;
    }

    private OpenGLTexture2D? CreateTexture(string key, Func<RgbaImage?> load, int? exactWidth = null, int? exactHeight = null, int? minimumWidth = null, int? minimumHeight = null)
    {
        RgbaImage? image = load();

        if (image is null)
        {
            return null;
        }

        ValidateImage(key, image, exactWidth, exactHeight, minimumWidth, minimumHeight);
        return _graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
    }

    private static void ValidateImage(string key, RgbaImage image, int? exactWidth, int? exactHeight, int? minimumWidth, int? minimumHeight)
    {
        if ((exactWidth is not null && image.Width != exactWidth) || (exactHeight is not null && image.Height != exactHeight))
        {
            throw new InvalidDataException($"Quickbar asset '{key}' decoded as {image.Width}x{image.Height}; expected {exactWidth}x{exactHeight}.");
        }

        if ((minimumWidth is not null && image.Width < minimumWidth) || (minimumHeight is not null && image.Height < minimumHeight))
        {
            throw new InvalidDataException($"Quickbar asset '{key}' decoded as {image.Width}x{image.Height}; expected at least {minimumWidth}x{minimumHeight}.");
        }
    }

    private static SlotTextureCache[] CreateSlotTextureCaches()
    {
        SlotTextureCache[] caches = new SlotTextureCache[MainHudQuickbarDefinition.SlotCount];

        for (int index = 0; index < caches.Length; index++)
        {
            caches[index] = new SlotTextureCache();
        }

        return caches;
    }

    private sealed class SlotTextureCache : IDisposable
    {
        private string? _contentKey;
        private OpenGLTexture2D? _contentTexture;
        private string? _glowSection;
        private OpenGLTexture2D[]? _glowTextures;
        private bool _contentResolved;
        private bool _glowResolved;
        private bool _disposed;

        public OpenGLTexture2D? GetContent(string key, Func<OpenGLTexture2D?> load)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrEmpty(key);
            ArgumentNullException.ThrowIfNull(load);

            if (_contentResolved && string.Equals(_contentKey, key, StringComparison.Ordinal))
            {
                return _contentTexture;
            }

            ClearContent();

            OpenGLTexture2D? texture = load();

            _contentKey = key;
            _contentTexture = texture;
            _contentResolved = true;

            return texture;
        }

        public OpenGLTexture2D[]? GetGlow(string section, Func<OpenGLTexture2D[]?> load)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrEmpty(section);
            ArgumentNullException.ThrowIfNull(load);

            if (_glowResolved && string.Equals(_glowSection, section, StringComparison.Ordinal))
            {
                return _glowTextures;
            }

            ClearGlow();

            OpenGLTexture2D[]? textures = load();

            _glowSection = section;
            _glowTextures = textures;
            _glowResolved = true;

            return textures;
        }

        public void ClearContent()
        {
            OpenGLTexture2D? texture = _contentTexture;

            _contentKey = null;
            _contentTexture = null;
            _contentResolved = false;

            texture?.Dispose();
        }

        public void ClearGlow()
        {
            OpenGLTexture2D[]? textures = _glowTextures;

            _glowSection = null;
            _glowTextures = null;
            _glowResolved = false;

            if (textures is null)
            {
                return;
            }

            ExceptionDispatchInfo? firstFailure = null;

            for (int index = textures.Length - 1; index >= 0; index--)
            {
                try
                {
                    textures[index].Dispose();
                }
                catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }
            }

            firstFailure?.Throw();
        }

        public void ClearDynamic()
        {
            ExceptionDispatchInfo? firstFailure = null;

            try
            {
                ClearContent();
            }
            catch (Exception exception) { firstFailure = ExceptionDispatchInfo.Capture(exception); }

            try
            {
                ClearGlow();
            }
            catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }

            firstFailure?.Throw();
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
                ClearDynamic();
            }
            catch (Exception exception) { firstFailure = ExceptionDispatchInfo.Capture(exception); }

            _disposed = true;
            firstFailure?.Throw();
        }
    }
}
