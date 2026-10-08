using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudStatusHintLayoutTests
{
    [Theory]
    [InlineData(800, 600, 459)]
    [InlineData(1024, 768, 627)]
    public void Create_PreservesVerifiedHudOrigin(int width, int height, int originY)
    {
        MainHudStatusHintLayout layout = MainHudStatusHintLayout.Create(new LogicalRenderSize(width, height));

        Assert.Equal(originY, layout.DialogOriginY);
        Assert.Equal(width, layout.LogicalWidth);
    }

    [Theory]
    [InlineData(800, 600, (int)MainHudStatusHintKind.WalkRun, 0, 462)]
    [InlineData(800, 600, (int)MainHudStatusHintKind.Map, 72, 462)]
    [InlineData(800, 600, (int)MainHudStatusHintKind.ScreenShift, 50, 450)]
    [InlineData(800, 600, (int)MainHudStatusHintKind.Equipment, 22, 450)]
    [InlineData(800, 600, (int)MainHudStatusHintKind.Skill, 0, 489)]
    [InlineData(800, 600, (int)MainHudStatusHintKind.Mana, 52, 493)]
    [InlineData(800, 600, (int)MainHudStatusHintKind.Life, 4, 493)]
    [InlineData(1024, 768, (int)MainHudStatusHintKind.WalkRun, 0, 630)]
    [InlineData(1024, 768, (int)MainHudStatusHintKind.Map, 72, 630)]
    [InlineData(1024, 768, (int)MainHudStatusHintKind.ScreenShift, 50, 618)]
    [InlineData(1024, 768, (int)MainHudStatusHintKind.Equipment, 22, 618)]
    [InlineData(1024, 768, (int)MainHudStatusHintKind.Skill, 0, 657)]
    [InlineData(1024, 768, (int)MainHudStatusHintKind.Mana, 52, 661)]
    [InlineData(1024, 768, (int)MainHudStatusHintKind.Life, 4, 661)]
    public void GetAnchor_MatchesVerifiedNativeBaseline(int width, int height, int kind, int x, int y)
    {
        MainHudStatusHintLayout layout = MainHudStatusHintLayout.Create(new LogicalRenderSize(width, height));

        Assert.Equal(new MainHudStatusHintAnchor(x, y), layout.GetAnchor((MainHudStatusHintKind)kind));
    }

    [Fact]
    public void GetRectangle_PreservesProgress44HotspotPaddingAndHiddenChatZeroRectangle()
    {
        MainHudStatusHintLayout.Create(new LogicalRenderSize(800, 600));

        Assert.Equal(new MainHudStatusHintRectangle(99, 95, 398, 12), MainHudStatusHintLayout.GetRectangle(0));
        Assert.Equal(new MainHudStatusHintRectangle(0, 0, 0, 0), MainHudStatusHintLayout.GetRectangle(2));
    }

    [Fact]
    public void GetChatControlRectangle_UsesNativeDialogUnitFormula()
    {
        Assert.Equal(
            new MainHudStatusHintRectangle(70, 32, 22, 22),
            MainHudStatusHintLayout.GetChatControlRectangle(8, 16));
    }

    [Fact]
    public void GetAnchor_DoesNotInventFontDependentChatCoordinates()
    {
        MainHudStatusHintLayout layout = MainHudStatusHintLayout.Create(new LogicalRenderSize(800, 600));

        Assert.Throws<InvalidOperationException>(() => layout.GetAnchor(MainHudStatusHintKind.Chat));
    }

    [Fact]
    public void GetRegionX_UsesExactNativeWidthMinusX()
    {
        MainHudStatusHintLayout layout = MainHudStatusHintLayout.Create(new LogicalRenderSize(800, 600));

        Assert.Equal(20, layout.GetRegionX(780, alternateLayout: true));
        Assert.Equal(780, layout.GetRegionX(780, alternateLayout: false));
        Assert.Equal(800, layout.GetRegionX(0, alternateLayout: true));
    }

    [Fact]
    public void Create_RejectsUnverifiedLogicalResolutions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MainHudStatusHintLayout.Create(new LogicalRenderSize(1280, 720)));
    }
}
