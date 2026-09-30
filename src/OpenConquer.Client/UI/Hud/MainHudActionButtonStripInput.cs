namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudActionButtonStripInput
{
    private readonly MainHudActionButtonStripState _state;
    private readonly MainHudActionButtonLayout _layout;
    private readonly Func<MainHudActionButtonId, bool> _isAvailable;

    public MainHudActionButtonStripInput(MainHudActionButtonStripState state, MainHudActionButtonLayout layout, Func<MainHudActionButtonId, bool> isAvailable)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(isAvailable);

        _state = state;
        _layout = layout;
        _isAvailable = isAvailable;
    }

    public bool HandlePointerMoved(int logicalX, int logicalY)
    {
        if (FindCapturedButton() is { } captured)
        {
            MainHudActionButtonState button = _state.GetButton(captured);
            button.HandlePointerMoved(logicalX, logicalY, _layout.GetBounds(captured));
            return true;
        }

        bool handled = false;

        foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
        {
            MainHudActionButtonState button = _state.GetButton(definition.Id);
            MainHudActionButtonBounds bounds = _layout.GetBounds(definition.Id);

            if (!_isAvailable(definition.Id))
            {
                button.HandlePointerMoved(-1, -1, bounds);
                continue;
            }

            handled |= button.HandlePointerMoved(logicalX, logicalY, bounds);
        }

        return handled;
    }

    public bool HandleLeftButtonDown(int logicalX, int logicalY)
    {
        if (FindCapturedButton() is not null)
        {
            return true;
        }

        foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
        {
            if (!_isAvailable(definition.Id))
            {
                continue;
            }

            MainHudActionButtonState button = _state.GetButton(definition.Id);

            if (button.HandleLeftButtonDown(logicalX, logicalY, _layout.GetBounds(definition.Id)))
            {
                return true;
            }
        }

        return false;
    }

    public bool HandleLeftButtonUp(int logicalX, int logicalY, out MainHudActionButtonId? activated)
    {
        activated = null;

        if (FindCapturedButton() is not { } captured)
        {
            return false;
        }

        MainHudActionButtonState button = _state.GetButton(captured);
        bool handled = button.HandleLeftButtonUp(logicalX, logicalY, _layout.GetBounds(captured), out bool didActivate);

        if (didActivate && _isAvailable(captured))
        {
            activated = captured;
        }

        return handled;
    }

    private MainHudActionButtonId? FindCapturedButton()
    {
        foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
        {
            if (_state.GetButton(definition.Id).IsPointerCaptured)
            {
                return definition.Id;
            }
        }

        return null;
    }
}
