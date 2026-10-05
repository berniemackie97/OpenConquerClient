using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudQuickbarLayoutTests
{
    [Theory]
    [InlineData(800, 600, 90, 557)]
    [InlineData(1024, 768, 90, 725)]
    public void GetBounds_MatchesVerifiedNativeRectangle(int width, int height, int expectedX, int expectedY)
    {
        MainHudQuickbarBounds bounds = MainHudQuickbarLayout.Create(new LogicalRenderSize(width, height)).GetBounds();
        Assert.Equal(expectedX, bounds.X);
        Assert.Equal(expectedY, bounds.Y);
    }

    [Theory]
    [InlineData(800, 600, 0, 90, 557)]
    [InlineData(800, 600, 1, 131, 557)]
    [InlineData(800, 600, 9, 459, 557)]
    [InlineData(1024, 768, 0, 90, 725)]
    [InlineData(1024, 768, 1, 131, 725)]
    [InlineData(1024, 768, 9, 459, 725)]
    public void GetSlotBounds_UsesVerifiedNativeStride(int width, int height, int slotIndex, int expectedX, int expectedY)
    {
        MainHudQuickbarSlotBounds bounds = MainHudQuickbarLayout.Create(new LogicalRenderSize(width, height)).GetSlotBounds(slotIndex);
        Assert.Equal(expectedX, bounds.X);
        Assert.Equal(expectedY, bounds.Y);
    }

    [Fact]
    public void SlotInputInterval_IncludesTheOnePixelGapAfterTheRenderedCell()
    {
        MainHudQuickbarLayout layout = MainHudQuickbarLayout.Create(new LogicalRenderSize(800, 600));
        MainHudQuickbarSlotBounds first = layout.GetSlotBounds(0);
        MainHudQuickbarSlotBounds second = layout.GetSlotBounds(1);

        Assert.True(first.ContainsInputPoint(90, 557));
        Assert.True(first.ContainsInputPoint(129, 596));
        Assert.True(first.ContainsInputPoint(130, 557));
        Assert.False(first.ContainsInputPoint(131, 557));
        Assert.True(second.ContainsInputPoint(131, 557));
    }

    [Theory]
    [InlineData(90, 557, 0)]
    [InlineData(129, 557, 0)]
    [InlineData(130, 557, 0)]
    [InlineData(131, 557, 1)]
    [InlineData(171, 557, 1)]
    [InlineData(172, 557, 2)]
    [InlineData(458, 557, 8)]
    [InlineData(459, 557, 9)]
    [InlineData(499, 557, 9)]
    public void TryGetSlotIndex_UsesVerifiedFortyOnePixelIntervals(int x, int y, int expectedSlotIndex)
    {
        MainHudQuickbarLayout layout = MainHudQuickbarLayout.Create(new LogicalRenderSize(800, 600));

        Assert.True(layout.TryGetSlotIndex(x, y, out int slotIndex));
        Assert.Equal(expectedSlotIndex, slotIndex);
    }

    [Theory]
    [InlineData(89, 557)]
    [InlineData(90, 556)]
    [InlineData(500, 557)]
    [InlineData(90, 597)]
    [InlineData(-1, 557)]
    [InlineData(90, -1)]
    public void TryGetSlotIndex_RejectsCoordinatesOutsideTheGrid(int x, int y)
    {
        MainHudQuickbarLayout layout = MainHudQuickbarLayout.Create(new LogicalRenderSize(800, 600));

        Assert.False(layout.TryGetSlotIndex(x, y, out int slotIndex));
        Assert.Equal(-1, slotIndex);
    }

    [Fact]
    public void GetBounds_UsesHalfOpenOuterRectangle()
    {
        MainHudQuickbarBounds bounds = MainHudQuickbarLayout.Create(new LogicalRenderSize(800, 600)).GetBounds();

        Assert.True(bounds.Contains(90, 557));
        Assert.True(bounds.Contains(499, 596));
        Assert.False(bounds.Contains(89, 557));
        Assert.False(bounds.Contains(500, 557));
        Assert.False(bounds.Contains(90, 556));
        Assert.False(bounds.Contains(90, 597));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void GetSlotBounds_RejectsInvalidSlotIndex(int slotIndex)
    {
        MainHudQuickbarLayout layout = MainHudQuickbarLayout.Create(new LogicalRenderSize(800, 600));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.GetSlotBounds(slotIndex));
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void Create_RejectsUnverifiedLogicalRenderSize(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MainHudQuickbarLayout.Create(new LogicalRenderSize(width, height)));
}
