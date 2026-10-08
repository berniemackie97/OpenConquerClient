using OpenConquer.Client.UI.Hud.Quickbar;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudQuickbarSlotsStateTests
{
    private static readonly MainHudQuickbarSlotMetadata s_metadata = new(4, 7, 0xAABBCCDD, 1);

    [Fact]
    public void Constructor_MatchesNativeDefaults()
    {
        MainHudQuickbarSlotsState state = new();

        for (int index = 0; index < 256; index++)
        {
            MainHudQuickbarSlotSnapshot slot = state.GetSlot(index);
            Assert.False(slot.IsOccupied);
            Assert.Equal(3u, slot.Selector);
            Assert.Equal(0u, slot.AggregateQuantity);
            Assert.Equal(0, slot.ContentKind);
            Assert.Equal(0, slot.GlowKind);
            Assert.Equal(0u, slot.GlowStartTime);
            Assert.Equal(0u, slot.Payload);
            Assert.Equal(0u, slot.ContentId);
            Assert.Equal(0u, slot.ItemUid);
            Assert.Equal(new MainHudQuickbarSlotMetadata(0, 0, 1, 0), slot.Metadata);
        }

        Assert.Equal(0u, state.InsertionRemovalCounter);
    }

    [Fact]
    public void Populate_WritesDescriptorAndIncrementsCounter()
    {
        MainHudQuickbarSlotsState state = new();
        int index = state.Populate(2, 1, 1003, 45, 9, 3, 2, s_metadata);
        MainHudQuickbarSlotSnapshot slot = state.GetSlot(index);

        Assert.Equal(1, index);
        Assert.True(slot.IsOccupied);
        Assert.Equal(1003u, slot.ContentId);
        Assert.Equal(45u, slot.Payload);
        Assert.Equal(9u, slot.Selector);
        Assert.Equal(3, slot.ContentKind);
        Assert.Equal(2, slot.GlowKind);
        Assert.Equal(s_metadata, slot.Metadata);
        Assert.Equal(1u, state.InsertionRemovalCounter);
    }

    [Fact]
    public void Populate_OverwriteStillIncrementsCounter()
    {
        MainHudQuickbarSlotsState state = new();

        state.Populate(1, 1, 10, 20, 3, 2, 0, s_metadata);
        state.Populate(1, 1, 30, 40, 4, 5, 0, s_metadata);

        Assert.Equal(2u, state.InsertionRemovalCounter);
        Assert.Equal(30u, state.GetSlot(0).ContentId);
    }

    [Fact]
    public void ClearOccupied_ClearsOnlyOccupancyAndDecrementsCounter()
    {
        MainHudQuickbarSlotsState state = new();

        state.Populate(1, 1, 100, 200, 7, 5, 6, s_metadata);
        state.SetItemUid(0, 900);
        state.SetAggregateQuantity(0, 12);
        state.SetCoverFlag(0, 1);
        state.SetGlowStartTime(0, 1234);
        state.ClearOccupied(1, 1);

        MainHudQuickbarSlotSnapshot slot = state.GetSlot(0);

        Assert.False(slot.IsOccupied);
        Assert.Equal(100u, slot.ContentId);
        Assert.Equal(200u, slot.Payload);
        Assert.Equal(7u, slot.Selector);
        Assert.Equal(5, slot.ContentKind);
        Assert.Equal(6, slot.GlowKind);
        Assert.Equal(1234u, slot.GlowStartTime);
        Assert.Equal(900u, slot.ItemUid);
        Assert.Equal(12u, slot.AggregateQuantity);
        Assert.Equal(1, slot.CoverFlag);
        Assert.Equal(s_metadata, slot.Metadata);
        Assert.Equal(0u, state.InsertionRemovalCounter);
    }

    [Fact]
    public void ZeroGlowPopulate_PreservesOldGlowStartTime()
    {
        MainHudQuickbarSlotsState state = new();
        state.SetGlowStartTime(0, 500);
        state.Populate(1, 1, 1, 0, 3, 1, 0, s_metadata);
        Assert.Equal(500u, state.GetSlot(0).GlowStartTime);
    }

    [Fact]
    public void NonzeroGlowPopulate_ResetsGlowStartTime()
    {
        MainHudQuickbarSlotsState state = new();
        state.SetGlowStartTime(0, 500);
        state.Populate(1, 1, 1, 0, 3, 1, 2, s_metadata);
        Assert.Equal(0u, state.GetSlot(0).GlowStartTime);
    }

    [Fact]
    public void ClearAllOccupied_PreservesDescriptors()
    {
        MainHudQuickbarSlotsState state = new();

        state.Populate(1, 1, 100, 200, 3, 1, 0, s_metadata);
        state.Populate(2, 1, 300, 400, 3, 3, 0, s_metadata);
        state.ClearAllOccupied();

        Assert.False(state.GetSlot(0).IsOccupied);
        Assert.False(state.GetSlot(1).IsOccupied);
        Assert.Equal(100u, state.GetSlot(0).ContentId);
        Assert.Equal(300u, state.GetSlot(1).ContentId);
        Assert.Equal(0u, state.InsertionRemovalCounter);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(11, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 2)]
    public void Populate_RejectsCoordinatesOutsideConfiguredVisibleGrid(int column, int row)
    {
        MainHudQuickbarSlotsState state = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Populate(column, row, 1, 0, 3, 1, 0, s_metadata));
    }
}
