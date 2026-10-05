using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudQuickbarInputTests
{
    private static readonly MainHudQuickbarLayout s_layout = MainHudQuickbarLayout.Create(new LogicalRenderSize(800, 600));
    private static readonly MainHudQuickbarSlotMetadata s_metadata = new(0, 0, 1, 0);

    [Fact]
    public void RightDown_OnOccupiedUnblockedSlotReturnsActivation()
    {
        MainHudQuickbarState state = CreateState(MainHudQuickbarContentKind.Magic);
        MainHudQuickbarInput input = new(state, s_layout);

        Assert.True(input.HandleRightButtonDown(90, 557, out MainHudQuickbarActivation? activation));
        Assert.NotNull(activation);
        Assert.Equal(0, activation.Value.SlotIndex);
        Assert.Equal((byte)MainHudQuickbarContentKind.Magic, activation.Value.ContentKind);
        Assert.Equal(123u, activation.Value.ContentId);
        Assert.Equal(456u, activation.Value.Payload);
        Assert.Equal(789u, activation.Value.ItemUid);
        Assert.Equal(3u, activation.Value.Selector);
    }

    [Fact]
    public void RightDown_IsBlockedByExplicitCoverFlag()
    {
        MainHudQuickbarState state = CreateState(MainHudQuickbarContentKind.Item);
        state.Slots.SetCoverFlag(0, 1);

        Assert.False(new MainHudQuickbarInput(state, s_layout).HandleRightButtonDown(90, 557, out MainHudQuickbarActivation? activation));
        Assert.Null(activation);
    }

    [Fact]
    public void RightDown_IsBlockedWhenInteractionDisabled()
    {
        MainHudQuickbarState state = CreateState(MainHudQuickbarContentKind.Item);
        state.SetInteractionEnabled(false);

        Assert.False(new MainHudQuickbarInput(state, s_layout).HandleRightButtonDown(90, 557, out _));
    }

    [Fact]
    public void PointerMoveOutsideGridPreservesHover()
    {
        MainHudQuickbarState state = CreateState(MainHudQuickbarContentKind.Magic);
        MainHudQuickbarInput input = new(state, s_layout);

        input.HandlePointerMoved(90, 557, out _);

        Assert.False(input.HandlePointerMoved(0, 0, out _));
        Assert.True(state.IsHoverActive);
    }

    [Fact]
    public void PollPointerOutsideGridClearsHover()
    {
        MainHudQuickbarState state = CreateState(MainHudQuickbarContentKind.Magic);
        MainHudQuickbarInput input = new(state, s_layout);

        input.HandlePointerMoved(90, 557, out _);

        Assert.True(input.PollPointer(0, 0, out MainHudQuickbarHoverNotification notification));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.Clear, notification.Kind);
        Assert.False(state.IsHoverActive);
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    [InlineData(5, 4)]
    [InlineData(6, 5)]
    public void PointerMove_NewOccupiedSlotReturnsNativeNotificationKind(int contentKindValue, int expectedKindValue)
    {
        MainHudQuickbarContentKind contentKind = (MainHudQuickbarContentKind)contentKindValue;
        MainHudQuickbarHoverNotificationKind expectedKind = (MainHudQuickbarHoverNotificationKind)expectedKindValue;
        MainHudQuickbarState state = CreateState(contentKind);
        MainHudQuickbarInput input = new(state, s_layout);

        Assert.True(input.HandlePointerMoved(90, 557, out MainHudQuickbarHoverNotification notification));
        Assert.Equal(expectedKind, notification.Kind);
        Assert.Equal(123u, notification.ContentId);
        Assert.Equal(90, notification.AnchorX);
        Assert.Equal(557, notification.AnchorY);
    }

    [Fact]
    public void ItemHoverReturnsItemNotificationWithInstanceUid()
    {
        MainHudQuickbarState state = CreateState(MainHudQuickbarContentKind.Item);
        MainHudQuickbarInput input = new(state, s_layout);

        Assert.True(input.HandlePointerMoved(90, 557, out MainHudQuickbarHoverNotification notification));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.Item, notification.Kind);
        Assert.Equal(789u, notification.ItemUid);
    }

    [Fact]
    public void ActionHoverUpdatesCoordinatesWithoutCreatingHoverState()
    {
        MainHudQuickbarState state = CreateState(MainHudQuickbarContentKind.Action);
        MainHudQuickbarInput input = new(state, s_layout);

        Assert.True(input.HandlePointerMoved(90, 557, out MainHudQuickbarHoverNotification notification));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.None, notification.Kind);
        Assert.False(state.IsHoverActive);
        Assert.Equal(1, state.HoveredColumnOneBased);
        Assert.Equal(1, state.HoveredRowOneBased);
    }

    [Fact]
    public void ActionHoverPreservesExistingHoverCacheWhileUpdatingCoordinates()
    {
        MainHudQuickbarState state = CreateState(MainHudQuickbarContentKind.Magic);
        state.Slots.Populate(2, 1, 999, 888, 3, (byte)MainHudQuickbarContentKind.Action, 0, s_metadata);
        MainHudQuickbarInput input = new(state, s_layout);

        input.HandlePointerMoved(90, 557, out _);
        input.HandlePointerMoved(131, 557, out _);

        Assert.True(state.IsHoverActive);
        Assert.Equal(2, state.HoveredColumnOneBased);
        Assert.Equal(1, state.HoveredRowOneBased);
        Assert.Equal(456u, state.HoveredPayload);
        Assert.Equal(123u, state.HoveredContentId);
    }

    [Fact]
    public void EmptySlotClearsExistingHover()
    {
        MainHudQuickbarState state = CreateState(MainHudQuickbarContentKind.Magic);
        MainHudQuickbarInput input = new(state, s_layout);

        input.HandlePointerMoved(90, 557, out _);
        state.Slots.ClearOccupied(1, 1);

        Assert.True(input.HandlePointerMoved(90, 557, out MainHudQuickbarHoverNotification notification));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.Clear, notification.Kind);
        Assert.False(state.IsHoverActive);
    }

    [Fact]
    public void GenericContentKindUsesGridContext()
    {
        MainHudQuickbarState state = new();
        state.Slots.Populate(1, 1, 123, 456, 3, 7, 0, s_metadata);
        MainHudQuickbarInput input = new(state, s_layout);

        Assert.True(input.HandlePointerMoved(90, 557, out MainHudQuickbarHoverNotification notification));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.Generic, notification.Kind);
        Assert.Equal(3u, notification.Context);
    }

    [Fact]
    public void PickupRequestDoesNotRemoveSlotUntilDragStartSucceeds()
    {
        MainHudQuickbarState state = CreateState(MainHudQuickbarContentKind.Item);
        MainHudQuickbarInput input = new(state, s_layout);

        Assert.True(input.TryCreatePickupRequest(90, 557, out MainHudQuickbarPickupRequest? request));
        Assert.True(state.Slots.GetSlot(0).IsOccupied);

        input.CompletePickup(request!.Value, dragStarted: false);
        Assert.True(state.Slots.GetSlot(0).IsOccupied);

        input.CompletePickup(request.Value, dragStarted: true);
        Assert.False(state.Slots.GetSlot(0).IsOccupied);
    }

    [Fact]
    public void HoverNotificationAnchorOmitsNativeOnePixelSlotSpacing()
    {
        MainHudQuickbarState state = new();
        state.Slots.Populate(2, 1, 123, 456, 3, (byte)MainHudQuickbarContentKind.Magic, 0, s_metadata);
        MainHudQuickbarInput input = new(state, s_layout);

        Assert.True(input.HandlePointerMoved(131, 557, out MainHudQuickbarHoverNotification notification));
        Assert.Equal(1, notification.SlotIndex);
        Assert.Equal(130, notification.AnchorX);
        Assert.Equal(557, notification.AnchorY);
    }

    private static MainHudQuickbarState CreateState(MainHudQuickbarContentKind kind)
    {
        MainHudQuickbarState state = new();
        state.Slots.Populate(1, 1, 123, 456, 3, (byte)kind, 0, s_metadata);
        state.Slots.SetItemUid(0, 789);
        return state;
    }
}
