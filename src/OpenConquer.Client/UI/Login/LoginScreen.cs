using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Audio;
using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content.Login;
using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Login;

/// <summary>
/// The login screen's state and input handling, independent of any OpenGL context. Pointer coordinates are on
/// the logical stage. Input reaches the screen in the retail order: an open dialog first, then the saved-account
/// list, the buttons, the credential fields and finally the region and server tiles.
/// </summary>
internal sealed class LoginScreen : IDisposable
{
    private readonly Action<LoginAudioCue> _playSound;
    private readonly LoginAudioFeedback _sounds;
    private bool _disposed;
    private LoginAttemptFeedback? _feedback;

    public LoginScreen(LoginStageLayout layout, LoginAccountHistory history, string versionLabel, Action<LoginAudioCue> playSound)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        ArgumentNullException.ThrowIfNull(history);
        VersionLabel = versionLabel ?? throw new ArgumentNullException(nameof(versionLabel));
        _playSound = playSound ?? throw new ArgumentNullException(nameof(playSound));

        Selection = new LoginServerSelection();
        SelectionInput = new LoginSelectionInput(layout, Selection);
        SelectionAnimation = new LoginSelectionAnimation(layout);
        Credentials = new LoginCredentialInput(Selection, LoginCredentialLayout.Create(layout));
        AccountLayout = new LoginAccountLayout(layout);
        Account = new LoginAccountControls(AccountLayout, history);
        Buttons = LoginControlLayout.CreateButtons(layout);
        Error = new LoginErrorDialog(layout);
        Quit = new LoginErrorDialog(layout, quit: true);
        LoadingQuit = new LoginErrorDialog(layout, quit: true, loading: true);
        _sounds = new LoginAudioFeedback(layout);
    }

    /// <summary>Raised when Exit is clicked.</summary>
    public event Action? ExitRequested;

    internal LoginAttemptStatus AttemptStatus
    {
        get;
        set;
    }

    internal LoginErrorDialog Error
    {
        get;
    }
    internal LoginErrorDialog Quit
    {
        get;
    }
    internal LoginErrorDialog LoadingQuit
    {
        get;
    }
    private LoginErrorDialog? ActiveDialog => LoadingQuit.IsOpen ? LoadingQuit : Quit.IsOpen ? Quit : Error.IsOpen ? Error : null;
    internal bool IsLoginPending => AttemptStatus is LoginAttemptStatus.ConnectingAccount or
        LoginAttemptStatus.ConnectingGame or LoginAttemptStatus.ReadingCharacter;

    internal void ApplyFeedback(LoginAttemptFeedback feedback, LoginErrorMessages messages)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        ArgumentNullException.ThrowIfNull(messages);
        if (ReferenceEquals(_feedback, feedback))
        {
            return;
        }

        _feedback = feedback;
        AttemptStatus = feedback.Status;
        if (feedback.Failure is { OpensErrorMovie: true } failure)
        {
            FocusLost();
            Error.Show(messages.Format(failure));
            Play(LoginAudioCue.Error);
        }
        else if (IsLoginPending)
        {
            FocusLost();
        }
    }

    /// <summary>The native Enter activation requests login; the host owns all network work.</summary>
    internal event Action? LoginRequested;

    public LoginStageLayout Layout
    {
        get;
    }

    public string VersionLabel
    {
        get;
    }

    public LoginStageClock Clock { get; } = new();

    public LoginServerSelection Selection
    {
        get;
    }

    public LoginSelectionInput SelectionInput
    {
        get;
    }

    public LoginSelectionAnimation SelectionAnimation
    {
        get;
    }

    public LoginCredentialInput Credentials
    {
        get;
    }

    public LoginAccountLayout AccountLayout
    {
        get;
    }

    public LoginAccountControls Account
    {
        get;
    }

    /// <summary>The buttons inside <c>enterMC</c>.</summary>
    public LoginButtonSet<LoginButton> Buttons
    {
        get;
    }

    /// <summary>Measures caret positions; set while an OpenGL context renders the credential fields.</summary>
    public ILoginCaretLocator? CaretLocator
    {
        get; set;
    }

    /// <summary>The opening covers the stage and takes no input until its last frame has played.</summary>
    public bool IsOpeningPlaying => Clock.Frame < LoginOpeningArtwork.FrameCount;

    /// <summary>logo.swf loads on root frame 3, after the opening; the logo timeline counts from there.</summary>
    public long LogoFrame => Math.Max(0, Clock.Frame - LoginOpeningArtwork.FrameCount);

    private bool PointerIsFree => !IsOpeningPlaying && ActiveDialog is null && !IsLoginPending && !Account.CapturesPointer;

    /// <summary>Advances every login timeline by one rendered frame.</summary>
    public void Advance(TimeSpan elapsed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int frames = Clock.Advance(elapsed);
        Credentials.Synchronize();
        Account.Advance(Clock.Step, frames);
        Error.Advance(frames);
        bool quit = Quit.Advance(frames);
        bool loadingQuit = LoadingQuit.Advance(frames);
        if (quit || loadingQuit)
        {
            ExitRequested?.Invoke();
        }
        SelectionAnimation.Advance(frames, Selection, interactive: PointerIsFree);
    }

    /// <summary>
    /// Installs a newly authenticated catalog (or its absence), keeping an equivalent choice across renewals and
    /// applying the retail (0, 0) startup choice the first time a current catalog arrives.
    /// </summary>
    public void ApplyCatalog(SignedLoginCatalog? previous, SignedLoginCatalog? current, DateTimeOffset now, bool firstCurrentCatalog)
    {
        LoginVerifiedSelectionRetention.Apply(previous, current, now, SelectionInput, Selection);
        if (firstCurrentCatalog)
        {
            LoginStartupSelection.TryApply(current, now, Selection);
        }

        _sounds.Reset();
        Credentials.Synchronize();
    }

    public void PointerMoved(bool mapped, int x, int y)
    {
        if (IsOpeningPlaying)
        {
            return;
        }

        if (ActiveDialog is { } dialog)
        {
            Play(dialog.PointerMoved(mapped, x, y) is not null ? LoginAudioCue.ButtonHover : null);
            return;
        }

        if (IsLoginPending)
        {
            return;
        }

        SelectionAnimation.PointerMoved(mapped, x, y);
        bool dialogButtonEntered = Account.PointerMoved(mapped, x, y) is not null;
        if (Account.CapturesPointer)
        {
            Play(dialogButtonEntered ? LoginAudioCue.ButtonHover : null);
            return;
        }

        // Every button's rollover sound is Button_2.mp3.
        LoginButton? control = Buttons.PointerMoved(mapped, x, y);
        LoginButton? page = SelectionInput.PointerMoved(mapped, x, y);
        LoginAudioCue? area = _sounds.PointerMoved(mapped, x, y, Selection);
        Play(control is not null || page is not null ? LoginAudioCue.ButtonHover : area);
    }

    public void PointerPressed(bool mapped, int x, int y, bool extendSelection)
    {
        if (IsOpeningPlaying)
        {
            return;
        }

        if (ActiveDialog is { } dialog)
        {
            Play(dialog.PointerPressed(mapped, x, y) switch
            {
                LoginDialogButton.Ok => LoginAudioCue.ButtonClick,
                LoginDialogButton.Cancel => LoginAudioCue.ButtonHover,
                _ => null,
            });
            return;
        }

        if (IsLoginPending)
        {
            return;
        }

        if (PressAccountControls(mapped, x, y))
        {
            Credentials.PointerReleased();
            SelectionInput.FocusLost();
            return;
        }

        if (Buttons.PointerPressed(mapped, x, y) is { } button)
        {
            // flashLogin.ini: click_enterBtn is Confirm.mp3, every other button's click is Button_1.mp3.
            Play(button == LoginButton.Enter ? LoginAudioCue.Confirm : LoginAudioCue.ButtonClick);
            if (button == LoginButton.Exit)
            {
                Credentials.FocusLost();
            }

            return;
        }

        int? caret = null;
        if (mapped && CaretLocator is { } locator && locator.TryLocate(Credentials, x, y, out int located))
        {
            caret = located;
        }

        if (Credentials.PointerPressed(mapped, x, y, caret, extendSelection))
        {
            return;
        }

        Play(SelectionInput.PointerPressed(mapped, x, y) switch
        {
            LoginSelectionEvent.RegionSelected => LoginAudioCue.Dragon,
            LoginSelectionEvent.ServerPressed => LoginAudioCue.ServerSelect,
            LoginSelectionEvent.PagePressed => LoginAudioCue.Page,
            _ => null,
        });
    }

    public void PointerReleased(bool mapped, int x, int y)
    {
        if (IsOpeningPlaying)
        {
            return;
        }

        if (ActiveDialog is { } dialog)
        {
            Play(dialog.PointerReleased(mapped, x, y) ? LoginAudioCue.Error : null);
            return;
        }

        if (IsLoginPending)
        {
            return;
        }

        Credentials.PointerReleased();

        if (Account.IsModal)
        {
            if (Account.PointerReleased(mapped, x, y) != LoginAccountRelease.None)
            {
                Play(LoginAudioCue.Error);
            }

            return;
        }

        if (Buttons.PointerReleased(mapped, x, y) is { } button)
        {
            Click(button);
            return;
        }

        SelectionInput.PointerReleased(mapped, x, y);
    }

    /// <summary>Continues a held credential selection; <paramref name="x"/> is clamped to the stage.</summary>
    public void PointerDragged(int x)
    {
        if (PointerIsFree && Credentials.IsSelecting && CaretLocator is { } locator)
        {
            Credentials.PointerDragged(locator.LocateWhileDragging(Credentials, x));
        }
    }

    public void KeyPressed(DesktopKeyEvent key, Func<string?> readClipboard, Action<string> writeClipboard)
    {
        ArgumentNullException.ThrowIfNull(readClipboard);
        ArgumentNullException.ThrowIfNull(writeClipboard);

        if (!IsOpeningPlaying && key.Key == DesktopKey.Escape && !key.IsRepeat && !Quit.IsOpen && !LoadingQuit.IsOpen)
        {
            FocusLost();
            (IsLoginPending ? LoadingQuit : Quit).Show(string.Empty);
            Play(LoginAudioCue.Error);
            return;
        }

        if (IsOpeningPlaying || ActiveDialog is not null || IsLoginPending || Account.CapturesPointer)
        {
            return;
        }

        if (key.Key == DesktopKey.Enter && !key.IsRepeat &&
            (key.Modifiers & (DesktopKeyModifiers.Alt | DesktopKeyModifiers.Control |
                DesktopKeyModifiers.Super)) == 0 &&
            Credentials.Focus != LoginCredentialFocus.None)
        {
            LoginRequested?.Invoke();
            return;
        }

        int revision = Credentials.EditRevision;
        if (!TryHandleClipboard(key, readClipboard, writeClipboard))
        {
            Credentials.KeyPressed(key);
        }

        PlayTypingSound(revision);
    }

    public void TextEntered(char character)
    {
        if (IsOpeningPlaying || ActiveDialog is not null || IsLoginPending || Account.CapturesPointer)
        {
            return;
        }

        int revision = Credentials.EditRevision;
        Credentials.TextEntered(character);
        PlayTypingSound(revision);
    }

    public void FocusLost()
    {
        SelectionInput.FocusLost();
        SelectionAnimation.ClearPointer();
        Credentials.FocusLost();
        Buttons.Reset();
        Account.FocusLost();
        Error.FocusLost();
        Quit.FocusLost();
        LoadingQuit.FocusLost();
        _sounds.Reset();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Credentials.Dispose();
    }

    /// <summary>Returns whether the account list or a dialog took the press.</summary>
    private bool PressAccountControls(bool mapped, int x, int y)
    {
        LoginAccountPress press = Account.PointerPressed(mapped, x, y);
        switch (press.Kind)
        {
            case LoginAccountPressKind.None:
                return false;
            case LoginAccountPressKind.AccountChosen:
                // CMD_SELECTACCOUNT; flashLogin.ini popAccount.
                Credentials.ReplaceAccount(press.Account!);
                Play(LoginAudioCue.ButtonClick);
                break;
            case LoginAccountPressKind.OkPressed:
                Play(LoginAudioCue.ButtonClick);
                break;
            case LoginAccountPressKind.CancelPressed:
                // flashLogin.ini [dialogBox] click_cancel is Button_2.mp3.
                Play(LoginAudioCue.ButtonHover);
                break;
        }

        Buttons.Reset();
        return true;
    }

    private void Click(LoginButton button)
    {
        switch (button)
        {
            case LoginButton.Accounts:
                Account.ToggleList();
                break;
            case LoginButton.Save:
                OpenDialog(LoginAccountDialog.Save);
                break;
            case LoginButton.Clear:
                OpenDialog(LoginAccountDialog.Clear);
                break;
            case LoginButton.Exit:
                ExitRequested?.Invoke();
                break;
            case LoginButton.Enter:
                LoginRequested?.Invoke();
                break;
            default:
                // Register, Purchase and Help open publisher-era URLs, which stay disabled.
                break;
        }
    }

    private void OpenDialog(LoginAccountDialog dialog)
    {
        if (Account.TryOpenDialog(dialog, Credentials.AccountDisplay))
        {
            // flashLogin.ini [dialogBox] show.
            Play(LoginAudioCue.Error);
            Credentials.FocusLost();
        }
    }

    private bool TryHandleClipboard(DesktopKeyEvent key, Func<string?> readClipboard, Action<string> writeClipboard)
    {
        bool shortcut = (key.Modifiers & (DesktopKeyModifiers.Control | DesktopKeyModifiers.Super)) != 0 &&
            (key.Modifiers & DesktopKeyModifiers.Alt) == 0;
        if (!shortcut || key.IsRepeat || Credentials.Focus == LoginCredentialFocus.None ||
            key.Key is not (DesktopKey.C or DesktopKey.X or DesktopKey.V))
        {
            return false;
        }

        try
        {
            if (key.Key == DesktopKey.V)
            {
                Credentials.Paste(readClipboard());
            }
            else if (Credentials.SelectedAccountText() is { } text)
            {
                writeClipboard(text);
                if (key.Key == DesktopKey.X)
                {
                    Credentials.CutSelectedAccount();
                }
            }
        }
        catch (NotSupportedException exception)
        {
            Console.Error.WriteLine($"OpenConquer: desktop clipboard unavailable ({exception.GetType().Name}).");
        }

        return true;
    }

    private void PlayTypingSound(int previousRevision)
    {
        if (Credentials.EditRevision != previousRevision)
        {
            _playSound(_sounds.NextKeySound());
        }
    }

    private void Play(LoginAudioCue? cue)
    {
        if (cue is { } sound)
        {
            _playSound(sound);
        }
    }
}
