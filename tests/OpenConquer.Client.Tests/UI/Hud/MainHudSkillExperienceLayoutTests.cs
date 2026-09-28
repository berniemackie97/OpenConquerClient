using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudSkillExperienceLayoutTests
{
    [Theory]
    [InlineData(800, 600, 459, 509, 555)]
    [InlineData(1024, 768, 627, 677, 723)]
    public void Create_ReturnsVerifiedNativeCoordinates(int width, int height, int originY, int skillY, int experienceY)
    {
        MainHudSkillExperienceLayout layout = MainHudSkillExperienceLayout.Create(new LogicalRenderSize(width, height));

        Assert.Equal(originY, layout.OriginY);
        Assert.Equal(skillY, layout.SkillY);
        Assert.Equal(experienceY, layout.ExperienceY);
        Assert.Equal(0, MainHudSkillExperienceLayout.SkillX);
        Assert.Equal(99, MainHudSkillExperienceLayout.ExperienceX);
        Assert.Equal(398, MainHudSkillExperienceLayout.ExperienceWidth);
        Assert.Equal(4, MainHudSkillExperienceLayout.ExperienceHeight);
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void Create_RejectsUnverifiedLogicalRenderSizes(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudSkillExperienceLayout.Create(new LogicalRenderSize(width, height)));
    }
}
