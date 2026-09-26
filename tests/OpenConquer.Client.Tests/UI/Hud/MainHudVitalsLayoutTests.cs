using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudVitalsLayoutTests
{
    [Theory]
    [InlineData(800, 600, 459, 513, 513, 517, 511)]
    [InlineData(1024, 768, 627, 681, 681, 685, 679)]
    public void Create_ReturnsVerifiedNativeCoordinates(int width, int height, int originY, int lifeY, int manaY, int staminaY, int extendedStaminaY)
    {
        MainHudVitalsLayout layout = MainHudVitalsLayout.Create(new LogicalRenderSize(width, height));

        Assert.Equal(originY, layout.OriginY);
        Assert.Equal(lifeY, layout.LifeY);
        Assert.Equal(manaY, layout.ManaY);
        Assert.Equal(staminaY, layout.StaminaY);
        Assert.Equal(extendedStaminaY, layout.ExtendedStaminaY);
        Assert.Equal(4, MainHudVitalsLayout.LifeX);
        Assert.Equal(52, MainHudVitalsLayout.ManaX);
        Assert.Equal(42, MainHudVitalsLayout.StaminaX);
        Assert.Equal(42, MainHudVitalsLayout.ExtendedStaminaX);
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void Create_RejectsUnverifiedLogicalRenderSizes(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudVitalsLayout.Create(new LogicalRenderSize(width, height)));
    }
}
