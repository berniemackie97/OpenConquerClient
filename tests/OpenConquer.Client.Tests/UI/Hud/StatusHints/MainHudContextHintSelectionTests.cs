using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Client.UI.Hud.StatusHints;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints;

public sealed class MainHudContextHintSelectionTests
{
    private static readonly MainHudQuickbarSlotMetadata s_metadata = new(0, 0, 1, 0);

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void QuickbarDance_UsesGridContextRatherThanStoredMagicType(int width, int height)
    {
        MainHudQuickbarState quickbar = new();
        quickbar.Slots.Populate(1, 1, 1415, 777, 3, (byte)MainHudQuickbarContentKind.Dance, 0, s_metadata);
        MainHudQuickbarInput input = new(quickbar, MainHudQuickbarLayout.Create(new LogicalRenderSize(width, height)));
        MainHudStatusHintState hints = new();

        Assert.True(input.HandlePointerMoved(90, height - 43, out MainHudQuickbarHoverNotification notification));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.Dance, notification.Kind);
        Assert.Equal(3u, notification.Context);
        MainHudQuickbarHintHandoff.Apply(hints, notification);

        Assert.True(hints.CanRenderDance);
        Assert.Equal(MainHudStatusHintState.DanceCategory, hints.Category);
        Assert.Equal(3u, hints.ContextParameter);
        Assert.Equal(90, hints.MagicAnchorX);
        Assert.Equal(height - 1, hints.MagicStoredAnchorY);
        Assert.False(hints.CanRenderMagic);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1024, 768)]
    public void QuickbarWeaponSwap_PreservesFullContextAndAnchor(int width, int height)
    {
        MainHudQuickbarState quickbar = new();
        quickbar.Slots.Populate(1, 1, uint.MaxValue, 0, 3, (byte)MainHudQuickbarContentKind.WeaponSwap, 0, s_metadata);
        MainHudQuickbarInput input = new(quickbar, MainHudQuickbarLayout.Create(new LogicalRenderSize(width, height)));
        MainHudStatusHintState hints = new();

        Assert.True(input.HandlePointerMoved(90, height - 43, out MainHudQuickbarHoverNotification notification));
        Assert.Equal(MainHudQuickbarHoverNotificationKind.WeaponSwap, notification.Kind);
        Assert.Equal(0x5Eu, notification.Context);
        MainHudQuickbarHintHandoff.Apply(hints, notification);

        Assert.True(hints.CanRenderEquipmentSwitch);
        Assert.Equal(0x5Eu, hints.ContextParameter);
        Assert.Equal(height - 1, hints.MagicStoredAnchorY);
        Assert.False(hints.CanRenderDance);
    }

    [Fact]
    public void Transitions_KeepOnlyTheLatestEligibleSelection()
    {
        MainHudStatusHintState state = new();
        state.SetMagicAnchor(90, 557);
        state.SelectMagic(1000);
        Assert.True(state.CanRenderMagic);

        MainHudQuickbarHintHandoff.Apply(state, new(MainHudQuickbarHoverNotificationKind.Dance, 0, 1415, 0, 130, 557, 3));
        Assert.True(state.CanRenderDance);
        Assert.Equal(3u, state.ContextParameter);
        Assert.Equal(1000u, state.MagicType);

        MainHudQuickbarHintHandoff.Apply(state, new(MainHudQuickbarHoverNotificationKind.WeaponSwap, 0, 0, 0, 170, 557, 0x5E));
        Assert.True(state.CanRenderEquipmentSwitch);
        Assert.Equal(0x5Eu, state.ContextParameter);
        Assert.False(state.CanRenderDance);

        MainHudQuickbarHintHandoff.Apply(state, new(MainHudQuickbarHoverNotificationKind.Skill, 0, 65537, 0, 210, 557, 0));
        Assert.True(state.CanRenderMagic);
        Assert.Equal(65537u, state.MagicType);
        Assert.Equal(65537u, state.ContextParameter);
    }

    [Fact]
    public void GenericAndClearEvents_HideContextWithoutDiscardingSelection()
    {
        MainHudStatusHintState state = new();
        state.SetMagicAnchor(90, 557);
        state.SelectEquipmentSwitch(0x5E);
        MainHudQuickbarHintHandoff.Apply(state, new(MainHudQuickbarHoverNotificationKind.Generic, 0, 0, 0, 0, 0, 0));

        Assert.False(state.IsVisible);
        Assert.False(state.CanRenderEquipmentSwitch);
        Assert.Equal(MainHudStatusHintState.EquipmentSwitchCategory, state.Category);
        Assert.Equal(0x5Eu, state.ContextParameter);

        state.SelectDance(1380);
        state.SetSkillDragActive(true);
        Assert.True(state.IsVisible);
        Assert.False(state.CanRenderDance);
        state.SetSkillDragActive(false);
        Assert.True(state.CanRenderDance);

        MainHudQuickbarHintHandoff.Apply(state, new(MainHudQuickbarHoverNotificationKind.Clear, -1, 0, 0, 0, 0, 0));
        Assert.False(state.CanRenderDance);
        Assert.Equal(1380u, state.ContextParameter);
    }

    [Fact]
    public void EquipmentSwitchState_PreservesFullDwordAndUnknownAbsence()
    {
        MainHudMagicHintRuntimeSnapshot missing = new([], 1, 0, 0, 0, null, default);
        MainHudMagicHintRuntimeSnapshot present = new([], 1, 0, 0, 0, null, default, uint.MaxValue);

        Assert.Null(missing.EquipmentSwitchState);
        Assert.Equal(uint.MaxValue, present.EquipmentSwitchState);
    }
}
