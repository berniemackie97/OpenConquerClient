using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Login.Credentials;

internal enum LoginCredentialFocus
{
    None,
    Account,
    Password,
}

/// <summary>
/// Account and password editor for ASCII input, with length limits. Rendering and clipboard code never see
/// password characters.
/// </summary>
internal sealed class LoginCredentialInput : IDisposable
{
    internal const int MaximumAccountLength = 32;
    internal const int MaximumPasswordLength = 15; // start.dat text field 364 maxLength

    private readonly LoginServerSelection _selection;
    private readonly LoginCredentialLayout _layout;
    private readonly char[] _account = new char[MaximumAccountLength];
    private readonly char[] _password = new char[MaximumPasswordLength];
    private LoginServerCatalog? _catalog;
    private int _accountLength;
    private int _passwordLength;
    private int _caret;
    private int _anchor;
    private int _revision;
    private int _editRevision;
    private int _contentRevision;
    private bool _dragging;
    private bool _disposed;

    public LoginCredentialFocus Focus
    {
        get; private set;
    }
    public int Revision => _revision;
    public int EditRevision => _editRevision;
    public int ContentRevision => _contentRevision;
    public bool Enabled => !_disposed; // Editing never depends on authenticated realm selection.
    public bool IsSelecting => _dragging && Focus != LoginCredentialFocus.None;
    public int AccountLength => _accountLength;
    public int PasswordLength => _passwordLength;
    public int CaretIndex => _caret;
    public int SelectionStart => Math.Min(_anchor, _caret);
    public int SelectionEnd => Math.Max(_anchor, _caret);
    public bool HasSelection => _anchor != _caret;
    public string AccountDisplay => new(_account, 0, _accountLength);
    public string PasswordMask => new('*', _passwordLength);

    public LoginCredentialInput(LoginServerSelection selection, LoginCredentialLayout layout)
    {
        ArgumentNullException.ThrowIfNull(selection);
        _selection = selection;
        _layout = layout;
    }

    /// <summary>Authority changes revoke any cached password; realm navigation never disables account editing.</summary>
    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LoginServerCatalog catalog = _selection.Catalog;
        if (ReferenceEquals(_catalog, catalog))
        {
            return;
        }

