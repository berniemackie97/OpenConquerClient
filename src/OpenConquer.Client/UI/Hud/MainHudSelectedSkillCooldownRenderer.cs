using System.Buffers.Text;
using OpenConquer.Content.Configuration;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudSelectedSkillCooldownRenderer : IDisposable
{
    private readonly OpenGLTextContext _textContext;
    private readonly MainHudSelectedSkillBounds _bounds;
    private readonly NativeTextRenderOptions _renderOptions;
    private readonly int _offsetX;
    private readonly int _offsetY;

    private OpenGLTextLayout? _cachedLayout;
    private uint _cachedSeconds;
    private bool _disposed;

    public MainHudSelectedSkillCooldownRenderer(OpenGLGraphicsDevice graphicsDevice, SelectedMagicCooldownTextConfiguration cooldownConfiguration, ClientFontSettingsConfiguration fontSettings, ClientCodePageConfiguration codePageConfiguration, LogicalRenderSize logicalRenderSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(cooldownConfiguration);
        ArgumentNullException.ThrowIfNull(fontSettings);
        ArgumentNullException.ThrowIfNull(codePageConfiguration);

        _bounds = MainHudSelectedSkillLayout.Create(logicalRenderSize).GetBounds();
        _offsetX = cooldownConfiguration.OffsetX;
        _offsetY = cooldownConfiguration.OffsetY;

        SpriteColor textColor = FromArgb(cooldownConfiguration.ColorArgb);
        SpriteColor cornerColor = FromArgb(fontSettings.DefaultCornerColorArgb);

        _renderOptions = new NativeTextRenderOptions((NativeTextRenderStyle)fontSettings.DefaultRenderTextStyle,
            textColor,
            cornerColor,
            ClientFontSettingsConfiguration.DefaultCornerOffsetXPixels,
            ClientFontSettingsConfiguration.DefaultCornerOffsetYPixels,
            NativeTextVertexColors.Solid(textColor));

        _textContext = new OpenGLTextContext(graphicsDevice, fontSettings.GuiFontFaceName, cooldownConfiguration.FontSizePixels, codePageConfiguration.EffectiveCodePage, fontSettings.AntialiasEnabled);
    }

    internal MainHudSelectedSkillCooldownRenderer(OpenGLTextContext ownedTextContext, LogicalRenderSize logicalRenderSize, int offsetX, int offsetY, NativeTextRenderOptions renderOptions)
    {
        ArgumentNullException.ThrowIfNull(ownedTextContext);

        _textContext = ownedTextContext;
        _bounds = MainHudSelectedSkillLayout.Create(logicalRenderSize).GetBounds();
        _offsetX = offsetX;
        _offsetY = offsetY;
        _renderOptions = renderOptions;
    }

    public void DrawAfterSelectedImage(OpenGLRenderer renderer, MainHudSelectedSkillState selectedSkillState, MainHudSelectedSkillCooldownState cooldownState)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(selectedSkillState);
        ArgumentNullException.ThrowIfNull(cooldownState);

        selectedSkillState.SetCoverFlag(0);

        uint seconds = GetDisplaySeconds(cooldownState.RemainingMilliseconds);

        if (seconds == 0)
        {
            return;
        }

        OpenGLTextLayout layout = GetLayout(seconds);
        (int x, int y) = GetTextOrigin(_bounds, _offsetX, _offsetY);

        _textContext.Draw(renderer, layout, _renderOptions, x, y);
        selectedSkillState.SetCoverFlag(1);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _textContext.Dispose();
        }
        finally
        {
            _cachedLayout = null;
            _disposed = true;
        }
    }

    internal static uint GetDisplaySeconds(uint remainingMilliseconds)
    {
        return remainingMilliseconds == 0 ? 0 : ((remainingMilliseconds - 1) / 1000) + 1;
    }

    internal static (int X, int Y) GetTextOrigin(MainHudSelectedSkillBounds bounds, int offsetX, int offsetY)
    {
        return (unchecked(bounds.X + offsetX), unchecked(bounds.Y + offsetY));
    }

    private OpenGLTextLayout GetLayout(uint seconds)
    {
        if (_cachedLayout is not null && _cachedSeconds == seconds)
        {
            return _cachedLayout;
        }

        Span<byte> text = stackalloc byte[10];

        if (!Utf8Formatter.TryFormat(seconds, text, out int written))
        {
            throw new InvalidOperationException($"Could not format selected-skill cooldown value {seconds}.");
        }

        _cachedLayout = _textContext.Layout(text[..written]);
        _cachedSeconds = seconds;

        return _cachedLayout;
    }

    private static SpriteColor FromArgb(uint argb)
    {
        return new SpriteColor((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
    }
}
