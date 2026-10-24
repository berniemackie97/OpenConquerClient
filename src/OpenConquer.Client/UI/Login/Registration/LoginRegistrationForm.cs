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

/// <summary>What the player asked the Sign Up dialog to do.</summary>
internal enum LoginRegistrationCommand
{
    None,
    Submit,
    Cancel,
}

/// <summary>
/// The OpenConquer Sign Up dialog: account, password and confirmation fields, Ok and Cancel, and a message.
/// It runs the ten-frame timeline of the native account dialogs and takes input only while held on frame 6.
/// Tab and Shift+Tab move between fields, Enter submits and Escape cancels.
/// </summary>
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

    /// <summary>One-based frame of the dialog timeline; frame 1 shows nothing.</summary>
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

    /// <summary>Changes whenever anything drawn in a field changes.</summary>
    internal int Revision => _revision;
    /// <summary>Changes whenever the characters of a field change.</summary>
    internal int ContentRevision => _contentRevision;
    /// <summary>Changes on every accepted edit, which plays a typing sound.</summary>
    internal int EditRevision => _editRevision;

    internal string AccountName => _fields[(int)LoginRegistrationField.Account].Display;

    internal LoginTextField Field(LoginRegistrationField field) =>
        Enum.IsDefined(field) ? _fields[(int)field] : throw new ArgumentOutOfRangeException(nameof(field));

    /// <summary>Ok and Cancel show no rollover while the request is in flight.</summary>
    internal LoginButtonState ButtonState(LoginDialogButton button) => IsSubmitting ? LoginButtonState.Up : _buttons.StateOf(button);

    /// <summary>Starts showing an empty dialog with the account field focused.</summary>
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

    /// <summary>
    /// Starts hiding the dialog. The passwords are wiped at once; the account name stays until the dialog has
    /// gone, so a created account can still be read.
    /// </summary>
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

        // A dialog still fading in fades out from the same mask strength.
        int held = LoginAccountArtwork.DialogHeldFrame;
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

    /// <summary>Showing stops on the held frame; hiding runs to frame 10, which clears the dialog.</summary>
    internal void Advance(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        if (!IsOpen || IsHolding)
        {
            return;
        }

        int held = LoginAccountArtwork.DialogHeldFrame;
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

    /// <summary>
    /// Shows an error and focuses the field to correct. A password problem clears both password fields, because
    /// masked text cannot be corrected in place; an account problem selects the name so typing replaces it.
    /// </summary>
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

    /// <summary>Locks the dialog while the request is in flight.</summary>
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

    /// <summary>Compares the two password entries in constant time.</summary>
    internal bool PasswordsMatch()
    {
        ReadOnlySpan<char> password = _fields[(int)LoginRegistrationField.Password].Characters;
        ReadOnlySpan<char> confirmation = _fields[(int)LoginRegistrationField.Confirm].Characters;
        return password.Length == confirmation.Length &&
            CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(password), MemoryMarshal.AsBytes(confirmation));
    }

    /// <summary>
    /// Copies the account name and password into new buffers and wipes both password fields. The caller owns and
    /// must zero both buffers.
    /// </summary>
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

    /// <summary>Tracks the pointer and returns the button it just moved onto, if any.</summary>
    internal LoginDialogButton? PointerMoved(bool mapped, int x, int y) =>
        AcceptsInput ? _buttons.PointerMoved(mapped, x, y) : null;

    /// <summary>
    /// A press on Ok or Cancel starts that button; one in a field focuses it and places the caret, measured by
    /// <paramref name="locator"/> when one is available. Returns the button pressed, if any.
    /// </summary>
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

        if (mapped && HitField(x, y) is { } field)
        {
            int caret = locator is not null && locator.TryLocate(this, field, x, out int located) ? located : Field(field).Length;
            bool sameField = Focus == field;
            SetFocus(field);
            MoveCaret(Field(field), caret, extendSelection && sameField);
            _dragging = true;
        }

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
        if (!key.IsRepeat && key.Key == DesktopKey.Escape)
        {
            return LoginRegistrationCommand.Cancel;
        }

        if (!key.IsRepeat && key.Key == DesktopKey.Enter && !shortcut && !alt)
        {
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
            if (!key.IsRepeat)
            {
                int step = (key.Modifiers & DesktopKeyModifiers.Shift) != 0 ? _fields.Length - 1 : 1;
                LoginRegistrationField next = (LoginRegistrationField)(((int)(Focus ?? LoginRegistrationField.Account) + step) % _fields.Length);
                SetFocus(next);
                if (Field(next).SelectAll())
                {
                    _revision++;
                }
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

    string? ILoginTextEditor.SelectedText() => AcceptsInput ? Active?.SelectedText() : null;

    bool ILoginTextEditor.CutSelection()
    {
        if (!AcceptsInput || Active is not { IsMasked: false } field || !field.DeleteSelection())
        {
            return false;
        }

        ChangedText();
        return true;
    }

    /// <summary>Window focus loss releases the buttons and any drag; the entries stay.</summary>
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
        if (change == LoginTextChange.Text)
        {
            ChangedText();
        }
        else if (change == LoginTextChange.Caret)
        {
            _revision++;
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
        if (password || confirmation)
        {
            _contentRevision++;
            _revision++;
        }
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