        _catalog = catalog;
        ClearPassword();
    }

    public bool PointerPressed(bool mapped, int x, int y, int? caretIndex = null, bool extendSelection = false)
    {
        Synchronize();
        LoginCredentialFocus target = !Enabled || !mapped ? LoginCredentialFocus.None :
            _layout.AccountField.Contains(x, y) ? LoginCredentialFocus.Account :
            _layout.PasswordField.Contains(x, y) ? LoginCredentialFocus.Password : LoginCredentialFocus.None;
        if (target == LoginCredentialFocus.None)
        {
            SetFocus(LoginCredentialFocus.None);
            ClearPassword();
            return false;
        }
        bool sameField = target == Focus;
        if (!sameField)
        {
            SetFocus(target);
        }

        MoveTo(caretIndex ?? ActiveLength, extendSelection && sameField);
        _dragging = true;
        return true;
    }

    public void PointerDragged(int caretIndex)
    {
        Synchronize();
        if (IsSelecting && Enabled)
        {
            MoveTo(caretIndex, extend: true);
        }
    }

    public void PointerReleased() => _dragging = false;

    /// <summary>Consumes focused command keys; Enter never authorizes a connection.</summary>
    public bool KeyPressed(DesktopKeyEvent key)
    {
        Synchronize();
        if (!Enabled || Focus == LoginCredentialFocus.None)
        {
            return false;
        }

        bool shortcut = (key.Modifiers & (DesktopKeyModifiers.Control | DesktopKeyModifiers.Super)) != 0;
        if (shortcut || (key.Modifiers & DesktopKeyModifiers.Alt) != 0)
        {
            if (shortcut && (key.Modifiers & DesktopKeyModifiers.Alt) == 0 && key.Key == DesktopKey.A && !key.IsRepeat)
            {
                MoveTo(ActiveLength, extend: false, anchor: 0);
            }

            return true;
        }
        bool shift = (key.Modifiers & DesktopKeyModifiers.Shift) != 0;
        switch (key.Key)
        {
            case DesktopKey.Escape:
                FocusLost();
                break;
            case DesktopKey.Tab:
                // Retail VAR_FOCUS=1 advances to password; VAR_FOCUS=2 asks for 3 (ignored).
                // Shift is not consulted by the retail host's Tab bridge.
                if (!key.IsRepeat && Focus == LoginCredentialFocus.Account)
                {
                    SetFocus(LoginCredentialFocus.Password);
                    // Retail selects the existing password when Tab moves focus from account.
                    // A further Tab (including Shift+Tab) in password is ignored.
                    MoveTo(_passwordLength, extend: false, anchor: 0);
                }
                break;
            case DesktopKey.Enter:
                // Retail sends a synthetic Enter-button press here; focus stays where it is.
                // Sending credentials to the account server is left out on purpose.
                break;
            case DesktopKey.Left:
                MoveTo(!shift && HasSelection ? SelectionStart : _caret - 1, shift);
                break;
            case DesktopKey.Right:
                MoveTo(!shift && HasSelection ? SelectionEnd : _caret + 1, shift);
                break;
            case DesktopKey.Home:
                MoveTo(0, shift);
                break;
            case DesktopKey.End:
                MoveTo(ActiveLength, shift);
                break;
            case DesktopKey.Backspace:
                if (!DeleteSelection() && _caret > 0)
                {
                    Remove(_caret - 1, 1);
                }

                break;
            case DesktopKey.Delete:
                if (!DeleteSelection() && _caret < ActiveLength)
                {
                    Remove(_caret, 1);
                }

                break;
        }
        return true;
    }

    public void TextEntered(char character)
    {
        Synchronize();
        if (Enabled && Focus != LoginCredentialFocus.None && IsSupportedCharacter(character))
        {
            Insert(stackalloc char[] { character });
        }
    }

    /// <summary>All-or-nothing paste; unsupported controls, Unicode and oversized data are rejected.</summary>
    public bool Paste(string? text)
    {
        Synchronize();
        if (!Enabled || Focus == LoginCredentialFocus.None || string.IsNullOrEmpty(text) || text.Length > ActiveBuffer.Length)
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!IsSupportedCharacter(character))
            {
                return false;
            }
        }

        return Insert(text.AsSpan());
    }

    /// <summary>Applies a retail history choice, clearing secrets only when the account actually changes.</summary>
    public bool ReplaceAccount(string account)
    {
        ArgumentNullException.ThrowIfNull(account);
        Synchronize();
        if (!Enabled || !LoginAccountHistory.IsValidName(account))
        {
            return false;
        }

        if (!string.Equals(AccountDisplay, account, StringComparison.Ordinal))
        {
            ClearPassword();
            Array.Clear(_account);
            account.AsSpan().CopyTo(_account);
            _accountLength = account.Length;
            _contentRevision++;
            _revision++;
        }
        SetFocus(LoginCredentialFocus.Account);
        MoveTo(_accountLength, extend: false);
        _dragging = false;
        return true;
    }

    /// <summary>
    /// Takes independent, short-lived credential buffers for one login attempt.
    /// The caller must zero both buffers. Taking the password immediately erases
    /// the editor copy so no concurrent render/input path can retain a secret.
    /// </summary>
    internal bool TryTakeLoginCredentials(out byte[] accountName, out char[] password)
    {
        Synchronize();
        accountName = [];
        password = [];
        if (_accountLength == 0 || _passwordLength is < 1 or > 14)
        {
            return false;
        }

        accountName = Encoding.ASCII.GetBytes(_account.AsSpan(0, _accountLength));
        password = _password.AsSpan(0, _passwordLength).ToArray();
        ClearPassword();
        return true;
    }

    /// <summary>Returns account selection only. Password content is never materialized as a string.</summary>
    public string? SelectedAccountText()
    {
        Synchronize();
        return Enabled && Focus == LoginCredentialFocus.Account && HasSelection ? new string(_account, SelectionStart, SelectionEnd - SelectionStart) : null;
    }

    public bool CutSelectedAccount()
    {
        Synchronize();
        return Enabled && Focus == LoginCredentialFocus.Account && DeleteSelection();
    }

    public void FocusLost()
    {
        if (_disposed)
        {
            return;
        }

        _dragging = false;
        SetFocus(LoginCredentialFocus.None);
        ClearPassword();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ClearAll();
        _disposed = true;
    }

    private int ActiveLength => Focus == LoginCredentialFocus.Account ? _accountLength : _passwordLength;
    private Span<char> ActiveBuffer => Focus == LoginCredentialFocus.Account ? _account : _password;

    private bool Insert(ReadOnlySpan<char> characters)
    {
        int start = SelectionStart;
        int end = SelectionEnd;
        int length = ActiveLength;
        int newLength = length - (end - start) + characters.Length;
        Span<char> buffer = ActiveBuffer;
        if (newLength > buffer.Length)
        {
            return false;
        }

        buffer.Slice(end, length - end).CopyTo(buffer[(start + characters.Length)..]);
        characters.CopyTo(buffer[start..]);
        buffer[newLength..].Clear();
        SetActiveLength(newLength);
        _caret = _anchor = start + characters.Length;
        ChangedText();
        return true;
    }

    private bool DeleteSelection()
    {
        if (!HasSelection)
        {
            return false;
        }

        Remove(SelectionStart, SelectionEnd - SelectionStart);
        return true;
    }

    private void Remove(int start, int count)
    {
        int length = ActiveLength;
        Span<char> buffer = ActiveBuffer;
        buffer.Slice(start + count, length - start - count).CopyTo(buffer[start..]);
        buffer[(length - count)..].Clear();
        SetActiveLength(length - count);
        _caret = _anchor = start;
        ChangedText();
    }

    private void SetActiveLength(int length)
    {
        if (Focus == LoginCredentialFocus.Account)
        {
            _accountLength = length;
        }
        else
        {
            _passwordLength = length;
        }
    }

    private void MoveTo(int position, bool extend, int? anchor = null)
    {
        int target = Math.Clamp(position, 0, ActiveLength);
        int newAnchor = anchor ?? (extend ? _anchor : target);
        if (target != _caret || newAnchor != _anchor)
        {
            _caret = target;
            _anchor = newAnchor;
            _revision++;
        }
    }

    private void SetFocus(LoginCredentialFocus target)
    {
        if (Focus == target)
        {
            return;
        }

        Focus = target;
        _caret = _anchor = target == LoginCredentialFocus.None ? 0 : ActiveLength;
        _dragging = false;
        _revision++;
    }

    private void ClearAll()
    {
        Array.Clear(_account);
        _accountLength = 0;
        ClearPassword();
        Focus = LoginCredentialFocus.None;
        _caret = _anchor = 0;
        _dragging = false;
        _contentRevision++;
        _revision++;
    }

    private void ClearPassword()
    {
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(_password.AsSpan()));
        if (_passwordLength != 0)
        {
            _passwordLength = 0;
            _contentRevision++;
            _revision++;
        }
        if (Focus == LoginCredentialFocus.Password)
        {
            MoveTo(0, extend: false);
        }
    }

    private void ChangedText()
    {
        // Retail account-name mutations invalidate the accompanying password.
        if (Focus == LoginCredentialFocus.Account)
        {
            ClearPassword();
        }

        _editRevision++;
        _contentRevision++;
        _revision++;
    }

    private static bool IsSupportedCharacter(char value) => LoginAccountHistory.IsAccountCharacter(value);
}
