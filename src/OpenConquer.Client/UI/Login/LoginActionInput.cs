namespace OpenConquer.Client.UI.Login;

/// <summary>Owns the native Enter/Exit press/release gestures. It never submits credentials or opens sockets.</summary>
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

        _pressed = Hovered;
        return Hovered != LoginActionButton.None;
    }

    public LoginActionButton PointerReleased(bool mapped, int x, int y)
    {
        LoginActionButton pressed = _pressed;
        _pressed = LoginActionButton.None;
        Hovered = mapped ? _layout.Hit(x, y) : LoginActionButton.None;
        return pressed != LoginActionButton.None && pressed == Hovered ? pressed : LoginActionButton.None;
    }

    public void FocusLost()
    {
        _pressed = LoginActionButton.None;
        Hovered = LoginActionButton.None;
    }
}
