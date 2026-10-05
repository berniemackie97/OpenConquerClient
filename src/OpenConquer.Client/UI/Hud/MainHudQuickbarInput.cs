namespace OpenConquer.Client.UI.Hud;

internal enum MainHudQuickbarHoverNotificationKind
{
    None,
    Clear,
    Item,
    Skill,
    Dance,
    WeaponSwap,
    Generic,
}

internal readonly record struct MainHudQuickbarHoverNotification(MainHudQuickbarHoverNotificationKind Kind, int SlotIndex, uint ContentId, uint ItemUid, int AnchorX, int AnchorY, uint Context);

internal readonly record struct MainHudQuickbarActivation(int SlotIndex, byte ContentKind, uint ContentId, uint Payload, uint ItemUid, uint Selector);

internal readonly record struct MainHudQuickbarPickupRequest(int SlotIndex, int ColumnOneBased, int RowOneBased, MainHudQuickbarSlotSnapshot Slot);

internal sealed class MainHudQuickbarInput
{
    private readonly MainHudQuickbarState _state;
    private readonly MainHudQuickbarLayout _layout;

    public MainHudQuickbarInput(MainHudQuickbarState state, MainHudQuickbarLayout layout)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        _layout = layout;
    }

    public bool HandlePointerMoved(int logicalX, int logicalY, out MainHudQuickbarHoverNotification notification)
    {
        notification = default;

        if (!_layout.TryGetSlotIndex(logicalX, logicalY, out int slotIndex))
        {
            return false;
        }

        MainHudQuickbarSlotSnapshot slot = _state.Slots.GetSlot(slotIndex);
        int columnOneBased = slotIndex + 1;

        if (!slot.IsOccupied)
        {
            if (_state.IsHoverActive)
            {
                _state.ClearHover();
                notification = new(MainHudQuickbarHoverNotificationKind.Clear, slotIndex, 0, 0, 0, 0, 0);
            }

            return true;
        }

        if (slot.ContentKind == (byte)MainHudQuickbarContentKind.Action)
        {
            _state.SetHoveredCoordinates(columnOneBased, 1);
            return true;
        }

        if (_state.IsHoverActive && _state.HoveredColumnOneBased == columnOneBased && _state.HoveredRowOneBased == 1)
        {
            return true;
        }

        _state.SetHover(columnOneBased, 1, slot.Payload, slot.ContentId);

        MainHudQuickbarBounds bounds = _layout.GetBounds();
        int anchorX = bounds.X + MainHudQuickbarDefinition.CellWidth * slotIndex;
        int anchorY = bounds.Y;

        MainHudQuickbarHoverNotificationKind kind = slot.ContentKind switch
        {
            (byte)MainHudQuickbarContentKind.Item => MainHudQuickbarHoverNotificationKind.Item,
            (byte)MainHudQuickbarContentKind.Magic or (byte)MainHudQuickbarContentKind.XpMagic => MainHudQuickbarHoverNotificationKind.Skill,
            (byte)MainHudQuickbarContentKind.Dance => MainHudQuickbarHoverNotificationKind.Dance,
            (byte)MainHudQuickbarContentKind.WeaponSwap => MainHudQuickbarHoverNotificationKind.WeaponSwap,
            _ => MainHudQuickbarHoverNotificationKind.Generic,
        };

        uint context = kind switch
        {
            MainHudQuickbarHoverNotificationKind.Dance => MainHudQuickbarDefinition.NativeContext,
            MainHudQuickbarHoverNotificationKind.WeaponSwap => 0x5E,
            MainHudQuickbarHoverNotificationKind.Generic => MainHudQuickbarDefinition.NativeContext,
            _ => 0,
        };

        notification = new(kind, slotIndex, slot.ContentId, slot.ItemUid, anchorX, anchorY, context);
        return true;
    }

    public bool PollPointer(int logicalX, int logicalY, out MainHudQuickbarHoverNotification notification)
    {
        notification = default;

        if (_layout.GetBounds().Contains(logicalX, logicalY) || !_state.IsHoverActive)
        {
            return false;
        }

        _state.ClearHover();
        notification = new(MainHudQuickbarHoverNotificationKind.Clear, -1, 0, 0, 0, 0, 0);
        return true;
    }

    public bool HandleRightButtonDown(int logicalX, int logicalY, out MainHudQuickbarActivation? activation)
    {
        activation = null;

        if (!_state.InteractionEnabled || !_layout.TryGetSlotIndex(logicalX, logicalY, out int slotIndex))
        {
            return false;
        }

        MainHudQuickbarSlotSnapshot slot = _state.Slots.GetSlot(slotIndex);

        if (!slot.IsOccupied || slot.CoverFlag != 0)
        {
            return false;
        }

        activation = new(slotIndex, slot.ContentKind, slot.ContentId, slot.Payload, slot.ItemUid, slot.Selector);
        return true;
    }

    public bool TryCreatePickupRequest(int logicalX, int logicalY, out MainHudQuickbarPickupRequest? request)
    {
        request = null;

        if (!_state.InteractionEnabled || !_state.PickupEnabled || !_layout.TryGetSlotIndex(logicalX, logicalY, out int slotIndex))
        {
            return false;
        }

        MainHudQuickbarSlotSnapshot slot = _state.Slots.GetSlot(slotIndex);

        if (!slot.IsOccupied || slot.CoverFlag != 0)
        {
            return false;
        }

        request = new(slotIndex, slotIndex + 1, 1, slot);
        return true;
    }

    public void CompletePickup(MainHudQuickbarPickupRequest request, bool dragStarted)
    {
        if (dragStarted)
        {
            _state.Slots.ClearOccupied(request.ColumnOneBased, request.RowOneBased);
        }
    }
}
