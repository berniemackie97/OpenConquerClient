using System.Buffers.Binary;
using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Content.Regions;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudQuickbarHintHandoffTests
{
    private static readonly MainHudQuickbarSlotMetadata s_metadata = new(0, 0, 1, 0);
    private static readonly ClientRegionFile s_inactiveRegion = CreateInactiveRegion();

    [Theory]
    [InlineData(800, 600, 0, 1000u)]
    [InlineData(1024, 768, 0, 1000u)]
    [InlineData(800, 600, 9, uint.MaxValue)]
    [InlineData(1024, 768, 9, uint.MaxValue)]
    public void MagicToHudHotspot_FramePollCannotClearNewCategoryEightSelection(int width, int height, int slotIndex, uint magicType)
    {
        Context context = new(width, height);
        context.MoveToSlot(slotIndex);

        Assert.True(context.Hints.CanRenderMagic);
        Assert.Equal(magicType, context.Hints.MagicType);

        int hotspotX = 0;
        int hotspotY = height - MainHudStatusHintDefinition.MainDialogHeight + 23;

        context.Move(hotspotX, hotspotY);

        Assert.True(context.Quickbar.IsHoverActive);
        Assert.Equal(MainHudStatusHintDefinition.Category, context.Hints.Category);
        Assert.Equal(MainHudStatusHintKind.WalkRun, context.Hints.Kind);
        Assert.True(context.Hints.CanRender);
        Assert.False(context.Hints.CanRenderMagic);

        Assert.True(context.Poll(hotspotX, hotspotY));

        Assert.False(context.Quickbar.IsHoverActive);
        Assert.Equal(MainHudStatusHintDefinition.Category, context.Hints.Category);
        Assert.Equal(MainHudStatusHintKind.WalkRun, context.Hints.Kind);
        Assert.Equal((int)MainHudStatusHintKind.WalkRun, context.Hints.HoveredHotspot);
        Assert.True(context.Hints.CanRender);
        Assert.False(context.Hints.CanRenderMagic);
        Assert.Equal(magicType, context.Hints.MagicType);
        Assert.Equal(90 + 40 * slotIndex, context.Hints.MagicAnchorX);
        Assert.Equal(height - 1, context.Hints.MagicStoredAnchorY);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void MagicToEmptyQuickbarSlot_ClearsActiveMagicHint(int width, int height)
    {
        Context context = new(width, height);
        context.MoveToSlot(0);

        Assert.True(context.Hints.CanRenderMagic);

        context.MoveToSlot(3);

        Assert.False(context.Quickbar.IsHoverActive);
        Assert.False(context.Hints.IsVisible);
        Assert.False(context.Hints.CanRenderMagic);
        Assert.Equal(MainHudStatusHintState.MagicCategory, context.Hints.Category);
        Assert.Equal(1000u, context.Hints.MagicType);
        Assert.Equal(90, context.Hints.MagicAnchorX);
        Assert.Equal(height - 1, context.Hints.MagicStoredAnchorY);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void MagicToOutsideQuickbar_PreservesHintUntilFramePoll(int width, int height)
    {
        Context context = new(width, height);
        context.MoveToSlot(0);

        context.Move(600, 400);

        Assert.True(context.Quickbar.IsHoverActive);
        Assert.True(context.Hints.CanRenderMagic);

        Assert.True(context.Poll(600, 400));

        Assert.False(context.Quickbar.IsHoverActive);
        Assert.False(context.Hints.IsVisible);
        Assert.False(context.Hints.CanRenderMagic);
        Assert.Equal(1000u, context.Hints.MagicType);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void MagicToAction_PreservesNativeHoverCacheAndMagicHint(int width, int height)
    {
        Context context = new(width, height);
        context.MoveToSlot(0);
        context.MoveToSlot(1);

        Assert.True(context.Quickbar.IsHoverActive);
        Assert.Equal(2, context.Quickbar.HoveredColumnOneBased);
        Assert.Equal(1, context.Quickbar.HoveredRowOneBased);
        Assert.Equal(1000u, context.Quickbar.HoveredContentId);
        Assert.Equal(1000u, context.Hints.MagicType);
        Assert.Equal(90, context.Hints.MagicAnchorX);
        Assert.True(context.Hints.CanRenderMagic);

        MainHudQuickbarSlotBounds action = context.Layout.GetSlotBounds(1);

        Assert.False(context.Poll(action.X, action.Y));
        Assert.True(context.Hints.CanRenderMagic);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void MagicToItem_ClearsCategoryNineWithoutClearingQuickbarHover(int width, int height)
    {
        Context context = new(width, height);
        context.MoveToSlot(0);
        context.MoveToSlot(2);

        Assert.True(context.Quickbar.IsHoverActive);
        Assert.Equal(3, context.Quickbar.HoveredColumnOneBased);
        Assert.False(context.Hints.IsVisible);
        Assert.False(context.Hints.CanRenderMagic);
        Assert.Equal(1000u, context.Hints.MagicType);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void HudHotspotToMagic_QuickbarMaySelectCategoryNineAgain(int width, int height)
    {
        Context context = new(width, height);
        context.Move(0, height - MainHudStatusHintDefinition.MainDialogHeight + 23);

        Assert.True(context.Hints.CanRender);
        Assert.Equal(MainHudStatusHintKind.WalkRun, context.Hints.Kind);

        context.MoveToSlot(0);

        Assert.False(context.Hints.CanRender);
        Assert.True(context.Hints.CanRenderMagic);
        Assert.Equal(MainHudStatusHintState.MagicCategory, context.Hints.Category);
        Assert.Equal(1000u, context.Hints.MagicType);
        Assert.Equal(90, context.Hints.MagicAnchorX);
        Assert.Equal(height - 1, context.Hints.MagicStoredAnchorY);
    }

    [Fact]
    public void LateClearAndNonSkillNotifications_DoNotAffectCategoryEight()
    {
        MainHudStatusHintState hints = new();
        hints.Select((int)MainHudStatusHintKind.WalkRun);

        MainHudQuickbarHoverNotificationKind[] notifications =
        [
            MainHudQuickbarHoverNotificationKind.Clear,
            MainHudQuickbarHoverNotificationKind.Item,
            MainHudQuickbarHoverNotificationKind.Dance,
            MainHudQuickbarHoverNotificationKind.WeaponSwap,
            MainHudQuickbarHoverNotificationKind.Generic,
            MainHudQuickbarHoverNotificationKind.None,
        ];

        foreach (MainHudQuickbarHoverNotificationKind kind in notifications)
        {
            MainHudQuickbarHintHandoff.Apply(hints, new MainHudQuickbarHoverNotification(kind, -1, 0, 0, 0, 0, 0));

            Assert.True(hints.IsVisible);
            Assert.True(hints.CanRender);
            Assert.Equal(MainHudStatusHintDefinition.Category, hints.Category);
            Assert.Equal(MainHudStatusHintKind.WalkRun, hints.Kind);
        }
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
        private readonly MainHudQuickbarInput _quickbarInput;
        private readonly MainHudStatusHintInput _hintInput;

        public Context(int width, int height)
        {
            LogicalRenderSize resolution = new(width, height);

            Layout = MainHudQuickbarLayout.Create(resolution);
            Quickbar = new MainHudQuickbarState();
            Hints = new MainHudStatusHintState();

            Quickbar.Slots.Populate(1, 1, 1000, 0, 3, (byte)MainHudQuickbarContentKind.Magic, 0, s_metadata);
            Quickbar.Slots.Populate(2, 1, 1, 0, 3, (byte)MainHudQuickbarContentKind.Action, 0, s_metadata);
            Quickbar.Slots.Populate(3, 1, 100, 0, 3, (byte)MainHudQuickbarContentKind.Item, 0, s_metadata);
            Quickbar.Slots.Populate(10, 1, uint.MaxValue, 0, 3, (byte)MainHudQuickbarContentKind.XpMagic, 0, s_metadata);

            _quickbarInput = new MainHudQuickbarInput(Quickbar, Layout);
            _hintInput = new MainHudStatusHintInput(Hints, MainHudStatusHintLayout.Create(resolution), s_inactiveRegion, s_inactiveRegion, s_inactiveRegion, alternateLayout: false);
        }

        public MainHudQuickbarLayout Layout
        {
            get;
        }

        public MainHudQuickbarState Quickbar
        {
            get;
        }

        public MainHudStatusHintState Hints
        {
            get;
        }

        public void MoveToSlot(int slotIndex)
        {
            MainHudQuickbarSlotBounds slot = Layout.GetSlotBounds(slotIndex);
            Move(slot.X, slot.Y);
        }

        public void Move(int x, int y)
        {
            bool consumedByQuickbar = _quickbarInput.HandlePointerMoved(x, y, out MainHudQuickbarHoverNotification notification);

            _hintInput.HandlePointerMoved(consumedByQuickbar ? -1 : x, consumedByQuickbar ? -1 : y);
            MainHudQuickbarHintHandoff.Apply(Hints, notification);
        }

        public bool Poll(int x, int y)
        {
            bool emitted = _quickbarInput.PollPointer(x, y, out MainHudQuickbarHoverNotification notification);
            MainHudQuickbarHintHandoff.Apply(Hints, notification);
            return emitted;
        }
    }
}
