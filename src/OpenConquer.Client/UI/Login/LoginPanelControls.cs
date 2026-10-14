using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Login;

internal enum LoginPanelControl
{
    None,
    Save,
    Clear,
    Accounts,
    Music,
    Account0 = 10,
}

/// <summary>Verified account-list and BGM command surface, with provisional non-retail control chrome.</summary>
internal sealed class LoginPanelControls
{
    private readonly LoginSpritePlacement[] _buttons;
    private readonly LoginSpritePlacement _listBounds;
    private readonly int _rowHeight;
    private readonly LoginLocalPreferencesStore _store;
    private readonly LoginLocalPreferences _preferences;
    private LoginPanelControl _pressed;
    private LoginPanelControl _hovered;
    private bool _expanded;
    private int _selectedIndex;
    private bool _persistenceFailed;

    public bool MusicEnabled => _preferences.MusicEnabled;
    public IReadOnlyList<string> SavedAccounts => _preferences.Accounts;
    public bool ListExpanded => _expanded;
    public LoginPanelControl Hovered => _hovered;
    public bool PersistenceFailed => _persistenceFailed;
    public int VisibleAccountCount => _expanded ? _preferences.Accounts.Count : 0;
    public int SelectedIndex => _selectedIndex;
    public LoginSpritePlacement Bounds(LoginPanelControl control)
    {
        if (control >= LoginPanelControl.Account0)
            return Row((int)control - (int)LoginPanelControl.Account0);
        return control >= LoginPanelControl.Save && control <= LoginPanelControl.Music ? _buttons[(int)control - 1] : default;
    }
    public string Label(LoginPanelControl control) => control switch
    {
        LoginPanelControl.Save => "SAVE",
        LoginPanelControl.Clear => "CLEAR",
        LoginPanelControl.Accounts => "LIST",
        LoginPanelControl.Music => MusicEnabled ? "BGM ON" : "BGM OFF",
        _ => control >= LoginPanelControl.Account0 && (int)control - (int)LoginPanelControl.Account0 < _preferences.Accounts.Count
            ? _preferences.Accounts[(int)control - (int)LoginPanelControl.Account0] : string.Empty,
    };
    public bool IsEnabled(LoginPanelControl control, LoginCredentialInput input) => Enabled(control, input);

