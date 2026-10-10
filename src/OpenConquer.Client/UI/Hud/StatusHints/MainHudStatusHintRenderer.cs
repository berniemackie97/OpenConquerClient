using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using OpenConquer.Client.UI.Hud.CheckControls;
using OpenConquer.Client.UI.Hud.SkillExperience;
using OpenConquer.Client.UI.Hud.Vitals;
using OpenConquer.Content.Configuration;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Client.UI.Hud.StatusHints;

internal sealed class MainHudStatusHintRenderer : IDisposable
{
    private static readonly SpriteSourceRectangle s_backdropSource = new(0, 0, MainHudStatusHintDefinition.Dialog21SourceWidth, MainHudStatusHintDefinition.Dialog21SourceHeight);

    private readonly MainHudStatusHintLayout _layout;
    private readonly MainHudStatusHintAssets _assets;
    private readonly OpenGLTexture2D? _backdrop;
    private readonly OpenGLTextContext _textContext;
    private readonly NativeTextRenderOptions _textOptions;
    private readonly Dictionary<int, OpenGLTextLayout> _fixedLayouts = [];

    private OpenGLTextLayout? _gaugeLayout;
    private MainHudStatusHintKind _gaugeKind;
    private int _gaugeValue;
    private int _gaugeMaximum;
    private bool _disposed;

    internal OpenGLTexture2D? BackdropTexture => _backdrop;
    internal OpenGLTextContext NormalTextContext => _textContext;
    internal NativeTextRenderOptions NormalTextOptions => _textOptions;

    public MainHudStatusHintRenderer(OpenGLGraphicsDevice graphicsDevice, MainHudStatusHintAssets assets, ClientFontSettingsConfiguration fontSettings, ClientFontSizeConfiguration fontSize, ClientCodePageConfiguration codePage, LogicalRenderSize logicalRenderSize)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(fontSettings);
        ArgumentNullException.ThrowIfNull(fontSize);
        ArgumentNullException.ThrowIfNull(codePage);

        _assets = assets;
        _layout = MainHudStatusHintLayout.Create(logicalRenderSize);

        SpriteColor textColor = SpriteColor.White;
        SpriteColor cornerColor = FromArgb(fontSettings.DefaultCornerColorArgb);

        _textOptions = new NativeTextRenderOptions((NativeTextRenderStyle)fontSettings.DefaultRenderTextStyle, textColor, cornerColor, ClientFontSettingsConfiguration.DefaultCornerOffsetXPixels, ClientFontSettingsConfiguration.DefaultCornerOffsetYPixels, NativeTextVertexColors.Solid(textColor));

        OpenGLTexture2D? backdrop = null;

        try
        {
            if (assets.Backdrop is { } image)
            {
                backdrop = graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span);
            }

