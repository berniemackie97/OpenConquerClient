using OpenConquer.Client.UI.Hud.ActionButtons;
using OpenConquer.Client.UI.Hud.CheckControls;
using OpenConquer.Client.UI.Hud.Quickbar;
using OpenConquer.Client.UI.Hud.StatusHints;

namespace OpenConquer.Client.UI.Hud.Input;

internal sealed class MainHudInputCoordinator
{
    private readonly MainHudActionButtonStripInput _actionButtonInput;
    private readonly MainHudQuickbarInput _quickbarInput;
    private readonly MainHudCheckControlsInput _checkControlsInput;
    private readonly MainHudStatusHintInput _statusHintInput;
    private readonly MainHudStatusHintState _statusHintState;

    public MainHudInputCoordinator(MainHudActionButtonStripInput actionButtonInput, MainHudQuickbarInput quickbarInput,
        MainHudCheckControlsInput checkControlsInput, MainHudStatusHintInput statusHintInput, MainHudStatusHintState statusHintState)
    {
        ArgumentNullException.ThrowIfNull(actionButtonInput);
        ArgumentNullException.ThrowIfNull(quickbarInput);
        ArgumentNullException.ThrowIfNull(checkControlsInput);
        ArgumentNullException.ThrowIfNull(statusHintInput);
        ArgumentNullException.ThrowIfNull(statusHintState);

        _actionButtonInput = actionButtonInput;
        _quickbarInput = quickbarInput;
        _checkControlsInput = checkControlsInput;
        _statusHintInput = statusHintInput;
        _statusHintState = statusHintState;
    }

    public void HandlePointerMoved(bool controlsAvailable, bool mapped, int logicalX, int logicalY)
    {
        bool consumedByQuickbar = false;
        MainHudQuickbarHoverNotification notification = default;

        if (controlsAvailable)
        {
            if (mapped)
            {
                _actionButtonInput.HandlePointerMoved(logicalX, logicalY);
                consumedByQuickbar = _quickbarInput.HandlePointerMoved(logicalX, logicalY, out notification);
            }
            else
            {
                _actionButtonInput.HandlePointerMoved(-1, -1);
                _quickbarInput.HandlePointerMoved(-1, -1, out notification);
            }
        }

        _statusHintInput.HandlePointerMoved(mapped && !consumedByQuickbar ? logicalX : -1,
            mapped && !consumedByQuickbar ? logicalY : -1);

        MainHudQuickbarHintHandoff.Apply(_statusHintState, notification);
    }

    public void HandlePrimaryPointerPressed(bool controlsAvailable, bool mapped, int logicalX, int logicalY)
    {
        if (!controlsAvailable || !mapped)
        {
            return;
        }

        _actionButtonInput.HandleLeftButtonDown(logicalX, logicalY);
        _checkControlsInput.HandleLeftButtonDown(logicalX, logicalY);
    }

    public void HandlePrimaryPointerReleased(bool controlsAvailable, bool mapped, int logicalX, int logicalY)
    {
        if (!controlsAvailable)
        {
            return;
        }

        _actionButtonInput.HandleLeftButtonUp(mapped ? logicalX : -1, mapped ? logicalY : -1, out _);
    }

    public void PollQuickbarPointer(bool mapped, int logicalX, int logicalY)
    {
        _quickbarInput.PollPointer(mapped ? logicalX : -1, mapped ? logicalY : -1, out MainHudQuickbarHoverNotification notification);
        MainHudQuickbarHintHandoff.Apply(_statusHintState, notification);
    }
}
