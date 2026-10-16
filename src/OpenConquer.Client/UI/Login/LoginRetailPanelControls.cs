using OpenConquer.Content.Startup;
using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Login;

internal enum LoginRetailDialog
{
    None, Save, Clear
}
internal enum LoginAccountPopupState
{
    Closed, Opening, Open, Closing
}
internal enum LoginRetailPanelEvent
{
    None, Opened, Cancelled, Saved, Cleared, Selected
}

/// <summary>Retail 5517 account controls. Owns pointer capture, confirmation, and 20-ms list movement.</summary>
internal sealed class LoginRetailPanelControls
{
    private static readonly TimeSpan s_listTick = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan s_dialogTick = TimeSpan.FromSeconds(1d / 30d);
    private readonly LoginRetailFlashLayout _layout;
    private readonly LoginAccountHistory _history;
    private readonly int _stageWidth;
    private LoginRetailFlashArtwork.Button? _hover;
    private LoginRetailFlashArtwork.Button? _pressed;
    private int _pressedDialog = -1;
    private int _hoveredDialog = -1;
    private int _popupOffset;
    private int _hoveredRow = -1;
    private double _animationElapsed;
    private string? _dialogAccount;
    private bool _accountSelectedOnPress;

    public LoginRetailDialog Dialog
    {
        get; private set;
    }
    public int DialogFrame { get; private set; } = 1;
    public LoginAccountPopupState Popup
    {
        get; private set;
    }
    public int PopupOffset => _popupOffset;
    public int VisibleRowCount => Math.Max(1, _history.Accounts.Count);
    public bool AccountSelectedOnPress => _accountSelectedOnPress;
    public int HoveredRow => _hoveredRow;
    public IReadOnlyList<string> SavedAccounts => _history.Accounts;
    public string? DialogAccount => _dialogAccount;
    public bool IsModal => Dialog != LoginRetailDialog.None;
    public bool CapturesPointer => IsModal || Popup != LoginAccountPopupState.Closed || _pressed is not null;

