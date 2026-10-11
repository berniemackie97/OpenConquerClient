using OpenConquer.Client.UI.Hud.ActionButtons;
using OpenConquer.Client.UI.Hud.CheckControls;
using OpenConquer.Client.UI.Hud.Chrome;
using OpenConquer.Client.UI.Hud.Input;
using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Client.UI.Hud.SelectedSkill;
using OpenConquer.Client.UI.Hud.SkillExperience;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;
using OpenConquer.Client.UI.Hud.Vitals;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud.Rendering;

/// <summary>
/// Owns gameplay HUD content, input, and GPU resources as a single authenticated-world lifetime.
/// Construct only after a world session has been authorized and its OpenGL context is active.
/// </summary>
internal sealed class WorldHudRuntime : IDisposable
{
    private static readonly Func<uint> s_readTickCount = static () => unchecked((uint)Environment.TickCount64);

    private readonly IMainHudMagicHintSource? _magicHintSource;
    private readonly MainHudVitalsState _vitalsState = new();
    private readonly MainHudSkillExperienceState _skillState = new();
    private readonly MainHudQuickbarState _quickbarState = new();
    private readonly MainHudActionButtonStripState _actionState = new();
    private readonly MainHudCheckControlsState _checkState = new();
    private readonly MainHudSelectedSkillState _selectedSkillState = new();
    private readonly MainHudSelectedSkillCooldownState _cooldownState = new();
    private readonly MainHudStatusHintState _statusHintState = new();

    private readonly MainHudChromeAssets _chromeAssets;
    private readonly MainHudActionButtonAssets _actionAssets;
    private readonly MainHudCheckControlAssets _checkAssets;
    private readonly MainHudInputCoordinator _input;
    private readonly MainHudRenderResources _resources;
    private bool _disposed;

    public WorldHudRuntime(IClientContentSource contentSource, OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize logicalRenderSize, IMainHudMagicHintSource? magicHintSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        _magicHintSource = magicHintSource;

        SelectedMagicCooldownTextConfiguration cooldownText = SelectedMagicCooldownTextConfiguration.Load(contentSource);
        ClientFontSettingsConfiguration fontSettings = ClientFontSettingsConfiguration.Load(contentSource);
        ClientFontSizeConfiguration fontSize = ClientFontSizeConfiguration.Load(contentSource);
        ClientCodePageConfiguration codePage = ClientCodePageConfiguration.Load(contentSource);

        _chromeAssets = MainHudChromeAssets.Load(contentSource);
        MainHudVitalsAssets vitalsAssets = MainHudVitalsAssets.Load(contentSource);
        MainHudSkillAssets skillAssets = MainHudSkillAssets.Load(contentSource);
        MainHudQuickbarAssets quickbarAssets = new(contentSource);
        _actionAssets = MainHudActionButtonAssets.Load(contentSource);
        _checkAssets = MainHudCheckControlAssets.Load(contentSource);
        MainHudSelectedSkillAssets selectedSkillAssets = new(contentSource);
        MainHudStatusHintAssets statusHintAssets = MainHudStatusHintAssets.Load(contentSource);
        MainHudMagicHintContent magicHintContent = MainHudMagicHintContent.Load(contentSource);

        MainHudQuickbarLayout quickbarLayout = MainHudQuickbarLayout.Create(logicalRenderSize);
        MainHudActionButtonLayout actionLayout = MainHudActionButtonLayout.Create(logicalRenderSize);
        MainHudCheckControlLayout checkLayout = MainHudCheckControlLayout.Create(logicalRenderSize);
        MainHudStatusHintLayout statusHintLayout = MainHudStatusHintLayout.Create(logicalRenderSize);

        MainHudQuickbarInput quickbarInput = new(_quickbarState, quickbarLayout);
        MainHudActionButtonStripInput actionInput = new(_actionState, actionLayout, IsActionAvailable);
        MainHudCheckControlsInput checkInput = new(_checkState, checkLayout, IsCheckAvailable);
        MainHudStatusHintInput statusHintInput = new(_statusHintState, statusHintLayout,
            statusHintAssets.SkillRegion, statusHintAssets.ManaRegion, statusHintAssets.LifeRegion,
            statusHintAssets.Strings.UsesArabicLayout);

        _input = new MainHudInputCoordinator(actionInput, quickbarInput, checkInput, statusHintInput, _statusHintState);

        MainHudRenderResourceDependencies dependencies = new()
        {
            ChromeAssets = _chromeAssets,
            VitalsAssets = vitalsAssets,
            SkillAssets = skillAssets,
            QuickbarAssets = quickbarAssets,
            ActionButtonAssets = _actionAssets,
            CheckControlAssets = _checkAssets,
            SelectedSkillAssets = selectedSkillAssets,
            StatusHintAssets = statusHintAssets,
            MagicHintContent = magicHintContent,
            CooldownTextConfiguration = cooldownText,
            FontSettingsConfiguration = fontSettings,
            FontSizeConfiguration = fontSize,
            CodePageConfiguration = codePage,
        };

        _resources = new MainHudRenderResources(graphicsDevice, logicalRenderSize, dependencies, s_readTickCount);
    }

