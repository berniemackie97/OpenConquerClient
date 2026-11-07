using OpenConquer.Client.UI.Hud.ActionButtons;
using OpenConquer.Client.UI.Hud.CheckControls;
using OpenConquer.Client.UI.Hud.Chrome;
using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Client.UI.Hud.SelectedSkill;
using OpenConquer.Client.UI.Hud.SkillExperience;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Client.UI.Hud.StatusHints.Equipment;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;
using OpenConquer.Client.UI.Hud.Vitals;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud.Rendering;

internal sealed class MainHudRenderResources : IDisposable
{
    private readonly ResourceOwnership _ownership = new();

    public MainHudChromeRenderer Chrome
    {
        get;
    }

    public MainHudVitalsRenderer Vitals
    {
        get;
    }

    public MainHudSkillExperienceRenderer SkillExperience
    {
        get;
    }

    public MainHudQuickbarRenderer Quickbar
    {
        get;
    }

    public MainHudActionButtonStripRenderer ActionButtons
    {
        get;
    }

    public MainHudCheckControlRenderer CheckControls
    {
        get;
    }

    public MainHudSelectedSkillRenderer SelectedSkill
    {
        get;
    }

    public MainHudSelectedSkillCooldownRenderer SelectedSkillCooldown
    {
        get;
    }

    public MainHudStatusHintRenderer StatusHints
    {
        get;
    }

    public MainHudMagicHintRenderer MagicHint
    {
        get;
    }

    public MainHudEquipmentHintRenderer EquipmentHint
    {
        get;
    }

    public MainHudRenderResources(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize logicalRenderSize,
        MainHudContent content, Func<uint> readTickCount)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(readTickCount);

        try
        {
            Chrome = _ownership.Track(new MainHudChromeRenderer(graphicsDevice, content.ChromeAssets, logicalRenderSize));
            Vitals = _ownership.Track(new MainHudVitalsRenderer(graphicsDevice, content.VitalsAssets, logicalRenderSize));
            SkillExperience = _ownership.Track(new MainHudSkillExperienceRenderer(graphicsDevice, content.SkillAssets, logicalRenderSize));
            Quickbar = _ownership.Track(new MainHudQuickbarRenderer(graphicsDevice, content.QuickbarAssets, logicalRenderSize, readTickCount: readTickCount));
            ActionButtons = _ownership.Track(new MainHudActionButtonStripRenderer(graphicsDevice, content.ActionButtonAssets, logicalRenderSize));
            CheckControls = _ownership.Track(new MainHudCheckControlRenderer(graphicsDevice, content.CheckControlAssets, logicalRenderSize));
            SelectedSkill = _ownership.Track(new MainHudSelectedSkillRenderer(graphicsDevice, content.SelectedSkillAssets, logicalRenderSize));
            SelectedSkillCooldown = _ownership.Track(new MainHudSelectedSkillCooldownRenderer(graphicsDevice,
                content.CooldownTextConfiguration, content.FontSettingsConfiguration, content.CodePageConfiguration, logicalRenderSize));
            StatusHints = _ownership.Track(new MainHudStatusHintRenderer(graphicsDevice, content.StatusHintAssets,
                content.FontSettingsConfiguration, content.FontSizeConfiguration, content.CodePageConfiguration, logicalRenderSize));
            MagicHint = _ownership.Track(new MainHudMagicHintRenderer(graphicsDevice, StatusHints, content.MagicHintContent,
                content.StatusHintAssets.Strings, content.FontSettingsConfiguration, content.CodePageConfiguration, logicalRenderSize));
            EquipmentHint = new MainHudEquipmentHintRenderer(StatusHints, content.MagicHintContent.KeyedStrings,
                content.FontSizeConfiguration, logicalRenderSize, content.StatusHintAssets.Strings.UsesArabicLayout);
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    public void Dispose() => _ownership.Dispose();
}
