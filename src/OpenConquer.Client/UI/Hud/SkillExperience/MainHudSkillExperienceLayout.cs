using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud.SkillExperience;

internal readonly record struct MainHudSkillExperienceLayout
{
    public const int SkillX = 0;
    public const int ExperienceX = 99;
    public const int ExperienceWidth = 398;
    public const int ExperienceHeight = 4;

    private MainHudSkillExperienceLayout(int originY)
    {
        OriginY = originY;
    }

    public int OriginY
    {
        get;
    }
    public int SkillY => OriginY + 50;
    public int ExperienceY => OriginY + 96;

    public static MainHudSkillExperienceLayout Create(LogicalRenderSize logicalRenderSize)
    {
        bool supported = logicalRenderSize is { Width: 800, Height: 600 } or { Width: 1024, Height: 768 };

        if (!supported)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalRenderSize), logicalRenderSize, "The native 5517 main HUD skill/experience layout is only verified for 800x600 and 1024x768 logical rendering.");
        }

        return new MainHudSkillExperienceLayout(logicalRenderSize.Height - 141);
    }
}