            _textContext = new OpenGLTextContext(graphicsDevice, fontSettings.GuiFontFaceName, fontSize.NormalFontHeightPixels, codePage.EffectiveCodePage, fontSettings.AntialiasEnabled);
            _backdrop = backdrop;
        }
        catch
        {
            try
            {
                backdrop?.Dispose();
            }
            catch
            {
                // Preserve the initialization failure.
            }

            throw;
        }
    }

    internal MainHudStatusHintRenderer(MainHudStatusHintAssets assets, MainHudStatusHintLayout layout, OpenGLTexture2D? ownedBackdrop, OpenGLTextContext ownedTextContext, NativeTextRenderOptions textOptions)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(ownedTextContext);

        _assets = assets;
        _layout = layout;
        _backdrop = ownedBackdrop;
        _textContext = ownedTextContext;
        _textOptions = textOptions;
    }

    public void Draw(OpenGLRenderer renderer, MainHudStatusHintState state, MainHudCheckControlsState checks, MainHudVitalsState vitals, MainHudSkillExperienceState skill)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(vitals);
        ArgumentNullException.ThrowIfNull(skill);

        if (!state.CanRender || !TryGetLayout(state.Kind, checks, vitals, skill, out OpenGLTextLayout? textLayout) || textLayout is null)
        {
            return;
        }

        // Native category-8 order: font lookup, text measurement, then Dialog21 resource guard.
        if (_backdrop is not { } backdrop || textLayout.WidthPixels <= 0 || textLayout.HeightPixels <= 0)
        {
            return;
        }

        MainHudStatusHintAnchor anchor = _layout.GetAnchor(state.Kind);
        int backdropX = anchor.X;
        int textX = backdropX;

        if (_assets.Strings.UsesArabicLayout)
        {
            if ((long)backdropX + textLayout.WidthPixels > _layout.LogicalWidth)
            {
                backdropX = _layout.LogicalWidth - textLayout.WidthPixels;
            }

            textX = backdropX + textLayout.WidthPixels;
        }

        renderer.DrawSprite(backdrop, s_backdropSource, backdropX, anchor.Y, textLayout.WidthPixels, textLayout.HeightPixels);
        _textContext.Draw(renderer, textLayout, _textOptions, textX, anchor.Y);
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
            _textContext.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _backdrop?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            _fixedLayouts.Clear();
            _gaugeLayout = null;
            _disposed = true;
        }

        firstFailure?.Throw();
    }

    private bool TryGetLayout(MainHudStatusHintKind kind, MainHudCheckControlsState checks, MainHudVitalsState vitals, MainHudSkillExperienceState skill, out OpenGLTextLayout? layout)
    {
        layout = null;

        if (kind == MainHudStatusHintKind.Chat)
        {
            // The only verified HUD mouse producer cannot select the hidden chat control.
            return false;
        }

        if (kind is MainHudStatusHintKind.Skill or MainHudStatusHintKind.Mana or MainHudStatusHintKind.Life)
        {
            if (!TryGetGauge(kind, vitals, skill, out int value, out int maximum))
            {
                return false;
            }

            if (_gaugeLayout is not null && _gaugeKind == kind && _gaugeValue == value && _gaugeMaximum == maximum)
            {
                layout = _gaugeLayout;
                return true;
            }

            string formatted = string.Concat(Math.Min(value, maximum).ToString(CultureInfo.InvariantCulture), "/", maximum.ToString(CultureInfo.InvariantCulture));

            layout = _textContext.Layout(Encoding.ASCII.GetBytes(formatted));
            _gaugeLayout = layout;
            _gaugeKind = kind;
            _gaugeValue = value;
            _gaugeMaximum = maximum;

            return true;
        }

        int stringId = kind switch
        {
            MainHudStatusHintKind.WalkRun => MainHudStatusHintDefinition.WalkRunStringId,
            MainHudStatusHintKind.Map => MainHudStatusHintDefinition.MapStringId,
            MainHudStatusHintKind.ScreenShift => checks.GetControl(MainHudCheckControlId.Check46).CurrentState != 0
                ? MainHudStatusHintDefinition.ScreenShiftOnStringId
                : MainHudStatusHintDefinition.ScreenShiftOffStringId,
            MainHudStatusHintKind.Equipment => MainHudStatusHintDefinition.EquipmentStringId,
            _ => 0,
        };

        if (stringId == 0)
        {
            return false;
        }

        if (!_fixedLayouts.TryGetValue(stringId, out layout))
        {
            layout = _textContext.Layout(_assets.Strings.GetEncoded(stringId).Span);
            _fixedLayouts.Add(stringId, layout);
        }

        return true;
    }

    private static bool TryGetGauge(MainHudStatusHintKind kind, MainHudVitalsState vitals, MainHudSkillExperienceState skill, out int value, out int maximum)
    {
        value = 0;
        maximum = 0;

        switch (kind)
        {
            case MainHudStatusHintKind.Skill when skill.Snapshot is { } skillSnapshot:
                value = skillSnapshot.Skill;
                maximum = MainHudStatusHintDefinition.SkillMaximum;
                return true;

            case MainHudStatusHintKind.Mana when vitals.LifeSubVariant != 1 && vitals.Snapshot is { } manaSnapshot:
                value = manaSnapshot.CurrentMana;
                maximum = manaSnapshot.MaxMana;
                return true;

            case MainHudStatusHintKind.Life when vitals.Snapshot is { } lifeSnapshot:
                value = lifeSnapshot.CurrentLife;
                maximum = lifeSnapshot.MaxLife;
                return true;

            default:
                return false;
        }
    }

    private static SpriteColor FromArgb(uint argb)
    {
        return new SpriteColor((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
    }
}
