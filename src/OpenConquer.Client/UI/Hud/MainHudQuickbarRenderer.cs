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
    private readonly Dictionary<string, OpenGLTexture2D> _textures = new(StringComparer.Ordinal);
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
        _useAlternateSwapIcon = useAlternateSwapIcon ?? static () => false;
        _readTickCount = readTickCount ?? static () => unchecked((uint)Environment.TickCount64);
    }

    public void Draw(OpenGLRenderer renderer, MainHudQuickbarState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(state);

        uint now = _readTickCount();

        for (int slotIndex = 0; slotIndex < MainHudQuickbarDefinition.SlotCount; slotIndex++)
        {
            MainHudQuickbarSlotBounds bounds = _layout.GetSlotBounds(slotIndex);
            MainHudQuickbarSlotSnapshot slot = state.Slots.GetSlot(slotIndex);
            uint cooldown = 0;
            bool itemIconDrawn = false;

            if (slot.IsOccupied)
            {
                DrawGlow(renderer, state, slotIndex, bounds, slot, now);
                itemIconDrawn = DrawContent(renderer, bounds, slot, now, out cooldown);

                if (itemIconDrawn)
                {
                    DrawQuantity(renderer, bounds, slot.AggregateQuantity);
                    DrawAddLevel(renderer, bounds, slot);
                }
            }

            bool covered = slot.CoverFlag != 0 ||
                slot.IsOccupied && slot.ContentKind == (byte)MainHudQuickbarContentKind.Item && slot.AggregateQuantity == 0 ||
                slot.IsOccupied && slot.ContentKind is (byte)MainHudQuickbarContentKind.Magic or (byte)MainHudQuickbarContentKind.XpMagic && cooldown != 0;

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

        foreach (OpenGLTexture2D texture in _textures.Values.Reverse())
        {
            try
            {
                texture.Dispose();
            }
            catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }
        }

        _textures.Clear();
        _disposed = true;
        firstFailure?.Throw();
    }

    private bool DrawContent(OpenGLRenderer renderer, MainHudQuickbarSlotBounds bounds, MainHudQuickbarSlotSnapshot slot, uint now, out uint cooldown)
    {
        cooldown = 0;

        switch (slot.ContentKind)
        {
            case (byte)MainHudQuickbarContentKind.Item:
                if (_itemIconKeyResolver is null || !_itemIconKeyResolver.TryResolve(slot.ContentId, slot.Selector, out uint iconKey))
                {
                    return false;
                }

                OpenGLTexture2D? item = GetTexture($"item:{iconKey}", () => _assets.GetItemFrame(iconKey), exactWidth: 64, exactHeight: 64);

                if (item is null)
                {
                    return false;
                }

                renderer.DrawSprite(item, bounds.X, bounds.Y, MainHudQuickbarDefinition.CellWidth, MainHudQuickbarDefinition.CellHeight);
                return true;

            case (byte)MainHudQuickbarContentKind.Action:
                DrawNatural(renderer, $"control:ButtonA{slot.ContentId}", () => _assets.GetControlFrame0($"ButtonA{slot.ContentId}"), bounds.X + 3, bounds.Y + 5, 64, 64);
                return false;

            case (byte)MainHudQuickbarContentKind.Magic:
            case (byte)MainHudQuickbarContentKind.XpMagic:
                string section = slot.ContentKind == (byte)MainHudQuickbarContentKind.Magic ? $"MagicSkillType{slot.ContentId}" : $"XpSkillType{slot.ContentId}";
                OpenGLTexture2D? magic = GetTexture($"magic:{section}", () => _assets.GetMagicFrame0(section), minimumWidth: 50, minimumHeight: 50);

                if (magic is not null)
                {
                    renderer.DrawSprite(magic, new SpriteSourceRectangle(0, 0, 50, 50), bounds.X + 2, bounds.Y + 4, 37, 37);
                }

                cooldown = _cooldownSource?.GetRemainingMilliseconds(slot.ContentId, now) ?? 0;
                return false;

            case (byte)MainHudQuickbarContentKind.Dance:
                DrawNatural(renderer, $"control:Action_Dance{slot.ContentId}Btn", () => _assets.GetControlFrame0($"Action_Dance{slot.ContentId}Btn"), bounds.X + 3, bounds.Y + 5, 64, 64);
                return false;

            case (byte)MainHudQuickbarContentKind.WeaponSwap:
                string swapSection = _useAlternateSwapIcon() ? "Swapuse_SwapmainbBtn" : "Swapuse_UsemainbBtn";
                DrawNatural(renderer, $"control:{swapSection}", () => _assets.GetControlFrame0(swapSection), bounds.X + 3, bounds.Y + 5, 64, 64);
                return false;

            default:
                return false;
        }
    }

    private void DrawGlow(OpenGLRenderer renderer, MainHudQuickbarState state, int slotIndex, MainHudQuickbarSlotBounds bounds, MainHudQuickbarSlotSnapshot slot, uint now)
    {
        if (slot.GlowKind == 0)
        {
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
            return;
        }

        AniFrameSet? frames = _assets.GetEffectFrames(section);

        if (frames is null || frames.FrameCount == 0)
        {
            return;
        }

        uint start = slot.GlowStartTime;
        int frameIndex;

        if (start == 0)
        {
            state.Slots.SetGlowStartTime(slotIndex, now);
            frameIndex = 0;
        }
        else
        {
            frameIndex = (int)(unchecked(now - start) / 100 % (uint)frames.FrameCount);
        }

        OpenGLTexture2D? texture = GetTexture($"effect:{section}:{frameIndex}", () => frames.GetFrame((uint)frameIndex), exactWidth: 64, exactHeight: 64);

        if (texture is not null)
        {
            renderer.DrawSprite(texture, bounds.X, bounds.Y, MainHudQuickbarDefinition.CellWidth, MainHudQuickbarDefinition.CellHeight);
        }
    }

    private void DrawQuantity(OpenGLRenderer renderer, MainHudQuickbarSlotBounds bounds, uint quantity)
    {
        string text = quantity.ToString(CultureInfo.InvariantCulture);
        int x = bounds.X + 2;

        foreach (char digit in text)
        {
            string section = $"Main3_Num{digit}Pic";
            OpenGLTexture2D? texture = GetTexture($"control:{section}", () => _assets.GetControlFrame0(section), exactWidth: 16, exactHeight: 16);

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
        uint addLevel = slot.Metadata.AddLevel;

        if (addLevel == 0 || slot.ContentId / 10000 % 100 == 73)
        {
            return;
        }

        string text = addLevel.ToString(CultureInfo.InvariantCulture);
        int x = bounds.X + MainHudQuickbarDefinition.CellWidth - 11;

        for (int index = text.Length - 1; index >= 0; index--)
        {
            string section = $"Equip_Num{text[index]}";
            OpenGLTexture2D? digit = GetTexture($"control:{section}", () => _assets.GetControlFrame0(section));

            if (digit is null)
            {
                break;
            }

            renderer.DrawSprite(digit, x, bounds.Y);
            x -= 9;
        }

        OpenGLTexture2D? plus = GetTexture("control:Equip_AddPic", () => _assets.GetControlFrame0("Equip_AddPic"));

        if (plus is not null)
        {
            renderer.DrawSprite(plus, x, bounds.Y);
        }
    }

    private void DrawCover(OpenGLRenderer renderer, MainHudQuickbarSlotBounds bounds)
    {
        OpenGLTexture2D? texture = GetTexture("control:Compose_CoverPic", _assets.GetCoverFrame, exactWidth: 64, exactHeight: 64);

        if (texture is not null)
        {
            renderer.DrawSprite(texture, bounds.X, bounds.Y, MainHudQuickbarDefinition.CellWidth, MainHudQuickbarDefinition.CellHeight);
        }
    }

    private void DrawNatural(OpenGLRenderer renderer, string key, Func<RgbaImage?> load, int x, int y, int width, int height)
    {
        OpenGLTexture2D? texture = GetTexture(key, load, exactWidth: width, exactHeight: height);

        if (texture is not null)
        {
            renderer.DrawSprite(texture, x, y);
        }
    }

    private OpenGLTexture2D? GetTexture(string key, Func<RgbaImage?> load, int? exactWidth = null, int? exactHeight = null, int? minimumWidth = null, int? minimumHeight = null)
    {
        if (_textures.TryGetValue(key, out OpenGLTexture2D? cached))
        {
            return cached;
        }

        RgbaImage? image = load();

        if (image is null)
        {
            return null;
        }

        if (exactWidth is not null && image.Width != exactWidth || exactHeight is not null && image.Height != exactHeight)
        {
            throw new InvalidDataException($"Quickbar asset '{key}' decoded as {image.Width}x{image.Height}; expected {exactWidth}x{exactHeight}.");
        }

        if (minimumWidth is not null && image.Width < minimumWidth || minimumHeight is not null && image.Height < minimumHeight)
        {
            throw new InvalidDataException($"Quickbar asset '{key}' decoded as {image.Width}x{image.Height}; expected at least {minimumWidth}x{minimumHeight}.");
        }

        OpenGLTexture2D texture = _graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
        _textures.Add(key, texture);
        return texture;
    }
}
