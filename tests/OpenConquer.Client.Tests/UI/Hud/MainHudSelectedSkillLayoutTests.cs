using OpenConquer.Client.UI.Hud.SelectedSkill;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudSelectedSkillLayoutTests
{
    [Theory]
    [InlineData(800, 600, 753, 555)]
    [InlineData(1024, 768, 753, 723)]
    public void GetBounds_MatchesVerifiedNativeRectangle(int width, int height, int expectedX, int expectedY)
    {
        MainHudSelectedSkillBounds bounds = MainHudSelectedSkillLayout.Create(new LogicalRenderSize(width, height)).GetBounds();

        Assert.Equal(expectedX, bounds.X);
        Assert.Equal(expectedY, bounds.Y);
        Assert.Equal(47, MainHudSelectedSkillBounds.Width);
        Assert.Equal(46, MainHudSelectedSkillBounds.Height);
    }

    [Theory]
    [InlineData(800, 600, 601)]
    [InlineData(1024, 768, 769)]
    public void GetBounds_PreservesNativeOnePixelExtensionBelowLogicalFramebuffer(int width, int height, int expectedBottom)
    {
        MainHudSelectedSkillBounds bounds = MainHudSelectedSkillLayout.Create(new LogicalRenderSize(width, height)).GetBounds();

        Assert.Equal(expectedBottom, bounds.Y + MainHudSelectedSkillBounds.Height);
        Assert.Equal(height + 1, bounds.Y + MainHudSelectedSkillBounds.Height);
    }

    [Fact]
    public void Contains_UsesNativeHalfOpenHwndRectangle()
    {
        MainHudSelectedSkillBounds bounds = MainHudSelectedSkillLayout.Create(new LogicalRenderSize(800, 600)).GetBounds();

        Assert.True(bounds.Contains(753, 555));
        Assert.True(bounds.Contains(799, 600));
        Assert.False(bounds.Contains(752, 555));
        Assert.False(bounds.Contains(800, 555));
        Assert.False(bounds.Contains(753, 554));
        Assert.False(bounds.Contains(753, 601));
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void Create_RejectsUnverifiedLogicalRenderSize(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudSelectedSkillLayout.Create(new LogicalRenderSize(width, height)));
}