    public void HandlePointerMoved(bool mapped, int x, int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _input.HandlePointerMoved(_chromeAssets.HasDialogPanels, mapped, x, y);
    }

    public void HandlePrimaryPointerPressed(bool mapped, int x, int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _input.HandlePrimaryPointerPressed(_chromeAssets.HasDialogPanels, mapped, x, y);
    }

    public void HandlePrimaryPointerReleased(bool mapped, int x, int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _input.HandlePrimaryPointerReleased(_chromeAssets.HasDialogPanels, mapped, x, y);
    }

    public void Draw(OpenGLRenderer renderer, DesktopWindow window)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(window);

        _resources.Chrome.DrawBackground(renderer);
        _resources.Vitals.Draw(renderer, _vitalsState);

        if (_resources.Chrome.DrawPanels(renderer))
        {
            _resources.SkillExperience.Draw(renderer, _skillState);
            _resources.Quickbar.Draw(renderer, _quickbarState);

            if (window.TryGetPointerPosition(out PixelPoint point))
            {
                bool mapped = renderer.Viewport.TryMapPointerToLogical(point.X, point.Y, out int x, out int y);
                _input.PollQuickbarPointer(mapped, x, y);
            }

            _actionState.AdvancePkBeforeDraw(s_readTickCount);
            _actionState.AdvanceOrganiseBeforeDraw(s_readTickCount);
            _resources.ActionButtons.Draw(renderer, _actionState);
            _resources.CheckControls.Draw(renderer, _checkState);
            _resources.SelectedSkill.Draw(renderer, _selectedSkillState);
            _resources.SelectedSkillCooldown.DrawAfterSelectedImage(renderer, _selectedSkillState, _cooldownState);
            _skillState.AdvanceAfterHudDraw(unchecked((uint)Environment.TickCount64));
        }

        _resources.StatusHints.Draw(renderer, _statusHintState, _checkState, _vitalsState, _skillState);
        MainHudMagicHintRuntimeSnapshot? hintRuntime = _magicHintSource?.Capture();
        _resources.MagicHint.Draw(renderer, _statusHintState, hintRuntime);
        _resources.EquipmentHint.Draw(renderer, _statusHintState, hintRuntime?.EquipmentSwitchState);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _resources.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }

    private bool IsActionAvailable(MainHudActionButtonId id) => id == MainHudActionButtonId.Button47
        ? _actionAssets.IsPkSkinAvailable(_actionState.PkSkin)
        : _actionAssets.IsAvailable(id);

    private bool IsCheckAvailable(MainHudCheckControlId id) => _checkAssets.IsAvailable(id);
}
