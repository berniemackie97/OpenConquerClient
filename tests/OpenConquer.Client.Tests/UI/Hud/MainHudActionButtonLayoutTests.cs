using OpenConquer.Client.UI.Hud.ActionButtons;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudActionButtonLayoutTests
{
    public static TheoryData<int, int, int, int, int> VerifiedBounds => new()
    {
        { 800, 600, 0x5DF, 502, 553 },
        { 800, 600, 0x3EE, 702, 578 },
        { 800, 600, 0x3EF, 552, 553 },
        { 800, 600, 0x3F0, 652, 578 },
        { 800, 600, 0x3F1, 502, 578 },
        { 800, 600, 0x3F2, 552, 578 },
        { 800, 600, 0x3F3, 602, 578 },
        { 800, 600, 0x3F5, 652, 553 },
        { 800, 600, 0x400, 702, 553 },
        { 800, 600, 0x402, 602, 553 },
        { 1024, 768, 0x5DF, 502, 721 },
        { 1024, 768, 0x3EE, 702, 746 },
        { 1024, 768, 0x3EF, 552, 721 },
        { 1024, 768, 0x3F0, 652, 746 },
        { 1024, 768, 0x3F1, 502, 746 },
        { 1024, 768, 0x3F2, 552, 746 },
        { 1024, 768, 0x3F3, 602, 746 },
        { 1024, 768, 0x3F5, 652, 721 },
        { 1024, 768, 0x400, 702, 721 },
        { 1024, 768, 0x402, 602, 721 },
    };

    [Theory]
    [MemberData(nameof(VerifiedBounds))]
    public void GetBounds_ReturnsVerifiedNativeControlBounds(int width, int height, int controlId, int expectedX, int expectedY)
    {
        MainHudActionButtonLayout layout = MainHudActionButtonLayout.Create(new LogicalRenderSize(width, height));
        MainHudActionButtonBounds bounds = layout.GetBounds((MainHudActionButtonId)controlId);

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
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudActionButtonLayout.Create(new LogicalRenderSize(width, height)));
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
