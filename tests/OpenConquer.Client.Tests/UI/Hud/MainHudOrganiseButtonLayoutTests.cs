using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudOrganiseButtonLayoutTests
{
    [Theory]
    [InlineData(800, 600, 702, 553, 46, 22)]
    [InlineData(1024, 768, 702, 721, 46, 22)]
    public void Create_ReturnsVerifiedNativeControlBounds(int width, int height, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        MainHudOrganiseButtonLayout layout = MainHudOrganiseButtonLayout.Create(new LogicalRenderSize(width, height));

        Assert.Equal(expectedX, layout.X);
        Assert.Equal(expectedY, layout.Y);
        Assert.Equal(expectedWidth, layout.HitWidth);
        Assert.Equal(expectedHeight, layout.HitHeight);
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void Create_RejectsUnverifiedLogicalRenderSizes(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudOrganiseButtonLayout.Create(new LogicalRenderSize(width, height)));
    }
}
