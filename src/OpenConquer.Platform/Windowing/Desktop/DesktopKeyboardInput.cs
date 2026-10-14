using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Silk.NET.Input;

namespace OpenConquer.Platform.Windowing.Desktop;

/// <summary>
/// Owns desktop keyboard subscriptions and translates backend input into UI commands and characters.
/// </summary>
internal sealed class DesktopKeyboardInput : IDisposable
{
    private readonly IKeyboard _keyboard;
    private readonly Action<DesktopKeyEvent> _keyPressed;
    private readonly Action<DesktopKeyEvent> _keyReleased;
    private readonly Action<char> _textEntered;
    private readonly DesktopKeyRepeat _repeat = new();

    private bool _hasFocus;
    private bool _disposed;

    public DesktopKeyboardInput(IKeyboard keyboard, bool hasFocus, Action<DesktopKeyEvent> keyPressed, Action<DesktopKeyEvent> keyReleased, Action<char> textEntered)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(keyPressed);
        ArgumentNullException.ThrowIfNull(keyReleased);
        ArgumentNullException.ThrowIfNull(textEntered);

        _keyboard = keyboard;
        _hasFocus = hasFocus;
        _keyPressed = keyPressed;
        _keyReleased = keyReleased;
        _textEntered = textEntered;

        try
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
            keyboard.KeyChar += OnKeyChar;
        }
        catch
        {
            try
            {
                keyboard.KeyChar -= OnKeyChar;
                keyboard.KeyUp -= OnKeyUp;
                keyboard.KeyDown -= OnKeyDown;
            }
            catch
            {
                // Preserve the subscription failure.
            }

            throw;
        }
    }

    public DesktopKeyModifiers Modifiers => GetModifiers();

    public string? ReadClipboardText()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _keyboard.ClipboardText;
    }

    public void WriteClipboardText(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(text);
        _keyboard.ClipboardText = text;
    }

    public void SetFocus(bool hasFocus)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _hasFocus = hasFocus;
        _repeat.Reset();
    }

    public void Advance()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hasFocus && _repeat.TryRepeat(Stopwatch.GetTimestamp(), out DesktopKey key))
        {
            _keyPressed(new DesktopKeyEvent(key, GetModifiers(), IsRepeat: true));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _hasFocus = false;
        _repeat.Reset();

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _keyboard.KeyChar -= OnKeyChar;
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _keyboard.KeyUp -= OnKeyUp;
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _keyboard.KeyDown -= OnKeyDown;
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            _disposed = true;
        }

        firstFailure?.Throw();
    }

    internal static DesktopKey TranslateKey(Key key) => key switch
    {
        Key.Tab => DesktopKey.Tab,
        Key.Enter or Key.KeypadEnter => DesktopKey.Enter,
        Key.Escape => DesktopKey.Escape,
        Key.Backspace => DesktopKey.Backspace,
        Key.Delete => DesktopKey.Delete,
        Key.Left => DesktopKey.Left,
        Key.Right => DesktopKey.Right,
        Key.Up => DesktopKey.Up,
        Key.Down => DesktopKey.Down,
        Key.Home => DesktopKey.Home,
        Key.End => DesktopKey.End,
        Key.PageUp => DesktopKey.PageUp,
        Key.PageDown => DesktopKey.PageDown,
        Key.A => DesktopKey.A,
        Key.C => DesktopKey.C,
        Key.V => DesktopKey.V,
        Key.X => DesktopKey.X,
        _ => DesktopKey.Unknown,
    };

    internal static bool IsTextCharacter(char character) => !char.IsControl(character) && !char.IsSurrogate(character);

    private void OnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        if (_disposed || !_hasFocus || !ReferenceEquals(keyboard, _keyboard))
        {
            return;
        }

        DesktopKey translated = TranslateKey(key);

        if (translated == DesktopKey.Unknown)
        {
            return;
        }

        _repeat.Press(translated, Stopwatch.GetTimestamp());
        _keyPressed(new DesktopKeyEvent(translated, GetModifiers(), IsRepeat: false));
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        if (_disposed || !ReferenceEquals(keyboard, _keyboard))
        {
            return;
        }

        DesktopKey translated = TranslateKey(key);
        _repeat.Release(translated);

        if (_hasFocus && translated != DesktopKey.Unknown)
        {
            _keyReleased(new DesktopKeyEvent(translated, GetModifiers(), IsRepeat: false));
        }
    }

    private void OnKeyChar(IKeyboard keyboard, char character)
    {
        if (!_disposed && _hasFocus && ReferenceEquals(keyboard, _keyboard) && IsTextCharacter(character) &&
            (GetModifiers() & (DesktopKeyModifiers.Control | DesktopKeyModifiers.Alt | DesktopKeyModifiers.Super)) == 0)
        {
            _textEntered(character);
        }
    }

    private DesktopKeyModifiers GetModifiers()
    {
        DesktopKeyModifiers modifiers = DesktopKeyModifiers.None;

        if (_keyboard.IsKeyPressed(Key.ShiftLeft) || _keyboard.IsKeyPressed(Key.ShiftRight))
        {
            modifiers |= DesktopKeyModifiers.Shift;
        }

        if (_keyboard.IsKeyPressed(Key.ControlLeft) || _keyboard.IsKeyPressed(Key.ControlRight))
        {
            modifiers |= DesktopKeyModifiers.Control;
        }

        if (_keyboard.IsKeyPressed(Key.AltLeft) || _keyboard.IsKeyPressed(Key.AltRight))
        {
            modifiers |= DesktopKeyModifiers.Alt;
        }

        if (_keyboard.IsKeyPressed(Key.SuperLeft) || _keyboard.IsKeyPressed(Key.SuperRight))
        {
            modifiers |= DesktopKeyModifiers.Super;
        }

        return modifiers;
    }
}
