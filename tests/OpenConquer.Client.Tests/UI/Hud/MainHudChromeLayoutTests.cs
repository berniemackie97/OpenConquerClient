using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudChromeLayoutTests
{
    [Theory]
    [InlineData(800, 600, 459, 456, 547)]
    [InlineData(1024, 768, 627, 624, 715)]
    public void Create_ReturnsVerifiedNativeCoordinates(int width, int height, int originY, int panelAY, int lowerPanelY)
    {
        MainHudChromeLayout layout = MainHudChromeLayout.Create(new LogicalRenderSize(width, height));

        Assert.Equal(originY, layout.OriginY);
        Assert.Equal(originY, layout.BackgroundY);
        Assert.Equal(panelAY, layout.DialogPanelAY);
        Assert.Equal(lowerPanelY, layout.DialogPanelBY);
        Assert.Equal(lowerPanelY, layout.DialogPanelCY);
        Assert.Equal(lowerPanelY, layout.DialogPanelDY);

        Assert.Equal(0, MainHudChromeLayout.BackgroundX);
        Assert.Equal(0, MainHudChromeLayout.DialogPanelAX);
        Assert.Equal(256, MainHudChromeLayout.DialogPanelBX);
        Assert.Equal(512, MainHudChromeLayout.DialogPanelCX);
        Assert.Equal(768, MainHudChromeLayout.DialogPanelDX);
    }

    [Fact]
    public void SourceRectangles_MatchVerifiedNativeSlices()
    {
        Assert.Equal(new SpriteSourceRectangle(0, 112, 256, 144), MainHudChromeLayout.DialogPanelASource);
        Assert.Equal(new SpriteSourceRectangle(0, 0, 256, 54), MainHudChromeLayout.DialogPanelBSource);
        Assert.Equal(new SpriteSourceRectangle(0, 0, 256, 54), MainHudChromeLayout.DialogPanelCSource);
        Assert.Equal(new SpriteSourceRectangle(0, 64, 256, 54), MainHudChromeLayout.DialogPanelDSource);
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void Create_RejectsUnverifiedLogicalRenderSizes(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudChromeLayout.Create(new LogicalRenderSize(width, height)));
    }
}
