namespace OpenConquer.Client.UI.Hud.CheckControls;

internal sealed class MainHudCheckControlsInput
{
    private readonly MainHudCheckControlsState _state;
    private readonly MainHudCheckControlLayout _layout;
    private readonly Func<MainHudCheckControlId, bool> _isAvailable;

    public MainHudCheckControlsInput(MainHudCheckControlsState state, MainHudCheckControlLayout layout, Func<MainHudCheckControlId, bool> isAvailable)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(isAvailable);

        _state = state;
        _layout = layout;
        _isAvailable = isAvailable;
    }

    public bool HandleLeftButtonDown(int logicalX, int logicalY)
    {
        foreach (MainHudCheckControlDefinition definition in MainHudCheckControlDefinitions.NativeDrawOrder)
        {
            if (!_isAvailable(definition.Id) || !_layout.GetBounds(definition.Id).Contains(logicalX, logicalY))
            {
                continue;
            }

            _state.GetControl(definition.Id).HandleLeftButtonDown();
            return true;
        }

        return false;
    }
}