    public LoginRetailPanelControls(LoginRetailFlashLayout layout, LoginAccountHistory history, int stageWidth)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stageWidth);
        _stageWidth = stageWidth;
    }

    public LoginRetailFlashArtwork.State DialogButtonState(int button)
    {
        if (button is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(button));
        }

        if (_pressedDialog == button && _hoveredDialog == button)
        {
            return LoginRetailFlashArtwork.State.Down;
        }

        return _hoveredDialog == button ? LoginRetailFlashArtwork.State.Over : LoginRetailFlashArtwork.State.Up;
    }

    public LoginRetailFlashArtwork.State ButtonState(LoginRetailFlashArtwork.Button button)
    {
        if (_pressed == button && _hover == button)
        {
            return LoginRetailFlashArtwork.State.Down;
        }

        return _hover == button ? LoginRetailFlashArtwork.State.Over : LoginRetailFlashArtwork.State.Up;
    }

    public bool PointerMoved(bool mapped, int x, int y)
    {
        LoginRetailFlashArtwork.Button? previous = _hover;
        _hoveredDialog = IsModal && DialogFrame == 6 && mapped ? DialogHit(x, y) : -1;
        _hover = !IsModal && mapped ? HitButton(x, y) : null;
        _hoveredRow = Popup == LoginAccountPopupState.Open && mapped ? HitRow(x, y) : -1;
        return _hover != previous && _hover is not null;
    }

    public bool PointerPressed(bool mapped, int x, int y, LoginCredentialInput credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        _pressed = null;
        _pressedDialog = -1;
        _accountSelectedOnPress = false;
        if (IsModal)
        {
            if (DialogFrame == 6 && mapped)
            {
                _pressedDialog = DialogHit(x, y);
                _hoveredDialog = _pressedDialog;
            }
            return true;
        }
        if (Popup == LoginAccountPopupState.Open)
        {
            int index = mapped ? HitRow(x, y) : -1;
            if (index >= 0)
            {
                string name = _history.Accounts[index];
                if (credentials.ReplaceAccount(name))
                {
                    _accountSelectedOnPress = true;
                    ClosePopup();
                }
            }
            else
            {
                ClosePopup();
            }

            return true;
        }
        if (Popup != LoginAccountPopupState.Closed)
        {
            return true;
        }

        if (!mapped)
        {
            return false;
        }

        LoginRetailFlashArtwork.Button? button = HitButton(x, y);
        if (button is null)
        {
            return false;
        }

        _pressed = button;
        _hover = button;
        return true;
    }

    public LoginRetailPanelEvent PointerReleased(bool mapped, int x, int y, LoginCredentialInput credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (IsModal)
        {
            int selected = _pressedDialog;
            _pressedDialog = -1;
            if (DialogFrame != 6 || !mapped || selected < 0 || selected != DialogHit(x, y))
            {
                return LoginRetailPanelEvent.None;
            }

            if (selected == 1)
            {
                BeginDialogClose();
                return LoginRetailPanelEvent.Cancelled;
            }
            string? account = _dialogAccount;
            bool changed = account is not null && (Dialog == LoginRetailDialog.Save ? _history.TryRecord(account) : _history.TryRemove(account));
            LoginRetailDialog kind = Dialog;
            BeginDialogClose();
            return !changed ? LoginRetailPanelEvent.None : kind == LoginRetailDialog.Save ? LoginRetailPanelEvent.Saved : LoginRetailPanelEvent.Cleared;
        }
        LoginRetailFlashArtwork.Button? pressed = _pressed;
        _pressed = null;
        if (pressed is null || !mapped || pressed != HitButton(x, y))
        {
            return LoginRetailPanelEvent.None;
        }

        switch (pressed.Value)
        {
            case LoginRetailFlashArtwork.Button.Accounts:
                if (Popup == LoginAccountPopupState.Closed)
                {
                    OpenPopup();
                }
                else if (Popup == LoginAccountPopupState.Open)
                {
                    ClosePopup();
                }

                return LoginRetailPanelEvent.Opened;
            case LoginRetailFlashArtwork.Button.Save:
                if (LoginAccountHistory.IsValidName(credentials.AccountDisplay) && _history.Writable)
                {
                    OpenDialog(LoginRetailDialog.Save, credentials.AccountDisplay);
                }

                return IsModal ? LoginRetailPanelEvent.Opened : LoginRetailPanelEvent.None;
            case LoginRetailFlashArtwork.Button.Clear:
                if (_history.Contains(credentials.AccountDisplay) && _history.Writable)
                {
                    OpenDialog(LoginRetailDialog.Clear, credentials.AccountDisplay);
                }

                return IsModal ? LoginRetailPanelEvent.Opened : LoginRetailPanelEvent.None;
            default:
                // Publisher-era URLs are deliberately disabled pending a trusted allowlist.
                return LoginRetailPanelEvent.None;
        }
    }

    public bool KeyPressed(DesktopKeyEvent key)
    {
        if (IsModal)
        {
            if (key.Key == DesktopKey.Escape && !key.IsRepeat && DialogFrame == 6)
            {
                BeginDialogClose();
            }

            return true;
        }
        if (Popup != LoginAccountPopupState.Closed)
        {
            if (key.Key == DesktopKey.Escape && !key.IsRepeat)
            {
                ClosePopup();
            }

            return true;
        }
        return false;
    }

    public void FocusLost()
    {
        _pressed = null;
        _pressedDialog = -1;
        _hoveredDialog = -1;
        _hover = null;
        _hoveredRow = -1;
        if (Popup == LoginAccountPopupState.Open)
        {
            ClosePopup();
        }
    }

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        if (Dialog != LoginRetailDialog.None)
        {
            if (DialogFrame == 6)
            {
                return;
            }

            _animationElapsed += Math.Min(elapsed.TotalSeconds, 0.25);
            while (_animationElapsed >= s_dialogTick.TotalSeconds && Dialog != LoginRetailDialog.None)
            {
                _animationElapsed -= s_dialogTick.TotalSeconds;
                DialogFrame++;
                if (DialogFrame >= 10)
                {
                    Dialog = LoginRetailDialog.None;
                    DialogFrame = 1;
                    _dialogAccount = null;
                    _animationElapsed = 0;
                }
                else if (DialogFrame == 6)
                {
                    _animationElapsed = 0;
                }
            }
            return;
        }
        if (Popup is not (LoginAccountPopupState.Opening or LoginAccountPopupState.Closing))
        {
            return;
        }

        _animationElapsed += Math.Min(elapsed.TotalSeconds, 0.25);
        int end = VisibleRowCount * 25;
        int step = 10 + 2 * VisibleRowCount;
        while (_animationElapsed >= s_listTick.TotalSeconds)
        {
            _animationElapsed -= s_listTick.TotalSeconds;
            _popupOffset += Popup == LoginAccountPopupState.Opening ? -step : step;
            if (Popup == LoginAccountPopupState.Opening && _popupOffset <= -end)
            {
                _popupOffset = -end;
                Popup = LoginAccountPopupState.Open;
                _animationElapsed = 0;
                break;
            }
            if (Popup == LoginAccountPopupState.Closing && _popupOffset >= 0)
            {
                _popupOffset = 0;
                Popup = LoginAccountPopupState.Closed;
                _animationElapsed = 0;
                break;
            }
        }
    }

    public LoginSpritePlacement RowBounds(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, VisibleRowCount);
        // start.dat sprite 360: (451.5,631.4), child itemBtn sprite 6 at 25*i.
        return new LoginSpritePlacement(Scale(452), Scale(631 + _popupOffset + 25 * index), Scale(166), Scale(25));
    }

    private int HitRow(int x, int y)
    {
        for (int index = 0; index < _history.Accounts.Count; index++)
            if (Contains(RowBounds(index), x, y))
            {
                return index;
            }

        return -1;
    }

    private LoginRetailFlashArtwork.Button? HitButton(int x, int y)
    {
        foreach (LoginRetailFlashArtwork.Button button in Enum.GetValues<LoginRetailFlashArtwork.Button>())
            if (Contains(_layout.ButtonAt(button), x, y))
            {
                return button;
            }

        return null;
    }

    private int DialogHit(int x, int y)
    {
        if (Contains(Rect(381, 415, 96, 26), x, y))
        {
            return 0;
        }

        if (Contains(Rect(541, 415, 96, 26), x, y))
        {
            return 1;
        }

        return -1;
    }

    private void OpenDialog(LoginRetailDialog kind, string account)
    {
        _pressedDialog = -1;
        _hoveredDialog = -1;
        Dialog = kind;
        DialogFrame = 2;
        _dialogAccount = account;
        _animationElapsed = 0;
    }

    private void BeginDialogClose()
    {
        if (DialogFrame != 6)
        {
            return;
        }

        DialogFrame = 7;
        _pressedDialog = -1;
        _hoveredDialog = -1;
        _animationElapsed = 0;
    }

    private void OpenPopup()
    {
        _popupOffset = 0;
        _animationElapsed = 0;
        Popup = LoginAccountPopupState.Opening;
    }

    private void ClosePopup()
    {
        if (Popup == LoginAccountPopupState.Closed || Popup == LoginAccountPopupState.Closing)
        {
            return;
        }

        Popup = LoginAccountPopupState.Closing;
        _hoveredRow = -1;
        _animationElapsed = 0;
    }

    private LoginSpritePlacement Rect(int x, int y, int width, int height) => new(Scale(x), Scale(y), Scale(width), Scale(height));
    private int Scale(int value) => checked((int)((value * (long)_stageWidth + 512) / 1024));
    private static bool Contains(LoginSpritePlacement rect, int x, int y) =>
        x >= rect.X && y >= rect.Y && (long)x < (long)rect.X + rect.Width && (long)y < (long)rect.Y + rect.Height;
}
