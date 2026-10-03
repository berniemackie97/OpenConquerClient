using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudCheckControlLayoutTests
{
    public static TheoryData<int, int, int, int, int> VerifiedBounds => new()
    {
        { 800, 600, 0x3F4, 0, 482 },
        { 800, 600, 0x3F7, 72, 482 },
        { 800, 600, 0x3FF, 50, 470 },
        { 800, 600, 0x3F8, 22, 470 },
        { 1024, 768, 0x3F4, 0, 650 },
        { 1024, 768, 0x3F7, 72, 650 },
        { 1024, 768, 0x3FF, 50, 638 },
        { 1024, 768, 0x3F8, 22, 638 },
    };

    [Theory]
    [MemberData(nameof(VerifiedBounds))]
    public void GetBounds_ReturnsVerifiedNativeControlBounds(int width, int height, int controlId, int expectedX, int expectedY)
    {
        MainHudCheckControlLayout layout = MainHudCheckControlLayout.Create(new LogicalRenderSize(width, height));
        MainHudCheckControlBounds bounds = layout.GetBounds((MainHudCheckControlId)controlId);

        Assert.Equal(expectedX, bounds.X);
        Assert.Equal(expectedY, bounds.Y);
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void Create_RejectsUnverifiedLogicalRenderSizes(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudCheckControlLayout.Create(new LogicalRenderSize(width, height)));
    }

    [Fact]
    public void Bounds_UseNativeTwentyTwoPixelHalfOpenHitRectangle()
    {
        MainHudCheckControlBounds bounds = new(100, 200);

        Assert.True(bounds.Contains(100, 200));
        Assert.True(bounds.Contains(121, 221));
        Assert.False(bounds.Contains(122, 200));
        Assert.False(bounds.Contains(100, 222));
        Assert.False(bounds.Contains(99, 200));
        Assert.False(bounds.Contains(100, 199));
    }
}
