namespace OpenConquer.Client.UI.Hud.CheckControls;

internal sealed class MainHudCheckControlState
{
    private int _currentState;

    public int CurrentState => _currentState;
    public int RenderFrame => _currentState;

    public void HandleLeftButtonDown()
    {
        _currentState = _currentState < MainHudCheckControlDefinitions.FrameCount - 1 ? _currentState + 1 : 0;
    }

    public void SetState(int state)
    {
        byte requested = unchecked((byte)state);

        if (requested >= MainHudCheckControlDefinitions.FrameCount)
        {
            return;
        }

        _currentState = requested;
    }
}
