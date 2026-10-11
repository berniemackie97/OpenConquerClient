using OpenConquer.Client.UI.Hud.StatusHints.Equipment;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints.Equipment;

public sealed class MainHudEquipmentHintLayoutTests
{
    [Theory]
    [InlineData(800, 600, false, 180, 82, 534, 190, 28, 87, 539)]
    [InlineData(800, 600, false, 210, 82, 534, 220, 28, 87, 539)]
    [InlineData(800, 600, true, 180, 262, 534, 190, 28, 447, 539)]
    [InlineData(800, 600, true, 210, 292, 534, 220, 28, 507, 539)]
    [InlineData(1024, 768, false, 180, 82, 702, 190, 28, 87, 707)]
    [InlineData(1024, 768, true, 210, 292, 702, 220, 28, 507, 707)]
    public void Calculate_MatchesPinnedMetricVectors(int width, int height, bool alternate, int measuredWidth, int x, int y, int w, int h, int textX, int textY)
    {
        MainHudEquipmentHintGeometry actual = MainHudEquipmentHintLayout.Calculate(new LogicalRenderSize(width, height), 90, height - 1, measuredWidth, 12, alternate);
        Assert.Equal(new MainHudEquipmentHintGeometry(x, y, w, h, textX, textY), actual);
    }

    [Theory]
    [InlineData(800, 180, 615, 800)]
    [InlineData(800, 210, 585, 800)]
    [InlineData(1024, 180, 622, 807)]
    [InlineData(1024, 210, 652, 867)]
    public void Calculate_AlternateLastSlot_ClampsOnlyWhenWidthExceedsScreen(int width, int textWidth, int backdropX, int textX)
    {
        MainHudEquipmentHintGeometry result = MainHudEquipmentHintLayout.Calculate(new LogicalRenderSize(width, width == 800 ? 600 : 768), 450, width == 800 ? 599 : 767, textWidth, 12, true);
        Assert.Equal(backdropX, result.BackdropX);
        Assert.Equal(textX, result.TextX);
    }

    [Fact]
    public void EmptyLocalization_KeepsOneBackdropSizedFromFontHeight()
    {
        MainHudEquipmentHintGeometry result = MainHudEquipmentHintLayout.Calculate(new LogicalRenderSize(800, 600), 90, 599, 0, 12, false);
        Assert.Equal(10, result.BackdropWidth);
        Assert.Equal(28, result.BackdropHeight);
        Assert.Equal(87, result.TextX);
    }

    [Fact]
    public void OverflowBranch_DoesNotApplySecondLeftClamp()
    {
        MainHudEquipmentHintGeometry result = MainHudEquipmentHintLayout.Calculate(new LogicalRenderSize(800, 600), 90, 599, 1000, 12, false);
        Assert.Equal(-205, result.BackdropX);
        Assert.Equal(-200, result.TextX);
    }

    [Fact]
    public void NegativeAnchor_UsesTenPixelMinimumOnlyWhenNoOverflow()
    {
        MainHudEquipmentHintGeometry result = MainHudEquipmentHintLayout.Calculate(new LogicalRenderSize(800, 600), 0, 0, 100, 12, false);
        Assert.Equal(5, result.BackdropX);
        Assert.Equal(5, result.BackdropY);
        Assert.Equal(10, result.TextX);
        Assert.Equal(10, result.TextY);
    }

    [Theory]
    [InlineData(-1, 12)]
    [InlineData(1, 0)]
    public void Calculate_RejectsInvalidMetrics(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudEquipmentHintLayout.Calculate(new LogicalRenderSize(800, 600), 90, 599, width, height, false));
    }
}
