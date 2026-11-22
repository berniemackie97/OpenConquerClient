using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Content.Login;
using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Login.Registration;

internal enum LoginRegistrationField
{
    Account,
    Password,
    Confirm,
}

internal enum LoginRegistrationCommand
{
    None,
    Submit,
    Cancel,
}

internal sealed class LoginRegistrationForm : ILoginTextEditor, IDisposable
{
    private readonly LoginTextField[] _fields =
    [
        new(LoginRegistrationPolicy.MaximumAccountLength, masked: false),
        new(LoginRegistrationPolicy.MaximumPasswordLength, masked: true),
        new(LoginRegistrationPolicy.MaximumPasswordLength, masked: true),
    ];

    private readonly LoginButtonSet<LoginDialogButton> _buttons;
    private int _revision;
    private int _contentRevision;
    private int _editRevision;
    private bool _dragging;
    private bool _disposed;

    internal LoginRegistrationForm(LoginRegistrationLayout layout)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _buttons = new LoginButtonSet<LoginDialogButton>([(LoginDialogButton.Ok, layout.Ok), (LoginDialogButton.Cancel, layout.Cancel)]);
    }

    internal LoginRegistrationLayout Layout
    {
        get;
    }

    internal int Frame { get; private set; } = 1;
    internal bool IsOpen => Frame != 1;
    internal bool IsHolding => Frame == LoginAccountArtwork.DialogHeldFrame;
    internal bool IsSubmitting
    {
        get; private set;
    }
    internal bool AcceptsInput => IsHolding && !IsSubmitting && !_disposed;
    internal LoginRegistrationField? Focus
    {
        get; private set;
    }
    internal string Message { get; private set; } = string.Empty;
    internal bool MessageIsError
    {
        get; private set;
    }
    internal bool IsSelecting => _dragging && Focus is not null;

    internal int Revision => _revision;
    internal int ContentRevision => _contentRevision;
    internal int EditRevision => _editRevision;

    internal string AccountName => _fields[(int)LoginRegistrationField.Account].Display;

    internal LoginTextField Field(LoginRegistrationField field)
    {
        return Enum.IsDefined(field) ? _fields[(int)field] : throw new ArgumentOutOfRangeException(nameof(field));
    }

    internal LoginButtonState ButtonState(LoginDialogButton button)
    {
        return IsSubmitting ? LoginButtonState.Up : _buttons.StateOf(button);
    }

    internal void Open(string guidance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(guidance);
        ClearFields();
        Message = guidance;
        MessageIsError = false;
        IsSubmitting = false;
        Focus = LoginRegistrationField.Account;
        _buttons.Reset();
        _dragging = false;
        Frame = 2;
        _revision++;
    }

    internal void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        ClearPasswords();
        Focus = null;
        IsSubmitting = false;
        _buttons.Reset();
        _dragging = false;
        _revision++;

        const int held = LoginAccountArtwork.DialogHeldFrame;
        int next = Frame < held ? 2 * held - Frame : Math.Max(Frame, held + 1);
        if (next >= LoginAccountArtwork.DialogFrameCount)
        {
            Hide();
        }
        else
        {
            Frame = next;
        }
    }

    internal void Advance(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        if (!IsOpen || IsHolding)
        {
            return;
        }

        const int held = LoginAccountArtwork.DialogHeldFrame;
        int target = Frame + frames;
        if (Frame < held && target > held)
        {
            target = held;
        }

        if (target >= LoginAccountArtwork.DialogFrameCount)
        {
            Hide();
        }
        else
        {
            Frame = target;
        }
    }

    internal void ShowGuidance(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Message = text;
        MessageIsError = false;
        IsSubmitting = false;
        SetFocus(Focus ?? LoginRegistrationField.Account);
    }

    internal void ShowProblem(string text, LoginRegistrationField field)
    {
        ArgumentNullException.ThrowIfNull(text);
        Message = text;
        MessageIsError = true;
        IsSubmitting = false;
        if (field == LoginRegistrationField.Account)
        {
            SetFocus(LoginRegistrationField.Account);
            if (_fields[(int)LoginRegistrationField.Account].SelectAll())
            {
                _revision++;
            }
        }
        else
        {
            ClearPasswords();
            SetFocus(LoginRegistrationField.Password);
        }
    }

    internal void BeginSubmitting(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Message = text;
        MessageIsError = false;
        IsSubmitting = true;
        SetFocus(null);
        _buttons.Reset();
        _dragging = false;
    }

    internal bool PasswordsMatch()
    {
        ReadOnlySpan<char> password = _fields[(int)LoginRegistrationField.Password].Characters;
        ReadOnlySpan<char> confirmation = _fields[(int)LoginRegistrationField.Confirm].Characters;
        return password.Length == confirmation.Length && CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(password), MemoryMarshal.AsBytes(confirmation));
    }

    internal bool TryTakeCredentials(out byte[] accountName, out char[] password)
    {
        accountName = [];
        password = [];
        ReadOnlySpan<char> name = _fields[(int)LoginRegistrationField.Account].Characters;
        LoginTextField entry = _fields[(int)LoginRegistrationField.Password];
        if (_disposed || name.Length == 0 || entry.Length == 0)
        {
            return false;
        }

        accountName = new byte[name.Length];
        for (int index = 0; index < name.Length; index++)
        {
            accountName[index] = checked((byte)name[index]);
        }

        password = entry.Characters.ToArray();
        ClearPasswords();
        return true;
    }

    internal LoginDialogButton? PointerMoved(bool mapped, int x, int y)
    {
        return AcceptsInput ? _buttons.PointerMoved(mapped, x, y) : null;
    }

    internal LoginDialogButton? PointerPressed(bool mapped, int x, int y, ILoginRegistrationCaretLocator? locator, bool extendSelection)
    {
        if (!AcceptsInput)
        {
            return null;
        }

        if (_buttons.PointerPressed(mapped, x, y) is { } button)
        {
            _dragging = false;
            return button;
        }

        if (!mapped || HitField(x, y) is not { } field)
        {
            return null;
        }

        int caret = locator is not null && locator.TryLocate(this, field, x, out int located) ? located : Field(field).Length;
        bool sameField = Focus == field;
        SetFocus(field);
        MoveCaret(Field(field), caret, extendSelection && sameField);
        _dragging = true;

        return null;
    }

    internal void PointerDragged(int caret)
    {
        if (AcceptsInput && IsSelecting && Focus is { } field)
        {
            MoveCaret(Field(field), caret, extend: true);
        }
    }

    internal LoginRegistrationCommand PointerReleased(bool mapped, int x, int y)
    {
        _dragging = false;
        if (!AcceptsInput || _buttons.PointerReleased(mapped, x, y) is not { } button)
        {
            return LoginRegistrationCommand.None;
        }

        return button == LoginDialogButton.Ok ? LoginRegistrationCommand.Submit : LoginRegistrationCommand.Cancel;
    }

    internal LoginRegistrationCommand KeyPressed(DesktopKeyEvent key)
    {
        if (!AcceptsInput)
        {
            return LoginRegistrationCommand.None;
        }

        bool shortcut = (key.Modifiers & (DesktopKeyModifiers.Control | DesktopKeyModifiers.Super)) != 0;
        bool alt = (key.Modifiers & DesktopKeyModifiers.Alt) != 0;
        switch (key.IsRepeat)
        {
            case false when key.Key == DesktopKey.Escape:
                return LoginRegistrationCommand.Cancel;
            case false when key.Key == DesktopKey.Enter && !shortcut && !alt:
                return LoginRegistrationCommand.Submit;
        }

        if (shortcut || alt)
        {
            if (shortcut && !alt && !key.IsRepeat && key.Key == DesktopKey.A && Active is { } all && all.SelectAll())
            {
                _revision++;
            }

            return LoginRegistrationCommand.None;
        }

        if (key.Key == DesktopKey.Tab)
        {
            if (key.IsRepeat)
            {
                return LoginRegistrationCommand.None;
            }

            int step = (key.Modifiers & DesktopKeyModifiers.Shift) != 0 ? _fields.Length - 1 : 1;
            LoginRegistrationField next = (LoginRegistrationField)(((int)(Focus ?? LoginRegistrationField.Account) + step) % _fields.Length);
            SetFocus(next);
            if (Field(next).SelectAll())
            {
                _revision++;
            }

            return LoginRegistrationCommand.None;
        }

        if (Active is { } field)
        {
            Apply(field.EditKey(key.Key, (key.Modifiers & DesktopKeyModifiers.Shift) != 0));
        }

        return LoginRegistrationCommand.None;
    }

    internal void TextEntered(char character)
    {
        if (AcceptsInput && Active is { } field && field.TextEntered(character))
        {
            ChangedText();
        }
    }

    bool ILoginTextEditor.HasFocus => AcceptsInput && Focus is not null;

    bool ILoginTextEditor.Paste(string? text)
    {
        if (!AcceptsInput || Active is not { } field || !field.Paste(text))
        {
            return false;
        }

        ChangedText();
        return true;
    }

    string? ILoginTextEditor.SelectedText()
    {
        return AcceptsInput ? Active?.SelectedText() : null;
    }

    bool ILoginTextEditor.CutSelection()
    {
        if (!AcceptsInput || Active is not { IsMasked: false } field || !field.DeleteSelection())
        {
            return false;
        }

        ChangedText();
        return true;
    }

    internal void FocusLost()
    {
        _buttons.Reset();
        _dragging = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClearFields();
        Focus = null;
        Frame = 1;
    }

    private LoginTextField? Active => Focus is { } focused ? _fields[(int)focused] : null;

    private LoginRegistrationField? HitField(int x, int y)
    {
        foreach (LoginRegistrationField field in Enum.GetValues<LoginRegistrationField>())
        {
            if (Layout.Box(field).Contains(x, y))
            {
                return field;
            }
        }

        return null;
    }

    private void SetFocus(LoginRegistrationField? field)
    {
        if (Focus == field)
        {
            return;
        }

        Focus = field;
        if (Active is { } active)
        {
            _ = active.MoveTo(active.Length, extend: false);
        }

        _dragging = false;
        _revision++;
    }

    private void MoveCaret(LoginTextField field, int caret, bool extend)
    {
        if (field.MoveTo(caret, extend))
        {
            _revision++;
        }
    }

    private void Apply(LoginTextChange change)
    {
        switch (change)
        {
            case LoginTextChange.Text:
                ChangedText();
                break;
            case LoginTextChange.Caret:
                _revision++;
                break;
            case LoginTextChange.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change), change, null);
        }
    }

    private void ChangedText()
    {
        _editRevision++;
        _contentRevision++;
        _revision++;
    }

    private void ClearPasswords()
    {
        bool password = _fields[(int)LoginRegistrationField.Password].Clear();
        bool confirmation = _fields[(int)LoginRegistrationField.Confirm].Clear();
        if (!password && !confirmation)
        {
            return;
        }

        _contentRevision++;
        _revision++;
    }

    private void ClearFields()
    {
        _ = _fields[(int)LoginRegistrationField.Account].Clear();
        ClearPasswords();
        _contentRevision++;
        _revision++;
    }

    private void Hide()
    {
        ClearFields();
        Focus = null;
        Message = string.Empty;
        MessageIsError = false;
        IsSubmitting = false;
        Frame = 1;
    }
}
