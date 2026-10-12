namespace OpenConquer.Client.UI.Login;

/// <summary>Owns an explicit press/release gesture for Exit. Enter has no submit capability.</summary>
internal sealed class LoginActionInput
{
    private readonly LoginActionLayout _layout;
    private LoginActionButton _pressed;

    public LoginActionButton Hovered
    {
        get; private set;
    }
    public LoginActionButton Pressed => _pressed;

    public LoginActionInput(LoginActionLayout layout) => _layout = layout;

    public void PointerMoved(bool mapped, int x, int y) => Hovered = mapped ? _layout.Hit(x, y) : LoginActionButton.None;

    /// <summary>Returns whether either action rectangle consumed the pointer press.</summary>
    public bool PointerPressed(bool mapped, int x, int y)
    {
        _pressed = LoginActionButton.None;
        Hovered = mapped ? _layout.Hit(x, y) : LoginActionButton.None;

        if (Hovered == LoginActionButton.Exit)
        {
            _pressed = LoginActionButton.Exit;
        }

        // Enter deliberately captures input without dispatching any login or network operation.
        return Hovered != LoginActionButton.None;
    }

    public LoginActionButton PointerReleased(bool mapped, int x, int y)
    {
        LoginActionButton pressed = _pressed;
        _pressed = LoginActionButton.None;
        Hovered = mapped ? _layout.Hit(x, y) : LoginActionButton.None;
        return pressed == LoginActionButton.Exit && Hovered == LoginActionButton.Exit ? LoginActionButton.Exit : LoginActionButton.None;
    }

    public void FocusLost()
    {
        _pressed = LoginActionButton.None;
        Hovered = LoginActionButton.None;
    }
}
