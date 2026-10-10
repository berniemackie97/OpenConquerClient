using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints.Magic;

public sealed class MainHudMagicHintQuickbarTransitionTests
{
    private static readonly MainHudQuickbarSlotMetadata s_metadata = new(0, 0, 1, 0);

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void MagicAndXpMagic_SelectCategoryNineWithFullTypeAndNativeAnchors(int width, int height)
    {
        MainHudQuickbarState quickbar = new();
        MainHudQuickbarLayout layout = MainHudQuickbarLayout.Create(new LogicalRenderSize(width, height));
        MainHudQuickbarInput input = new(quickbar, layout);

        quickbar.Slots.Populate(1, 1, 65537, 0, 3, (byte)MainHudQuickbarContentKind.Magic, 0, s_metadata);
        quickbar.Slots.Populate(10, 1, uint.MaxValue, 0, 3, (byte)MainHudQuickbarContentKind.XpMagic, 0, s_metadata);

        MainHudQuickbarSlotBounds first = layout.GetSlotBounds(0);
        MainHudQuickbarSlotBounds last = layout.GetSlotBounds(9);

        Assert.True(input.HandlePointerMoved(first.X, first.Y, out MainHudQuickbarHoverNotification magic));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.Skill, magic.Kind);
        Assert.Equal(65537u, magic.ContentId);
        Assert.Equal(90, magic.AnchorX);
        Assert.Equal(height - 43, magic.AnchorY);

        Assert.True(input.HandlePointerMoved(last.X, last.Y, out MainHudQuickbarHoverNotification xp));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.Skill, xp.Kind);
        Assert.Equal(uint.MaxValue, xp.ContentId);
        Assert.Equal(450, xp.AnchorX);
        Assert.Equal(height - 43, xp.AnchorY);

        MainHudStatusHintState status = new();
        status.SetMagicAnchor(xp.AnchorX, xp.AnchorY);
        status.SelectMagic(xp.ContentId);

        Assert.True(status.CanRenderMagic);
        Assert.Equal(uint.MaxValue, status.MagicType);
        Assert.Equal(height - 1, status.MagicStoredAnchorY);
    }

    [Fact]
    public void MovingFromMagicToAction_DoesNotClearNativeCategoryNineSelection()
    {
        MainHudQuickbarState quickbar = new();
        MainHudQuickbarLayout layout = MainHudQuickbarLayout.Create(new LogicalRenderSize(800, 600));
        MainHudQuickbarInput input = new(quickbar, layout);
        MainHudStatusHintState status = new();

        quickbar.Slots.Populate(1, 1, 1000, 0, 3, (byte)MainHudQuickbarContentKind.Magic, 0, s_metadata);
        quickbar.Slots.Populate(2, 1, 1, 0, 3, (byte)MainHudQuickbarContentKind.Action, 0, s_metadata);

        MainHudQuickbarSlotBounds magicSlot = layout.GetSlotBounds(0);
        MainHudQuickbarSlotBounds actionSlot = layout.GetSlotBounds(1);

        Assert.True(input.HandlePointerMoved(magicSlot.X, magicSlot.Y, out MainHudQuickbarHoverNotification magic));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.Skill, magic.Kind);

        status.SetMagicAnchor(magic.AnchorX, magic.AnchorY);
        status.SelectMagic(magic.ContentId);

        Assert.True(input.HandlePointerMoved(actionSlot.X, actionSlot.Y, out MainHudQuickbarHoverNotification action));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.None, action.Kind);
        Assert.True(quickbar.IsHoverActive);
        Assert.Equal(2, quickbar.HoveredColumnOneBased);

        Assert.True(status.CanRenderMagic);
        Assert.Equal(1000u, status.MagicType);
        Assert.Equal(90, status.MagicAnchorX);
    }

    [Fact]
    public void MovingFromMagicToEmptySlot_EmitsNativeClear()
    {
        MainHudQuickbarState quickbar = new();
        MainHudQuickbarLayout layout = MainHudQuickbarLayout.Create(new LogicalRenderSize(800, 600));
        MainHudQuickbarInput input = new(quickbar, layout);

        quickbar.Slots.Populate(1, 1, 1000, 0, 3, (byte)MainHudQuickbarContentKind.Magic, 0, s_metadata);

        MainHudQuickbarSlotBounds occupied = layout.GetSlotBounds(0);
        MainHudQuickbarSlotBounds empty = layout.GetSlotBounds(1);

        Assert.True(input.HandlePointerMoved(occupied.X, occupied.Y, out _));
        Assert.True(input.HandlePointerMoved(empty.X, empty.Y, out MainHudQuickbarHoverNotification notification));

        Assert.Equal(MainHudQuickbarHoverNotificationKind.Clear, notification.Kind);
        Assert.False(quickbar.IsHoverActive);
    }

    [Fact]
    public void LeavingQuickbar_ClearsDuringFramePollNotPointerMove()
    {
        MainHudQuickbarState quickbar = new();
        MainHudQuickbarLayout layout = MainHudQuickbarLayout.Create(new LogicalRenderSize(800, 600));
        MainHudQuickbarInput input = new(quickbar, layout);

        quickbar.Slots.Populate(1, 1, 1000, 0, 3, (byte)MainHudQuickbarContentKind.Magic, 0, s_metadata);

        MainHudQuickbarSlotBounds occupied = layout.GetSlotBounds(0);

        Assert.True(input.HandlePointerMoved(occupied.X, occupied.Y, out _));
        Assert.False(input.HandlePointerMoved(0, 0, out MainHudQuickbarHoverNotification moved));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.None, moved.Kind);
        Assert.True(quickbar.IsHoverActive);

        Assert.True(input.PollPointer(0, 0, out MainHudQuickbarHoverNotification polled));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.Clear, polled.Kind);
        Assert.False(quickbar.IsHoverActive);
    }

    [Fact]
    public void SameOccupiedCoordinates_DoNotEmitNewTypeAfterSlotReplacement()
    {
        MainHudQuickbarState quickbar = new();
        MainHudQuickbarLayout layout = MainHudQuickbarLayout.Create(new LogicalRenderSize(800, 600));
        MainHudQuickbarInput input = new(quickbar, layout);

        quickbar.Slots.Populate(1, 1, 1000, 0, 3, (byte)MainHudQuickbarContentKind.Magic, 0, s_metadata);

        MainHudQuickbarSlotBounds slot = layout.GetSlotBounds(0);

        Assert.True(input.HandlePointerMoved(slot.X, slot.Y, out MainHudQuickbarHoverNotification initial));
        Assert.Equal(1000u, initial.ContentId);

        quickbar.Slots.Populate(1, 1, 2000, 0, 3, (byte)MainHudQuickbarContentKind.XpMagic, 0, s_metadata);

        Assert.True(input.HandlePointerMoved(slot.X, slot.Y, out MainHudQuickbarHoverNotification repeated));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.None, repeated.Kind);
        Assert.Equal(1000u, quickbar.HoveredContentId);
    }
}
