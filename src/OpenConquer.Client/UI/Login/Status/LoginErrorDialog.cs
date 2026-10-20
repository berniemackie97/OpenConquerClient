using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Status;

/// <summary>start.dat's ten-frame modal timeline. No keyboard shortcut activates its buttons.</summary>
internal sealed class LoginErrorDialog
{
    private readonly LoginButtonSet<LoginDialogButton> _buttons;
    private bool _confirmed;

    internal LoginErrorDialog(LoginStageLayout layout, bool quit = false)
    {
        IsQuit = quit;
        _buttons = new(quit
            ? [(LoginDialogButton.Ok, layout.Place(381, 415, 96, 26)), (LoginDialogButton.Cancel, layout.Place(541, 415, 96, 26))]
            : [(LoginDialogButton.Ok, layout.Place(461, 415, 96, 26))]);
    }

    internal bool IsQuit
    {
        get;
    }
    internal int Frame { get; private set; } = 1;
    internal bool IsOpen => Frame != 1;
    internal string Text { get; private set; } = string.Empty;
    internal LoginRect Bounds(LoginDialogButton button) => _buttons.BoundsOf(button);
    internal LoginButtonState State(LoginDialogButton button) => _buttons.StateOf(button);

    internal void Show(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Frame = 2;
        _confirmed = false;
        _buttons.Reset();
    }

    internal LoginDialogButton? PointerMoved(bool mapped, int x, int y) =>
        Frame == 6 ? _buttons.PointerMoved(mapped, x, y) : null;

    internal LoginDialogButton? PointerPressed(bool mapped, int x, int y) =>
        Frame == 6 ? _buttons.PointerPressed(mapped, x, y) : null;

    internal bool PointerReleased(bool mapped, int x, int y)
    {
        if (Frame != 6 || _buttons.PointerReleased(mapped, x, y) is not { } button)
        {
            return false;
        }

        _confirmed = IsQuit && button == LoginDialogButton.Ok;
        Frame = 7;
        _buttons.Reset();
        return true;
    }

    /// <returns>Whether quit was confirmed and reached its frame10 action.</returns>
    internal bool Advance(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        if (Frame is 1 or 6)
        {
            return false;
        }

        Frame = (int)Math.Min((long)Frame + frames, Frame < 6 ? 6 : 10);
        if (Frame != 10)
        {
            return false;
        }

        Frame = 1;
        Text = string.Empty;
        return _confirmed;
    }

    internal void FocusLost() => _buttons.Reset();
}
