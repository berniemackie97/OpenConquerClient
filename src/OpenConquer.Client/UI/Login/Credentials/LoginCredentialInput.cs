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

internal sealed class LoginCredentialInput : ILoginTextEditor, IDisposable
{
    internal const int MaximumAccountLength = 32;
    internal const int MaximumPasswordLength = 15;

    private readonly LoginServerSelection _selection;
    private readonly LoginCredentialLayout _layout;
    private readonly LoginTextField _account = new(MaximumAccountLength, masked: false);
    private readonly LoginTextField _password = new(MaximumPasswordLength, masked: true);
    private LoginServerCatalog? _catalog;
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
    public bool Enabled => !_disposed;
    public bool IsSelecting => _dragging && Focus != LoginCredentialFocus.None;
    public int AccountLength => _account.Length;
    public int PasswordLength => _password.Length;
    public int CaretIndex => Active?.CaretIndex ?? 0;
    public int SelectionStart => Active?.SelectionStart ?? 0;
    public int SelectionEnd => Active?.SelectionEnd ?? 0;
    public bool HasSelection => Active?.HasSelection ?? false;
    public string AccountDisplay => _account.Display;
    public string PasswordMask => _password.Display;

    public LoginCredentialInput(LoginServerSelection selection, LoginCredentialLayout layout)
    {
        ArgumentNullException.ThrowIfNull(selection);
        _selection = selection;
        _layout = layout;
    }

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

        LoginTextField field = Active!;
        MoveTo(field, caretIndex ?? field.Length, extendSelection && sameField);
        _dragging = true;
        return true;
    }

    public void PointerDragged(int caretIndex)
    {
        Synchronize();
        if (IsSelecting && Enabled)
        {
            MoveTo(Active!, caretIndex, extend: true);
        }
    }

    public void PointerReleased()
    {
        _dragging = false;
    }

    public bool KeyPressed(DesktopKeyEvent key)
    {
        Synchronize();
        if (!Enabled || Active is not { } field)
        {
            return false;
        }

        bool shortcut = (key.Modifiers & (DesktopKeyModifiers.Control | DesktopKeyModifiers.Super)) != 0;
        if (shortcut || (key.Modifiers & DesktopKeyModifiers.Alt) != 0)
        {
            if (shortcut && (key.Modifiers & DesktopKeyModifiers.Alt) == 0 && key.Key == DesktopKey.A && !key.IsRepeat &&
                field.SelectAll())
            {
                _revision++;
            }

            return true;
        }
        switch (key.Key)
        {
            case DesktopKey.Escape:
                FocusLost();
                break;
            case DesktopKey.Tab:
                if (!key.IsRepeat && Focus == LoginCredentialFocus.Account)
                {
                    SetFocus(LoginCredentialFocus.Password);
                    if (_password.SelectAll())
                    {
                        _revision++;
                    }
                }
                break;
            case DesktopKey.Enter:
                break;
            default:
                Apply(field.EditKey(key.Key, (key.Modifiers & DesktopKeyModifiers.Shift) != 0));
                break;
        }
        return true;
    }

    public void TextEntered(char character)
    {
        Synchronize();
        if (Enabled && Active is { } field && field.TextEntered(character))
        {
            ChangedText();
        }
    }

    public bool Paste(string? text)
    {
        Synchronize();
        if (!Enabled || Active is not { } field || !field.Paste(text))
        {
            return false;
        }

        ChangedText();
        return true;
    }

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
            _account.Replace(account);
            _contentRevision++;
            _revision++;
        }
        SetFocus(LoginCredentialFocus.Account);
        MoveTo(_account, _account.Length, extend: false);
        _dragging = false;
        return true;
    }

    internal bool TryTakeLoginCredentials(out byte[] accountName, out char[] password)
    {
        Synchronize();
        accountName = [];
        password = [];
        if (_account.Length == 0 || _password.Length is < 1 or > 14)
        {
            return false;
        }

        ReadOnlySpan<char> account = _account.Characters;
        accountName = new byte[account.Length];
        for (int index = 0; index < accountName.Length; index++)
        {
            accountName[index] = checked((byte)account[index]);
        }
        password = _password.Characters.ToArray();
        ClearPassword();
        return true;
    }

    public string? SelectedAccountText()
    {
        Synchronize();
        return Enabled && Focus == LoginCredentialFocus.Account ? _account.SelectedText() : null;
    }

    public bool CutSelectedAccount()
    {
        Synchronize();
        if (!Enabled || Focus != LoginCredentialFocus.Account || !_account.DeleteSelection())
        {
            return false;
        }

        ChangedText();
        return true;
    }

    internal void StopEditingForLogin()
    {
        _dragging = false;
        SetFocus(LoginCredentialFocus.None);
    }

    public void FocusLost()
    {
        if (_disposed)
        {
            return;
        }

        StopEditingForLogin();
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

    bool ILoginTextEditor.HasFocus => Focus != LoginCredentialFocus.None;

    string? ILoginTextEditor.SelectedText()
    {
        return SelectedAccountText();
    }

    bool ILoginTextEditor.CutSelection()
    {
        return CutSelectedAccount();
    }

    private LoginTextField? Active => Focus switch
    {
        LoginCredentialFocus.Account => _account,
        LoginCredentialFocus.Password => _password,
        _ => null,
    };

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

    private void MoveTo(LoginTextField field, int position, bool extend)
    {
        if (field.MoveTo(position, extend))
        {
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
        if (Active is { } field)
        {
            _ = field.MoveTo(field.Length, extend: false);
        }

        _dragging = false;
        _revision++;
    }

    private void ClearAll()
    {
        _ = _account.Clear();
        ClearPassword();
        Focus = LoginCredentialFocus.None;
        _dragging = false;
        _contentRevision++;
        _revision++;
    }

    private void ClearPassword()
    {
        if (_password.Clear())
        {
            _contentRevision++;
            _revision++;
        }
    }

    private void ChangedText()
    {
        if (Focus == LoginCredentialFocus.Account)
        {
            ClearPassword();
        }

        _editRevision++;
        _contentRevision++;
        _revision++;
    }
}
