using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudActionButtonLayoutTests
{
    public static TheoryData<int, int, MainHudActionButtonId, int, int> VerifiedBounds => new()
    {
        { 800, 600, MainHudActionButtonId.Button40, 502, 553 },
        { 800, 600, MainHudActionButtonId.Button410, 702, 578 },
        { 800, 600, MainHudActionButtonId.Button42, 552, 553 },
        { 800, 600, MainHudActionButtonId.Button43, 652, 578 },
        { 800, 600, MainHudActionButtonId.Main3MissionBtn, 502, 578 },
        { 800, 600, MainHudActionButtonId.Button45, 552, 578 },
        { 800, 600, MainHudActionButtonId.Button46, 602, 578 },
        { 800, 600, MainHudActionButtonId.Button47, 652, 553 },
        { 800, 600, MainHudActionButtonId.Main3OrganiseBtn, 702, 553 },
        { 800, 600, MainHudActionButtonId.Button41, 602, 553 },
        { 1024, 768, MainHudActionButtonId.Button40, 502, 721 },
        { 1024, 768, MainHudActionButtonId.Button410, 702, 746 },
        { 1024, 768, MainHudActionButtonId.Button42, 552, 721 },
        { 1024, 768, MainHudActionButtonId.Button43, 652, 746 },
        { 1024, 768, MainHudActionButtonId.Main3MissionBtn, 502, 746 },
        { 1024, 768, MainHudActionButtonId.Button45, 552, 746 },
        { 1024, 768, MainHudActionButtonId.Button46, 602, 746 },
        { 1024, 768, MainHudActionButtonId.Button47, 652, 721 },
        { 1024, 768, MainHudActionButtonId.Main3OrganiseBtn, 702, 721 },
        { 1024, 768, MainHudActionButtonId.Button41, 602, 721 },
    };

    [Theory]
    [MemberData(nameof(VerifiedBounds))]
    public void GetBounds_ReturnsVerifiedNativeControlBounds(
        int width, int height, MainHudActionButtonId id, int expectedX, int expectedY)
    {
        MainHudActionButtonLayout layout = MainHudActionButtonLayout.Create(new LogicalRenderSize(width, height));
        MainHudActionButtonBounds bounds = layout.GetBounds(id);

        Assert.Equal(expectedX, bounds.X);
        Assert.Equal(expectedY, bounds.Y);
        Assert.Equal(46, bounds.HitWidth);
        Assert.Equal(22, bounds.HitHeight);
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void Create_RejectsUnverifiedLogicalRenderSizes(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MainHudActionButtonLayout.Create(new LogicalRenderSize(width, height)));
    }

    [Fact]
    public void Bounds_UseRightAndBottomExclusiveHitTesting()
    {
        MainHudActionButtonBounds bounds = new(100, 200, 46, 22);

        Assert.True(bounds.Contains(100, 200));
        Assert.True(bounds.Contains(145, 221));
        Assert.False(bounds.Contains(146, 200));
        Assert.False(bounds.Contains(100, 222));
        Assert.False(bounds.Contains(99, 200));
        Assert.False(bounds.Contains(100, 199));
    }
}
