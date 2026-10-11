using OpenConquer.Client.UI.Hud.Quickbar;

namespace OpenConquer.Client.UI.Hud.StatusHints;

internal static class MainHudQuickbarHintHandoff
{
    public static void Apply(MainHudStatusHintState state, MainHudQuickbarHoverNotification notification)
    {
        ArgumentNullException.ThrowIfNull(state);

        switch (notification.Kind)
        {
            case MainHudQuickbarHoverNotificationKind.Skill:
                state.SetMagicAnchor(notification.AnchorX, notification.AnchorY);
                state.SelectMagic(notification.ContentId);
                break;

            case MainHudQuickbarHoverNotificationKind.Dance:
                state.SetMagicAnchor(notification.AnchorX, notification.AnchorY);
                state.SelectDance(notification.Context);
                break;

            case MainHudQuickbarHoverNotificationKind.WeaponSwap:
                state.SetMagicAnchor(notification.AnchorX, notification.AnchorY);
                state.SelectEquipmentSwitch(notification.Context);
                break;

            case MainHudQuickbarHoverNotificationKind.Clear:
            case MainHudQuickbarHoverNotificationKind.Item:
            case MainHudQuickbarHoverNotificationKind.Generic:
                if (state.IsVisible && state.Category is MainHudStatusHintState.MagicCategory or MainHudStatusHintState.DanceCategory or MainHudStatusHintState.EquipmentSwitchCategory)
                {
                    state.Clear();
                }

                break;
        }
    }
}
