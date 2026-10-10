using System.Buffers.Binary;
using OpenConquer.Client.UI.Hud.ActionButtons;
using OpenConquer.Client.UI.Hud.CheckControls;
using OpenConquer.Client.UI.Hud.Input;
using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Content.Regions;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudInputCoordinatorTests
{
    private static readonly MainHudQuickbarSlotMetadata s_metadata = new(0, 0, 1, 0);
    private static readonly ClientRegionFile s_inactiveRegion = CreateInactiveRegion();

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PointerMoved_QuickbarConsumesOverlappingExperienceHotspot(int width, int height)
    {
        Context context = new(width, height);
        MainHudQuickbarSlotBounds slot = context.QuickbarLayout.GetSlotBounds(0);

        context.Coordinator.HandlePointerMoved(true, true, slot.X + 10, slot.Y);

        Assert.True(context.Quickbar.IsHoverActive);
        Assert.True(context.Hints.CanRenderMagic);
        Assert.Equal(MainHudStatusHintState.NoHoveredHotspot, context.Hints.HoveredHotspot);
        Assert.Equal(1000u, context.Hints.MagicType);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PointerMoved_MagicToHudHotspotPreservesCategoryEightAfterPolling(int width, int height)
    {
        Context context = new(width, height);
        context.MoveToSlot(0);

        int hotspotY = height - MainHudStatusHintDefinition.MainDialogHeight + 23;
        context.Coordinator.HandlePointerMoved(true, true, 0, hotspotY);

        Assert.True(context.Quickbar.IsHoverActive);
        Assert.True(context.Hints.CanRender);
        Assert.Equal(MainHudStatusHintKind.WalkRun, context.Hints.Kind);

        context.Coordinator.PollQuickbarPointer(true, 0, hotspotY);

        Assert.False(context.Quickbar.IsHoverActive);
        Assert.True(context.Hints.CanRender);
        Assert.False(context.Hints.CanRenderMagic);
        Assert.Equal(MainHudStatusHintDefinition.Category, context.Hints.Category);
        Assert.Equal(MainHudStatusHintKind.WalkRun, context.Hints.Kind);
        Assert.Equal(1000u, context.Hints.MagicType);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PointerMoved_MagicToEmptySlotClearsCategoryNineImmediately(int width, int height)
    {
        Context context = new(width, height);
        context.MoveToSlot(0);

        context.MoveToSlot(3);

        Assert.False(context.Quickbar.IsHoverActive);
        Assert.False(context.Hints.IsVisible);
        Assert.Equal(MainHudStatusHintState.MagicCategory, context.Hints.Category);
        Assert.Equal(1000u, context.Hints.MagicType);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PointerMoved_LeavingQuickbarPreservesMagicUntilFramePoll(int width, int height)
    {
        Context context = new(width, height);
        context.MoveToSlot(0);

        context.Coordinator.HandlePointerMoved(true, true, 600, 400);

        Assert.True(context.Quickbar.IsHoverActive);
        Assert.True(context.Hints.CanRenderMagic);

        context.Coordinator.PollQuickbarPointer(true, 600, 400);

        Assert.False(context.Quickbar.IsHoverActive);
        Assert.False(context.Hints.IsVisible);
        Assert.False(context.Hints.CanRenderMagic);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PointerMoved_MagicToActionPreservesNativeHoverCache(int width, int height)
    {
        Context context = new(width, height);
        context.MoveToSlot(0);
        context.MoveToSlot(1);

        Assert.True(context.Quickbar.IsHoverActive);
        Assert.Equal(2, context.Quickbar.HoveredColumnOneBased);
        Assert.Equal(1000u, context.Quickbar.HoveredContentId);
        Assert.True(context.Hints.CanRenderMagic);

        MainHudQuickbarSlotBounds action = context.QuickbarLayout.GetSlotBounds(1);
        context.Coordinator.PollQuickbarPointer(true, action.X, action.Y);

        Assert.True(context.Quickbar.IsHoverActive);
        Assert.True(context.Hints.CanRenderMagic);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PointerMoved_HudHotspotToXpMagicTransfersHintOwnership(int width, int height)
    {
        Context context = new(width, height);
        context.Coordinator.HandlePointerMoved(true, true, 0, height - MainHudStatusHintDefinition.MainDialogHeight + 23);

        Assert.True(context.Hints.CanRender);

        context.MoveToSlot(9);

        Assert.False(context.Hints.CanRender);
        Assert.True(context.Hints.CanRenderMagic);
        Assert.Equal(uint.MaxValue, context.Hints.MagicType);
        Assert.Equal(450, context.Hints.MagicAnchorX);
        Assert.Equal(height - 1, context.Hints.MagicStoredAnchorY);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PointerMoved_UnavailableControlsStillUpdateCategoryEightHints(int width, int height)
    {
        Context context = new(width, height);
        int hotspotY = height - MainHudStatusHintDefinition.MainDialogHeight + 23;

        context.Coordinator.HandlePointerMoved(false, true, 0, hotspotY);

        Assert.True(context.Hints.CanRender);
        Assert.Equal(MainHudStatusHintKind.WalkRun, context.Hints.Kind);

        context.MoveToSlot(0, controlsAvailable: false);

        Assert.False(context.Quickbar.IsHoverActive);
        Assert.False(context.Hints.IsVisible);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PointerMoved_UnavailableControlsDoNotUpdateActionButtons(int width, int height)
    {
        Context context = new(width, height);
        MainHudActionButtonBounds bounds = context.ActionLayout.GetBounds(MainHudActionButtonId.Button42);

        context.Coordinator.HandlePointerMoved(false, true, bounds.X, bounds.Y);

        Assert.False(context.Buttons.GetButton(MainHudActionButtonId.Button42).IsCursorInside);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PointerMoved_UnmappedCoordinatesPreserveMagicUntilPoll(int width, int height)
    {
        Context context = new(width, height);
        context.MoveToSlot(0);

        MainHudQuickbarSlotBounds slot = context.QuickbarLayout.GetSlotBounds(0);
        context.Coordinator.HandlePointerMoved(true, false, slot.X, slot.Y);

        Assert.True(context.Quickbar.IsHoverActive);
        Assert.True(context.Hints.CanRenderMagic);

        context.Coordinator.PollQuickbarPointer(false, slot.X, slot.Y);

        Assert.False(context.Quickbar.IsHoverActive);
        Assert.False(context.Hints.IsVisible);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PrimaryPointerPressed_CapturesAvailableActionButton(int width, int height)
    {
        Context context = new(width, height);
        MainHudActionButtonBounds bounds = context.ActionLayout.GetBounds(MainHudActionButtonId.Button40);

        context.Coordinator.HandlePrimaryPointerPressed(true, true, bounds.X, bounds.Y);

        Assert.True(context.Buttons.GetButton(MainHudActionButtonId.Button40).IsPointerCaptured);

        context.Coordinator.HandlePointerMoved(true, false, bounds.X, bounds.Y);

        Assert.True(context.Buttons.GetButton(MainHudActionButtonId.Button40).IsPointerCaptured);
        Assert.False(context.Buttons.GetButton(MainHudActionButtonId.Button40).IsCursorInside);

        context.Coordinator.HandlePrimaryPointerReleased(true, false, bounds.X, bounds.Y);

        Assert.False(context.Buttons.GetButton(MainHudActionButtonId.Button40).IsPointerCaptured);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PrimaryPointerReleased_DisabledControlsPreserveExistingCapture(int width, int height)
    {
        Context context = new(width, height);
        MainHudActionButtonBounds bounds = context.ActionLayout.GetBounds(MainHudActionButtonId.Button42);

        context.Coordinator.HandlePrimaryPointerPressed(true, true, bounds.X, bounds.Y);
        Assert.True(context.Buttons.GetButton(MainHudActionButtonId.Button42).IsPointerCaptured);

        context.Coordinator.HandlePrimaryPointerReleased(false, true, bounds.X, bounds.Y);

        Assert.True(context.Buttons.GetButton(MainHudActionButtonId.Button42).IsPointerCaptured);

        context.Coordinator.HandlePrimaryPointerReleased(true, false, bounds.X, bounds.Y);

        Assert.False(context.Buttons.GetButton(MainHudActionButtonId.Button42).IsPointerCaptured);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PrimaryPointerPressed_TogglesCheckControlsOnlyWhenInteractiveAndMapped(int width, int height)
    {
        Context context = new(width, height);
        MainHudCheckControlBounds bounds = context.CheckLayout.GetBounds(MainHudCheckControlId.Check40);

        context.Coordinator.HandlePrimaryPointerPressed(false, true, bounds.X, bounds.Y);
        context.Coordinator.HandlePrimaryPointerPressed(true, false, bounds.X, bounds.Y);

        Assert.Equal(0, context.Checks.GetControl(MainHudCheckControlId.Check40).CurrentState);

        context.Coordinator.HandlePrimaryPointerPressed(true, true, bounds.X, bounds.Y);

        Assert.Equal(1, context.Checks.GetControl(MainHudCheckControlId.Check40).CurrentState);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void PrimaryPointerPressed_HonorsExistingControlAvailabilityPredicates(int width, int height)
    {
        Context context = new(width, height)
        {
            ActionsAvailable = false,
            ChecksAvailable = false,
        };

        MainHudActionButtonBounds button = context.ActionLayout.GetBounds(MainHudActionButtonId.Button42);
        MainHudCheckControlBounds check = context.CheckLayout.GetBounds(MainHudCheckControlId.Check40);

        context.Coordinator.HandlePrimaryPointerPressed(true, true, button.X, button.Y);
        context.Coordinator.HandlePrimaryPointerPressed(true, true, check.X, check.Y);

        Assert.False(context.Buttons.GetButton(MainHudActionButtonId.Button42).IsPointerCaptured);
        Assert.Equal(0, context.Checks.GetControl(MainHudCheckControlId.Check40).CurrentState);
    }

    private static ClientRegionFile CreateInactiveRegion()
    {
        byte[] encoded = new byte[sizeof(uint) + 32 + 16];
        Span<byte> payload = encoded.AsSpan(sizeof(uint));

        BinaryPrimitives.WriteUInt32LittleEndian(encoded, 48);
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[4..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[8..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[12..], 16);

        for (int index = 0; index < 2; index++)
        {
            int offset = 16 + index * 16;

            BinaryPrimitives.WriteInt32LittleEndian(payload[offset..], 600);
            BinaryPrimitives.WriteInt32LittleEndian(payload[(offset + 4)..], 600);
            BinaryPrimitives.WriteInt32LittleEndian(payload[(offset + 8)..], 601);
            BinaryPrimitives.WriteInt32LittleEndian(payload[(offset + 12)..], 601);
        }

        return ClientRegionFile.Parse(encoded);
    }

    private sealed class Context
    {
        public Context(int width, int height)
        {
            LogicalRenderSize resolution = new(width, height);

            QuickbarLayout = MainHudQuickbarLayout.Create(resolution);
            ActionLayout = MainHudActionButtonLayout.Create(resolution);
            CheckLayout = MainHudCheckControlLayout.Create(resolution);

            Quickbar = new MainHudQuickbarState();
            Buttons = new MainHudActionButtonStripState();
            Checks = new MainHudCheckControlsState();
            Hints = new MainHudStatusHintState();

            Quickbar.Slots.Populate(1, 1, 1000, 0, 3, (byte)MainHudQuickbarContentKind.Magic, 0, s_metadata);
            Quickbar.Slots.Populate(2, 1, 1, 0, 3, (byte)MainHudQuickbarContentKind.Action, 0, s_metadata);
            Quickbar.Slots.Populate(10, 1, uint.MaxValue, 0, 3, (byte)MainHudQuickbarContentKind.XpMagic, 0, s_metadata);

            Coordinator = new MainHudInputCoordinator(
                new MainHudActionButtonStripInput(Buttons, ActionLayout, _ => ActionsAvailable),
                new MainHudQuickbarInput(Quickbar, QuickbarLayout),
                new MainHudCheckControlsInput(Checks, CheckLayout, _ => ChecksAvailable),
                new MainHudStatusHintInput(Hints, MainHudStatusHintLayout.Create(resolution),
                    s_inactiveRegion, s_inactiveRegion, s_inactiveRegion, alternateLayout: false),
                Hints);
        }

        public MainHudInputCoordinator Coordinator
        {
            get;
        }

        public MainHudQuickbarLayout QuickbarLayout
        {
            get;
        }

        public MainHudActionButtonLayout ActionLayout
        {
            get;
        }

        public MainHudCheckControlLayout CheckLayout
        {
            get;
        }

        public MainHudQuickbarState Quickbar
        {
            get;
        }

        public MainHudActionButtonStripState Buttons
        {
            get;
        }

        public MainHudCheckControlsState Checks
        {
            get;
        }

        public MainHudStatusHintState Hints
        {
            get;
        }
        public bool ActionsAvailable { get; set; } = true;
        public bool ChecksAvailable { get; set; } = true;

        public void MoveToSlot(int slotIndex, bool controlsAvailable = true)
        {
            MainHudQuickbarSlotBounds bounds = QuickbarLayout.GetSlotBounds(slotIndex);
            Coordinator.HandlePointerMoved(controlsAvailable, true, bounds.X, bounds.Y);
        }
    }
}
