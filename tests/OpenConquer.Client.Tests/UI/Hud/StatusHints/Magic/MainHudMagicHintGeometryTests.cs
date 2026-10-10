using OpenConquer.Client.UI.Hud.StatusHints.Magic;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints.Magic;

public sealed class MainHudMagicHintGeometryTests
{
    [Fact]
    public void Learned_UsesNativePanelPaddingAndBottomAnchoringAt800x600()
    {
        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Learned(logicalWidth: 800, anchorX: 90, storedAnchorY: 599, maximumTextWidth: 108, wrappedLineCount: 3, alternateTextAnchor: false);

        Assert.Equal(new MainHudMagicHintGeometry(90, 511, 95, 516, 118, 46), geometry);
    }

    [Fact]
    public void Learned_RightOverflowUsesNativeRightEdgeRule()
    {
        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Learned(logicalWidth: 800, anchorX: 750, storedAnchorY: 599, maximumTextWidth: 108, wrappedLineCount: 3, alternateTextAnchor: false);

        Assert.Equal(new MainHudMagicHintGeometry(680, 511, 685, 516, 118, 46), geometry);
    }

    [Fact]
    public void Learned_AlternateTextAnchorUsesRightInset()
    {
        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Learned(logicalWidth: 1024, anchorX: 1000, storedAnchorY: 767, maximumTextWidth: 108, wrappedLineCount: 3, alternateTextAnchor: true);

        Assert.Equal(new MainHudMagicHintGeometry(904, 679, 1017, 684, 118, 46), geometry);
    }

    [Fact]
    public void Learned_ClampsTopToZeroWithoutShiftingHorizontalAnchor()
    {
        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Learned(logicalWidth: 800, anchorX: 90, storedAnchorY: 42, maximumTextWidth: 100, wrappedLineCount: 50, alternateTextAnchor: false);

        Assert.Equal(0, geometry.BackdropY);
        Assert.Equal(5, geometry.TextY);
        Assert.Equal(90, geometry.BackdropX);
        Assert.Equal(610, geometry.Height);
    }

    [Fact]
    public void Revive_UsesSeparateNormalAndAlternateTextAnchors()
    {
        MainHudMagicHintGeometry normal = MainHudMagicHintLayout.Revive(800, 90, 599, 100, 20, false);
        MainHudMagicHintGeometry alternate = MainHudMagicHintLayout.Revive(800, 90, 599, 100, 20, true);

        Assert.Equal(new MainHudMagicHintGeometry(87, 539, 87, 539, 100, 20), normal);
        Assert.Equal(new MainHudMagicHintGeometry(87, 539, 187, 539, 100, 20), alternate);
    }

    [Fact]
    public void Revive_UsesOverflowSpecificAnchor()
    {
        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Revive(800, 750, 599, 100, 20, true);

        Assert.Equal(new MainHudMagicHintGeometry(697, 539, 797, 539, 100, 20), geometry);
    }

    [Fact]
    public void Simple_UsesRestorationBackgroundOffsetWithoutMovingText()
    {
        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Simple(90, 599, 100, 20, -4, false, false);

        Assert.Equal(new MainHudMagicHintGeometry(86, 539, 90, 539, 100, 20), geometry);
    }

    [Fact]
    public void Tryout_AppliesConfiguredOffsetsAfterNativeOverflowDecision()
    {
        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Tryout(800, 750, 599, 100, 20, -5, -75, false);

        Assert.Equal(new MainHudMagicHintGeometry(695, 464, 695, 464, 100, 20), geometry);
    }

    [Fact]
    public void Tryout_DoesNotClampAlternateAnchor()
    {
        MainHudMagicHintGeometry geometry = MainHudMagicHintLayout.Tryout(800, 750, 599, 100, 20, -5, -75, true);

        Assert.Equal(new MainHudMagicHintGeometry(745, 464, 845, 464, 100, 20), geometry);
    }
}
