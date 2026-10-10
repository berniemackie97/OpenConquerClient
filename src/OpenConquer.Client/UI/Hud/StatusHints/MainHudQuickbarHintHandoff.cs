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

            case MainHudQuickbarHoverNotificationKind.Clear:
            case MainHudQuickbarHoverNotificationKind.Item:
            case MainHudQuickbarHoverNotificationKind.Dance:
            case MainHudQuickbarHoverNotificationKind.WeaponSwap:
            case MainHudQuickbarHoverNotificationKind.Generic:
                if (state.Category == MainHudStatusHintState.MagicCategory && state.IsVisible)
                {
                    state.Clear();
                }

                break;
        }
    }
}
