using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Account;

internal enum LoginAccountList
{
    Closed,
    Opening,
    Open,
    Closing,
}

internal enum LoginAccountPressKind
{
    None,
    Absorbed,
    AccountChosen,
    OkPressed,
    CancelPressed,
}

internal readonly record struct LoginAccountPress(LoginAccountPressKind Kind, string? Account = null)
{
    public override string ToString()
    {
        return $"{nameof(LoginAccountPress)} {{ Kind = {Kind} }}";
    }
}

internal enum LoginAccountRelease
{
    None,
    Confirmed,
    Cancelled,
}

internal sealed class LoginAccountControls(LoginAccountLayout layout, LoginAccountHistory history)
{
    private static readonly TimeSpan s_listStep = TimeSpan.FromMilliseconds(20);

    private readonly LoginAccountLayout _layout = layout ?? throw new ArgumentNullException(nameof(layout));
    private readonly LoginButtonSet<LoginDialogButton> _dialogButtons = new([(LoginDialogButton.Ok, layout.Ok), (LoginDialogButton.Cancel, layout.Cancel)]);
    private TimeSpan _listElapsed;

    public LoginAccountHistory History
    {
        get;
    } = history ?? throw new ArgumentNullException(nameof(history));

    public LoginAccountList List
    {
        get; private set;
    }

    public int ListOffset
    {
        get; private set;
    }

    public int HoveredRow { get; private set; } = -1;

    public int VisibleRowCount => Math.Max(1, History.Accounts.Count);

    public LoginAccountDialog? Dialog
    {
        get; private set;
    }

    public int DialogFrame { get; private set; } = 1;

    public string? DialogAccount
    {
        get; private set;
    }

    public bool IsModal => Dialog is not null;

    public bool CapturesPointer => IsModal || List != LoginAccountList.Closed;

    public LoginButtonState DialogButtonState(LoginDialogButton button)
    {
        return _dialogButtons.StateOf(button);
    }

    public LoginRect RowBounds(int row)
    {
        return _layout.Row(row, ListOffset);
    }

    public void ToggleList()
    {
        switch (List)
        {
            case LoginAccountList.Closed:
                List = LoginAccountList.Opening;
                ListOffset = 0;
                _listElapsed = TimeSpan.Zero;
                break;
            case LoginAccountList.Open:
                CloseList();
                break;
            case LoginAccountList.Opening:
            case LoginAccountList.Closing:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public bool TryOpenDialog(LoginAccountDialog dialog, string account)
    {
        ArgumentNullException.ThrowIfNull(account);

        bool allowed = dialog == LoginAccountDialog.Save ? LoginAccountHistory.IsValidName(account) : History.Contains(account);
        if (IsModal || !History.Writable || !allowed)
        {
            return false;
        }

        Dialog = dialog;
        DialogFrame = 2;
        DialogAccount = account;
        _dialogButtons.Reset();
        return true;
    }

    public LoginDialogButton? PointerMoved(bool mapped, int x, int y)
    {
        HoveredRow = List == LoginAccountList.Open && mapped ? HitRow(x, y) : -1;
        return IsHolding ? _dialogButtons.PointerMoved(mapped, x, y) : null;
    }

    public LoginAccountPress PointerPressed(bool mapped, int x, int y)
    {
        if (IsModal)
        {
            return IsHolding && _dialogButtons.PointerPressed(mapped, x, y) is { } button
                ? new LoginAccountPress(button == LoginDialogButton.Ok ? LoginAccountPressKind.OkPressed : LoginAccountPressKind.CancelPressed)
                : new LoginAccountPress(LoginAccountPressKind.Absorbed);
        }

        switch (List)
        {
            case LoginAccountList.Closed:
                return new LoginAccountPress(LoginAccountPressKind.None);
            case LoginAccountList.Open:
                int row = mapped ? HitRow(x, y) : -1;
                CloseList();
                return row >= 0
                    ? new LoginAccountPress(LoginAccountPressKind.AccountChosen, History.Accounts[row])
                    : new LoginAccountPress(LoginAccountPressKind.Absorbed);
            case LoginAccountList.Opening:
            case LoginAccountList.Closing:
            default:
                return new LoginAccountPress(LoginAccountPressKind.Absorbed);
        }
    }

    public LoginAccountRelease PointerReleased(bool mapped, int x, int y)
    {
        if (!IsHolding || _dialogButtons.PointerReleased(mapped, x, y) is not { } button)
        {
            return LoginAccountRelease.None;
        }

        if (button == LoginDialogButton.Ok && DialogAccount is { } account)
        {
            _ = Dialog == LoginAccountDialog.Save ? History.TryRecord(account) : History.TryRemove(account);
        }

        DialogFrame = LoginAccountArtwork.DialogHeldFrame + 1;
        return button == LoginDialogButton.Ok ? LoginAccountRelease.Confirmed : LoginAccountRelease.Cancelled;
    }

    public void Advance(TimeSpan step, int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);

        if (IsModal && !IsHolding)
        {
            int target = DialogFrame + frames;
            if (DialogFrame < LoginAccountArtwork.DialogHeldFrame && target > LoginAccountArtwork.DialogHeldFrame)
            {
                target = LoginAccountArtwork.DialogHeldFrame;
            }

            if (target >= LoginAccountArtwork.DialogFrameCount)
            {
                Dialog = null;
                DialogFrame = 1;
                DialogAccount = null;
            }
            else
            {
                DialogFrame = target;
            }
        }

        if (List is LoginAccountList.Opening or LoginAccountList.Closing)
        {
            SlideList(step);
        }
    }

    public void FocusLost()
    {
        _dialogButtons.Reset();
        HoveredRow = -1;
        if (List == LoginAccountList.Open)
        {
            CloseList();
        }
    }

    private bool IsHolding => IsModal && DialogFrame == LoginAccountArtwork.DialogHeldFrame;

    private void SlideList(TimeSpan step)
    {
        int end = -LoginAccountLayout.RowHeight * VisibleRowCount;
        int distance = 10 + 2 * VisibleRowCount;

        _listElapsed += step;
        while (_listElapsed >= s_listStep)
        {
            _listElapsed -= s_listStep;
            if (List == LoginAccountList.Opening)
            {
                ListOffset = Math.Max(end, ListOffset - distance);
                if (ListOffset != end)
                {
                    continue;
                }

                List = LoginAccountList.Open;
                return;
            }
            else
            {
                ListOffset = Math.Min(0, ListOffset + distance);
                if (ListOffset != 0)
                {
                    continue;
                }

                List = LoginAccountList.Closed;
                return;
            }
        }
    }

    private void CloseList()
    {
        List = LoginAccountList.Closing;
        HoveredRow = -1;
        _listElapsed = TimeSpan.Zero;
    }

    private int HitRow(int x, int y)
    {
        for (int row = 0; row < History.Accounts.Count; row++)
        {
            if (RowBounds(row).Contains(x, y))
            {
                return row;
            }
        }

        return -1;
    }
}
