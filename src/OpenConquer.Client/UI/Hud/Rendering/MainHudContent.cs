using OpenConquer.Client.UI.Hud.ActionButtons;
using OpenConquer.Client.UI.Hud.CheckControls;
using OpenConquer.Client.UI.Hud.Chrome;
using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Client.UI.Hud.SelectedSkill;
using OpenConquer.Client.UI.Hud.SkillExperience;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;
using OpenConquer.Client.UI.Hud.Vitals;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;

namespace OpenConquer.Client.UI.Hud.Rendering;

/// <summary>Prepared HUD assets and settings for one admitted world. OpenGL resources are created separately.</summary>
internal sealed class MainHudContent
{
    public required MainHudChromeAssets ChromeAssets
    {
        get; init;
    }

    public required MainHudVitalsAssets VitalsAssets
    {
        get; init;
    }

    public required MainHudSkillAssets SkillAssets
    {
        get; init;
    }

    public required MainHudQuickbarAssets QuickbarAssets
    {
        get; init;
    }

    public required MainHudActionButtonAssets ActionButtonAssets
    {
        get; init;
    }

    public required MainHudCheckControlAssets CheckControlAssets
    {
        get; init;
    }

    public required MainHudSelectedSkillAssets SelectedSkillAssets
    {
        get; init;
    }

    public required MainHudStatusHintAssets StatusHintAssets
    {
        get; init;
    }

    public required MainHudMagicHintContent MagicHintContent
    {
        get; init;
    }

    public required SelectedMagicCooldownTextConfiguration CooldownTextConfiguration
    {
        get; init;
    }

    public required ClientFontSettingsConfiguration FontSettingsConfiguration
    {
        get; init;
    }

    public required ClientFontSizeConfiguration FontSizeConfiguration
    {
        get; init;
    }

    public required ClientCodePageConfiguration CodePageConfiguration
    {
        get; init;
    }

    internal static MainHudContent Load(IClientContentSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        cancellationToken.ThrowIfCancellationRequested();
        SelectedMagicCooldownTextConfiguration cooldownText = SelectedMagicCooldownTextConfiguration.Load(source);
        cancellationToken.ThrowIfCancellationRequested();
        ClientFontSettingsConfiguration fontSettings = ClientFontSettingsConfiguration.Load(source);
        ClientFontSizeConfiguration fontSize = ClientFontSizeConfiguration.Load(source);
        ClientCodePageConfiguration codePage = ClientCodePageConfiguration.Load(source);

        cancellationToken.ThrowIfCancellationRequested();
        MainHudChromeAssets chrome = MainHudChromeAssets.Load(source);
        MainHudVitalsAssets vitals = MainHudVitalsAssets.Load(source);
        MainHudSkillAssets skill = MainHudSkillAssets.Load(source);

        cancellationToken.ThrowIfCancellationRequested();
        MainHudActionButtonAssets actions = MainHudActionButtonAssets.Load(source);
        MainHudCheckControlAssets checks = MainHudCheckControlAssets.Load(source);
        MainHudStatusHintAssets hints = MainHudStatusHintAssets.Load(source);

        cancellationToken.ThrowIfCancellationRequested();
        MainHudMagicHintContent magicHints = MainHudMagicHintContent.Load(source);
        cancellationToken.ThrowIfCancellationRequested();

        return new MainHudContent
        {
            ChromeAssets = chrome,
            VitalsAssets = vitals,
            SkillAssets = skill,
            QuickbarAssets = new MainHudQuickbarAssets(source),
            ActionButtonAssets = actions,
            CheckControlAssets = checks,
            SelectedSkillAssets = new MainHudSelectedSkillAssets(source),
            StatusHintAssets = hints,
            MagicHintContent = magicHints,
            CooldownTextConfiguration = cooldownText,
            FontSettingsConfiguration = fontSettings,
            FontSizeConfiguration = fontSize,
            CodePageConfiguration = codePage,
        };
    }
}
