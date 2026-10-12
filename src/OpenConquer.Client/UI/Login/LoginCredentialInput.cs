using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Login;

internal enum LoginCredentialFocus
{
    None,
    Account,
    Password,
}

/// <summary>Owns ephemeral, bounded account input; never submits credentials or authorizes a connection.</summary>
internal sealed class LoginCredentialInput : IDisposable
{
    internal const int MaximumAccountLength = 32;
    internal const int MaximumPasswordLength = 32;

    private readonly LoginServerSelection _selection;
    private readonly LoginCredentialLayout _layout;
    private readonly char[] _account = new char[MaximumAccountLength];
    private readonly char[] _password = new char[MaximumPasswordLength];
    private LoginServerCatalog? _catalog;
    private LoginServer? _server;
    private int _accountLength;
    private int _passwordLength;
    private int _revision;
    private bool _disposed;

    public LoginCredentialFocus Focus
    {
        get; private set;
    }
    public int Revision => _revision;
    public bool Enabled => !_disposed && _selection.SelectedServer is not null;
    public int AccountLength => _accountLength;
    public int PasswordLength => _passwordLength;
    public string AccountDisplay => new(_account, 0, _accountLength);
    public string PasswordMask => new('*', _passwordLength);

    public LoginCredentialInput(LoginServerSelection selection, LoginCredentialLayout layout)
    {
        ArgumentNullException.ThrowIfNull(selection);
        _selection = selection;
        _layout = layout;
    }

    /// <summary>Immediately discards captured data when the signed catalog or selected server changes.</summary>
    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LoginServerCatalog catalog = _selection.Catalog;
        LoginServer? server = _selection.SelectedServer;

        if (ReferenceEquals(_catalog, catalog) && ReferenceEquals(_server, server))
        {
            return;
        }

        ClearAll();
        _catalog = catalog;
        _server = server;
    }

    /// <summary>Returns whether the press was inside an enabled credential field.</summary>
    public bool PointerPressed(bool mapped, int x, int y)
    {
        Synchronize();
        LoginCredentialFocus target = !Enabled || !mapped ? LoginCredentialFocus.None :
            Contains(_layout.AccountField, x, y) ? LoginCredentialFocus.Account :
            Contains(_layout.PasswordField, x, y) ? LoginCredentialFocus.Password : LoginCredentialFocus.None;

        if (target != Focus)
        {
            Focus = target;
            _revision++;
        }

        if (target == LoginCredentialFocus.None)
        {
            ClearPassword();
        }

        return target != LoginCredentialFocus.None;
    }

    /// <summary>Returns true when a focused field owns this key; Enter deliberately never submits.</summary>
    public bool KeyPressed(DesktopKeyEvent key)
    {
        Synchronize();

        if (Focus == LoginCredentialFocus.None)
        {
            return false;
        }

        if ((key.Modifiers & (DesktopKeyModifiers.Control | DesktopKeyModifiers.Alt | DesktopKeyModifiers.Super)) != 0)
        {
            return true;
        }

        switch (key.Key)
        {
            case DesktopKey.Escape:
                FocusLost();
                break;
            case DesktopKey.Tab:
                if (!key.IsRepeat)
                {
                    Focus = Focus == LoginCredentialFocus.Account ? LoginCredentialFocus.Password : LoginCredentialFocus.Account;
                    _revision++;
                }
                break;
            case DesktopKey.Enter:
                if (!key.IsRepeat && Focus == LoginCredentialFocus.Account)
                {
                    Focus = LoginCredentialFocus.Password;
                    _revision++;
                }
                break;
            case DesktopKey.Backspace:
                if (Focus == LoginCredentialFocus.Account && _accountLength > 0)
                {
                    _account[--_accountLength] = '\0';
                    _revision++;
                }
                else if (Focus == LoginCredentialFocus.Password && _passwordLength > 0)
                {
                    _password[--_passwordLength] = '\0';
                    _revision++;
                }
                break;
            case DesktopKey.Delete:
                if (Focus == LoginCredentialFocus.Account)
                {
                    Array.Clear(_account);
                    _accountLength = 0;
                    _revision++;
                }
                else
                {
                    ClearPassword();
                }
                break;
        }

        return true;
    }

    public void TextEntered(char character)
    {
        Synchronize();

        if (!Enabled || Focus == LoginCredentialFocus.None || !IsSupportedCharacter(character))
        {
            return;
        }

        if (Focus == LoginCredentialFocus.Account && _accountLength < _account.Length)
        {
            _account[_accountLength++] = character;
            _revision++;
        }
        else if (Focus == LoginCredentialFocus.Password && _passwordLength < _password.Length)
        {
            _password[_passwordLength++] = character;
            _revision++;
        }
    }

    public void FocusLost()
    {
        if (_disposed)
        {
            return;
        }

        if (Focus != LoginCredentialFocus.None)
        {
            Focus = LoginCredentialFocus.None;
            _revision++;
        }

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

    private void ClearAll()
    {
        Array.Clear(_account);
        _accountLength = 0;
        ClearPassword();
        Focus = LoginCredentialFocus.None;
        _revision++;
    }

    private void ClearPassword()
    {
        // Never store the unmasked password in a managed string, log, renderer, or settings file.
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(_password.AsSpan()));
        if (_passwordLength != 0)
        {
            _passwordLength = 0;
            _revision++;
        }
    }

    private static bool IsSupportedCharacter(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '.' or '-' or '@' or '>' or '_';

    private static bool Contains(LoginSpritePlacement rect, int x, int y) =>
        x >= rect.X && y >= rect.Y && (long)x < (long)rect.X + rect.Width && (long)y < (long)rect.Y + rect.Height;
}
