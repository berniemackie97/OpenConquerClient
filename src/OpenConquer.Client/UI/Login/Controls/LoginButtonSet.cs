using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Controls;

/// <summary>
/// Push-button behaviour for a group of buttons sharing one pointer: Over while hovered, Down while pressed and
/// still over the button, and a click only when the press is released over the button it started on.
/// </summary>
internal sealed class LoginButtonSet<TButton> where TButton : struct, Enum
{
    private readonly (TButton Button, LoginRect Bounds)[] _buttons;
    private TButton? _hovered;
    private TButton? _pressed;

    public LoginButtonSet(IEnumerable<(TButton Button, LoginRect Bounds)> buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        _buttons = [.. buttons];
    }

    public LoginRect BoundsOf(TButton button)
    {
        foreach ((TButton candidate, LoginRect bounds) in _buttons)
        {
            if (EqualityComparer<TButton>.Default.Equals(candidate, button))
            {
                return bounds;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(button), button, "The button is not part of this set.");
    }

    public LoginButtonState StateOf(TButton button)
    {
        bool hovered = Is(_hovered, button);
        if (_pressed is not null)
        {
            return Is(_pressed, button) && hovered ? LoginButtonState.Down : LoginButtonState.Up;
        }

        return hovered ? LoginButtonState.Over : LoginButtonState.Up;
    }

    /// <summary>Tracks the pointer and returns the button it just moved onto, if any.</summary>
    public TButton? PointerMoved(bool mapped, int x, int y)
    {
        TButton? previous = _hovered;
        _hovered = mapped ? Hit(x, y) : null;
        return _hovered is not null && !Is(previous, _hovered.Value) ? _hovered : null;
    }

    /// <summary>Starts a press and returns the button that captured it, if any.</summary>
    public TButton? PointerPressed(bool mapped, int x, int y)
    {
        _hovered = mapped ? Hit(x, y) : null;
        _pressed = _hovered;
        return _pressed;
    }

    /// <summary>Ends a press and returns the button that was clicked, if any.</summary>
    public TButton? PointerReleased(bool mapped, int x, int y)
    {
        TButton? pressed = _pressed;
        _pressed = null;
        _hovered = mapped ? Hit(x, y) : null;
        return pressed is not null && Is(_hovered, pressed.Value) ? pressed : null;
    }

    public void Reset()
    {
        _hovered = null;
        _pressed = null;
    }

    private TButton? Hit(int x, int y)
    {
        foreach ((TButton button, LoginRect bounds) in _buttons)
        {
            if (bounds.Contains(x, y))
            {
                return button;
            }
        }

        return null;
    }

    private static bool Is(TButton? candidate, TButton button) =>
        candidate is { } value && EqualityComparer<TButton>.Default.Equals(value, button);
}
