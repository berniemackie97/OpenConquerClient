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
    /// <summary>The press is not for the account controls.</summary>
    None,
    /// <summary>The press was taken by the list or a dialog without further effect.</summary>
    Absorbed,
    /// <summary>A saved account was chosen from the list.</summary>
    AccountChosen,
    OkPressed,
    CancelPressed,
}

internal readonly record struct LoginAccountPress(LoginAccountPressKind Kind, string? Account = null);

internal enum LoginAccountRelease
{
    None,
    /// <summary>Ok was clicked: the account was saved or cleared and the dialog started closing.</summary>
    Confirmed,
    /// <summary>Cancel was clicked and the dialog started closing.</summary>
    Cancelled,
}

/// <summary>
/// The saved-account list (start.dat sprite 360) and the save/clear confirmation dialogs (sprites 283 and 285).
/// Only an explicit Save then Ok records an account; only Clear then Ok removes one.
/// </summary>
internal sealed class LoginAccountControls
{
    // listMC pop()/reset(): a setInterval of 20 ms moves the list 10 + 2 * rows pixels per step.
    private static readonly TimeSpan s_listStep = TimeSpan.FromMilliseconds(20);

    private readonly LoginAccountLayout _layout;
    private readonly LoginButtonSet<LoginDialogButton> _dialogButtons;
    private TimeSpan _listElapsed;

    public LoginAccountControls(LoginAccountLayout layout, LoginAccountHistory history)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        History = history ?? throw new ArgumentNullException(nameof(history));
        _dialogButtons = new LoginButtonSet<LoginDialogButton>([(LoginDialogButton.Ok, layout.Ok), (LoginDialogButton.Cancel, layout.Cancel)]);
    }

    public LoginAccountHistory History
    {
        get;
    }

    public LoginAccountList List
    {
        get; private set;
    }

    /// <summary>How far the list has slid up from its anchor, in reference pixels (zero or negative).</summary>
    public int ListOffset
    {
        get; private set;
    }

    public int HoveredRow { get; private set; } = -1;

    /// <summary>An empty history still shows one empty row.</summary>
    public int VisibleRowCount => Math.Max(1, History.Accounts.Count);

    public LoginAccountDialog? Dialog
    {
        get; private set;
    }

    /// <summary>One-based frame of the dialog timeline; frame 1 shows nothing.</summary>
    public int DialogFrame { get; private set; } = 1;

    public string? DialogAccount
    {
        get; private set;
    }

    public bool IsModal => Dialog is not null;

    public bool CapturesPointer => IsModal || List != LoginAccountList.Closed;

    public LoginButtonState DialogButtonState(LoginDialogButton button) => _dialogButtons.StateOf(button);

    public LoginRect RowBounds(int row) => _layout.Row(row, ListOffset);

    /// <summary>The Accounts button: listMC pop() while closed, reset() once fully open.</summary>
    public void ToggleList()
    {
        if (List == LoginAccountList.Closed)
        {
            List = LoginAccountList.Opening;
            ListOffset = 0;
            _listElapsed = TimeSpan.Zero;
        }
        else if (List == LoginAccountList.Open)
        {
            CloseList();
        }
    }

    /// <summary>
    /// saveBtn opens its dialog for any non-empty account; clearBtn only for an account already saved.
    /// Returns whether the dialog opened.
    /// </summary>
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

    /// <summary>Tracks the pointer and returns the dialog button it just moved onto, if any.</summary>
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
                // Any press closes the open list; one on a row also chooses that account.
                int row = mapped ? HitRow(x, y) : -1;
                CloseList();
                return row >= 0
                    ? new LoginAccountPress(LoginAccountPressKind.AccountChosen, History.Accounts[row])
                    : new LoginAccountPress(LoginAccountPressKind.Absorbed);
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
            // CMD_RECORDACCOUNT / CMD_CLEARACCOUNT.
            _ = Dialog == LoginAccountDialog.Save ? History.TryRecord(account) : History.TryRemove(account);
        }

        DialogFrame = LoginAccountArtwork.DialogHeldFrame + 1;
        return button == LoginDialogButton.Ok ? LoginAccountRelease.Confirmed : LoginAccountRelease.Cancelled;
    }

    /// <summary>Advances the dialog by stage frames and the list by its own 20 ms steps.</summary>
    public void Advance(TimeSpan step, int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);

        if (IsModal && !IsHolding)
        {
            // Showing stops on the held frame; hiding runs to frame 10, which clears the dialog.
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
                if (ListOffset == end)
                {
                    List = LoginAccountList.Open;
                    return;
                }
            }
            else
            {
                ListOffset = Math.Min(0, ListOffset + distance);
                if (ListOffset == 0)
                {
                    List = LoginAccountList.Closed;
                    return;
                }
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