    public LoginPanelControls(LoginVisualLayout layout, LoginLocalPreferencesStore store)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _preferences = store.Load();
        _rowHeight = Math.Max(15, Scale(20, layout.Width));
        LoginSpritePlacement panel = layout.AccountPanel;
        _buttons = [
            Place(panel, 362, 36, 52, 21),  // SAVE
            Place(panel, 420, 36, 66, 21),  // CLEAR
            Place(panel, 362, 63, 52, 21),  // LIST
            Place(panel, 420, 63, 66, 21),  // MUSIC
        ];
        _listBounds = new LoginSpritePlacement(_buttons[2].X, _buttons[0].Y - _rowHeight * LoginLocalPreferences.MaximumAccounts,
            Math.Max(Scale(270, layout.Width), _buttons[2].Width + _buttons[3].Width), _rowHeight * LoginLocalPreferences.MaximumAccounts);
        LoginCredentialLayout fields = LoginCredentialLayout.Create(layout);
        foreach (LoginSpritePlacement button in _buttons)
        {
            if (!Contains(panel, button.X, button.Y) || !Contains(panel, button.X + button.Width - 1, button.Y + button.Height - 1) ||
                Overlaps(button, fields.AccountField) || Overlaps(button, fields.PasswordField))
                throw new InvalidOperationException("Login account controls overlap the verified credential layout.");
        }
    }

    public bool PointerMoved(bool mapped, int x, int y, LoginCredentialInput input)
    {
        LoginPanelControl previous = _hovered;
        _hovered = Hit(mapped, x, y);
        return _hovered != LoginPanelControl.None && _hovered != previous && Enabled(_hovered, input);
    }

    public bool PointerPressed(bool mapped, int x, int y, LoginCredentialInput input)
    {
        _pressed = Hit(mapped, x, y);
        if (_pressed == LoginPanelControl.None || !Enabled(_pressed, input))
        {
            _pressed = LoginPanelControl.None;
            _expanded = false;
            return false;
        }
        return true;
    }

    public LoginPanelControl PointerReleased(bool mapped, int x, int y, LoginCredentialInput input)
    {
        LoginPanelControl pressed = _pressed;
        _pressed = LoginPanelControl.None;
        if (pressed == LoginPanelControl.None || pressed != Hit(mapped, x, y) || !Enabled(pressed, input))
            return LoginPanelControl.None;
        Apply(pressed, input);
        return pressed;
    }

    public bool KeyPressed(DesktopKeyEvent key, LoginCredentialInput input)
    {
        if (!_expanded || (key.Modifiers & (DesktopKeyModifiers.Control | DesktopKeyModifiers.Alt | DesktopKeyModifiers.Super)) != 0)
            return false;
        switch (key.Key)
        {
            case DesktopKey.Escape:
                _expanded = false;
                return true;
            case DesktopKey.Up:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                return true;
            case DesktopKey.Down:
                _selectedIndex = Math.Min(_preferences.Accounts.Count - 1, _selectedIndex + 1);
                return true;
            case DesktopKey.Enter:
                if (!key.IsRepeat && _preferences.Accounts.Count > 0)
                    Apply((LoginPanelControl)((int)LoginPanelControl.Account0 + _selectedIndex), input);
                return true;
            default:
                return false;
        }
    }

    public void FocusLost()
    {
        _pressed = _hovered = LoginPanelControl.None;
        _expanded = false;
    }

    private bool Enabled(LoginPanelControl control, LoginCredentialInput input)
    {
        if (control == LoginPanelControl.Music)
            return true;
        if (!input.Enabled)
            return false;
        if (control >= LoginPanelControl.Account0)
            return _expanded && (int)control - (int)LoginPanelControl.Account0 < _preferences.Accounts.Count;
        return control switch
        {
            LoginPanelControl.Save => !_persistenceFailed && input.AccountLength > 0,
            LoginPanelControl.Clear => input.AccountLength > 0,
            LoginPanelControl.Accounts => _preferences.Accounts.Count > 0,
            _ => false,
        };
    }

    private LoginPanelControl Hit(bool mapped, int x, int y)
    {
        if (!mapped)
            return LoginPanelControl.None;
        if (_expanded)
        {
            for (int index = 0; index < _preferences.Accounts.Count; index++)
                if (Contains(Row(index), x, y))
                    return (LoginPanelControl)((int)LoginPanelControl.Account0 + index);
        }
        for (int index = 0; index < _buttons.Length; index++)
            if (Contains(_buttons[index], x, y))
                return (LoginPanelControl)(index + 1);
        return LoginPanelControl.None;
    }

    private void Apply(LoginPanelControl control, LoginCredentialInput input)
    {
        switch (control)
        {
            case LoginPanelControl.Music:
                _preferences.SetMusicEnabled(!_preferences.MusicEnabled);
                Persist();
                break;
            case LoginPanelControl.Save:
                if (_preferences.SaveAccount(input.AccountDisplay))
                    Persist();
                _expanded = false;
                break;
            case LoginPanelControl.Clear:
                _preferences.RemoveAccount(input.AccountDisplay);
                input.ReplaceAccount(string.Empty);
                Persist();
                _expanded = false;
                break;
            case LoginPanelControl.Accounts:
                _expanded = !_expanded;
                _selectedIndex = 0;
                break;
            default:
                if (control >= LoginPanelControl.Account0 && (int)control - (int)LoginPanelControl.Account0 < _preferences.Accounts.Count)
                    input.ReplaceAccount(_preferences.Accounts[(int)control - (int)LoginPanelControl.Account0]);
                _expanded = false;
                break;
        }
    }

    private void Persist()
    {
        if (!_store.TrySave(_preferences) && !_persistenceFailed)
        {
            _persistenceFailed = true;
            Console.Error.WriteLine("OpenConquer: login preferences could not be saved; refusing further account-history writes.");
        }
    }

    private LoginSpritePlacement Row(int index) => new(_listBounds.X, _buttons[0].Y - (index + 1) * _rowHeight, _listBounds.Width, _rowHeight);
    private static LoginSpritePlacement Place(LoginSpritePlacement panel, int x, int y, int width, int height) => new(
        panel.X + Scale(x, panel.Width, 508), panel.Y + Scale(y, panel.Height, 98),
        Scale(width, panel.Width, 508), Scale(height, panel.Height, 98));
    private static int Scale(int value, int width) => checked((int)(((long)value * width + 512) / 1024));
    private static int Scale(int value, int width, int reference) => checked((int)(((long)value * width + reference / 2) / reference));
    private static bool Contains(LoginSpritePlacement rect, int x, int y) => x >= rect.X && y >= rect.Y && (long)x < (long)rect.X + rect.Width && (long)y < (long)rect.Y + rect.Height;
    private static bool Overlaps(LoginSpritePlacement a, LoginSpritePlacement b) =>
        (long)a.X < (long)b.X + b.Width && (long)b.X < (long)a.X + a.Width &&
        (long)a.Y < (long)b.Y + b.Height && (long)b.Y < (long)a.Y + a.Height;
}
