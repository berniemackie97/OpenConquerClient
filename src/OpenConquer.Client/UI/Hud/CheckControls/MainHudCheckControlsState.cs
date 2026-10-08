namespace OpenConquer.Client.UI.Hud.CheckControls;

internal sealed class MainHudCheckControlsState
{
    private readonly Dictionary<MainHudCheckControlId, MainHudCheckControlState> _controls = CreateControlStates();

    public MainHudCheckControlState GetControl(MainHudCheckControlId id)
    {
        if (_controls.TryGetValue(id, out MainHudCheckControlState? state))
        {
            return state;
        }

        throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown native main HUD check-control identifier.");
    }

    private static Dictionary<MainHudCheckControlId, MainHudCheckControlState> CreateControlStates()
    {
        Dictionary<MainHudCheckControlId, MainHudCheckControlState> states = new();

        foreach (MainHudCheckControlDefinition definition in MainHudCheckControlDefinitions.NativeDrawOrder)
        {
            states.Add(definition.Id, new MainHudCheckControlState());
        }

        return states;
    }
}
