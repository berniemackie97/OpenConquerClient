using OpenConquer.Client.Diagnostics;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Audio;
using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Registration;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content.Login;
using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Login;

internal sealed class LoginScreen : IDisposable
{
    private readonly ClientDiagnostics _diagnostics;
    private readonly Action<LoginAudioCue> _playSound;
    private readonly LoginAudioFeedback _sounds;
    private bool _disposed;
    private LoginAttemptFeedback? _feedback;
    private LoginRegistrationFeedback? _registrationFeedback;

    public LoginScreen(LoginStageLayout layout, LoginAccountHistory history, string versionLabel, Action<LoginAudioCue> playSound, ClientDiagnostics? diagnostics = null)
    {
        _diagnostics = diagnostics ?? ClientDiagnostics.Silent;
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
        Registration = new LoginRegistrationForm(new LoginRegistrationLayout(layout));
        _sounds = new LoginAudioFeedback(layout);
    }

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
    internal bool IsLoginPending => AwaitingLoginCommand || AttemptStatus is LoginAttemptStatus.ConnectingAccount or
        LoginAttemptStatus.ConnectingGame or LoginAttemptStatus.ReadingCharacter or
        LoginAttemptStatus.InitializingWorld or LoginAttemptStatus.CharacterReceived;

    internal bool AwaitingLoginCommand
    {
        get; private set;
    }

    internal event Action? PendingLoginCancelled;

    internal LoginRegistrationForm Registration
    {
        get;
    }

    private bool IsExchangePending => IsLoginPending || Registration.IsSubmitting;

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
            ShowError(messages.Format(failure));
        }
        else if (IsLoginPending)
        {
            FocusLost();
        }
    }

    internal event Action? LoginRequested;
    internal event Action? RegistrationRequested;
    internal event Action? RegistrationSubmitRequested;
    internal event Action? RegistrationCancelRequested;

    internal void ApplyRegistrationFeedback(LoginRegistrationFeedback feedback, LoginErrorMessages messages)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        ArgumentNullException.ThrowIfNull(messages);

        if (ReferenceEquals(_registrationFeedback, feedback))
        {
            return;
        }

        _registrationFeedback = feedback;
        switch (feedback.Status)
        {
            case LoginRegistrationStatus.Closed:
                Registration.Close();
                if (feedback.Notice is { } refusal)
                {
                    ShowError(messages.Format(refusal));
                }

                break;
            case LoginRegistrationStatus.Editing:
                if (!Registration.IsOpen)
                {
                    // flashLogin.ini [dialogBox] show.
                    FocusLost();
                    Registration.Open(LoginRegistrationText.For(LoginRegistrationNotice.Guidance));
                    Play(LoginAudioCue.Error);
                }

                if (feedback.Notice is { } problem)
                {
                    Registration.ShowProblem(messages.Format(problem), feedback.Focus);
                    Play(LoginAudioCue.Error);
                }
                else
                {
                    Registration.ShowGuidance(LoginRegistrationText.For(LoginRegistrationNotice.Guidance));
                }

                break;
            case LoginRegistrationStatus.Submitting:
                Registration.BeginSubmitting(LoginRegistrationText.For(LoginRegistrationNotice.Submitting));
                break;
            case LoginRegistrationStatus.Created:
                // The new name fills the login account field; the player then enters the password to log in.
                string account = Registration.AccountName;
                Registration.Close();
                Credentials.ReplaceAccount(account);
                if (feedback.Notice is { } created)
                {
                    ShowError(messages.Format(created));
                }

                break;
        }
    }

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

    public LoginButtonSet<LoginButton> Buttons
    {
        get;
    }

    public ILoginCaretLocator? CaretLocator
    {
        get; set;
    }

    internal ILoginRegistrationCaretLocator? RegistrationCaretLocator
    {
        get; set;
    }

    public bool IsOpeningPlaying => Clock.Frame < LoginOpeningArtwork.FrameCount;

    public long LogoFrame => Math.Max(0, Clock.Frame - LoginOpeningArtwork.FrameCount);

    private bool PointerIsFree => !IsOpeningPlaying && ActiveDialog is null && !Registration.IsOpen && !IsExchangePending && !Account.CapturesPointer;

    public void Advance(TimeSpan elapsed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int frames = Clock.Advance(elapsed);
        Credentials.Synchronize();
        Account.Advance(Clock.Step, frames);
        Registration.Advance(frames);
        Error.Advance(frames);
        bool quit = Quit.Advance(frames);
        bool loadingQuit = LoadingQuit.Advance(frames);

        if (quit || loadingQuit)
        {
            ExitRequested?.Invoke();
        }
        SelectionAnimation.Advance(frames, Selection, interactive: PointerIsFree);
    }

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

        if (Registration.IsOpen)
        {
            Play(Registration.PointerMoved(mapped, x, y) is not null ? LoginAudioCue.ButtonHover : null);
            return;
        }

        if (IsExchangePending)
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

        if (Registration.IsOpen)
        {
            Play(Registration.PointerPressed(mapped, x, y, RegistrationCaretLocator, extendSelection) switch
            {
                LoginDialogButton.Ok => LoginAudioCue.ButtonClick,
                LoginDialogButton.Cancel => LoginAudioCue.ButtonHover,
                _ => null,
            });
            return;
        }

        if (IsExchangePending)
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

        if (Registration.IsOpen)
        {
            Handle(Registration.PointerReleased(mapped, x, y));
            return;
        }

        if (IsExchangePending)
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

    public void PointerDragged(int x)
    {
        if (ActiveDialog is null && Registration.IsSelecting && RegistrationCaretLocator is { } registration)
        {
            Registration.PointerDragged(registration.LocateWhileDragging(Registration, x));
        }
        else if (PointerIsFree && Credentials.IsSelecting && CaretLocator is { } locator)
        {
            Credentials.PointerDragged(locator.LocateWhileDragging(Credentials, x));
        }
    }

    internal bool IsSelectingText => Credentials.IsSelecting || Registration.IsSelecting;

    public void KeyPressed(DesktopKeyEvent key, Func<string?> readClipboard, Action<string> writeClipboard)
    {
        ArgumentNullException.ThrowIfNull(readClipboard);
        ArgumentNullException.ThrowIfNull(writeClipboard);

        if (!IsOpeningPlaying && key.Key == DesktopKey.Escape && !key.IsRepeat && !Quit.IsOpen && !LoadingQuit.IsOpen)
        {
            if (Registration is { IsOpen: true, IsSubmitting: false })
            {
                if (ActiveDialog is null)
                {
                    Handle(Registration.KeyPressed(key));
                }

                return;
            }

            bool hasStartedExchange = IsExchangePending && !AwaitingLoginCommand;
            FocusLost();
            (hasStartedExchange ? LoadingQuit : Quit).Show(string.Empty);
            Play(LoginAudioCue.Error);
            return;
        }

        if (IsOpeningPlaying || ActiveDialog is not null)
        {
            return;
        }

        if (Registration.IsOpen)
        {
            int edits = Registration.EditRevision;
            if (!TryHandleClipboard(Registration, key, readClipboard, writeClipboard))
            {
                Handle(Registration.KeyPressed(key));
            }

            PlayTypingSound(edits, Registration.EditRevision);
            return;
        }

        if (IsExchangePending || Account.CapturesPointer)
        {
            return;
        }

        if (key is { Key: DesktopKey.Enter, IsRepeat: false }
            && (key.Modifiers & (DesktopKeyModifiers.Alt | DesktopKeyModifiers.Control | DesktopKeyModifiers.Super)) == 0
            && Credentials.Focus != LoginCredentialFocus.None)
        {
            LoginRequested?.Invoke();
            return;
        }

        int revision = Credentials.EditRevision;
        if (!TryHandleClipboard(Credentials, key, readClipboard, writeClipboard))
        {
            Credentials.KeyPressed(key);
        }

        PlayTypingSound(revision, Credentials.EditRevision);
    }

    public void TextEntered(char character)
    {
        if (IsOpeningPlaying || ActiveDialog is not null)
        {
            return;
        }

        if (Registration.IsOpen)
        {
            int edits = Registration.EditRevision;
            Registration.TextEntered(character);
            PlayTypingSound(edits, Registration.EditRevision);
            return;
        }

        if (IsExchangePending || Account.CapturesPointer)
        {
            return;
        }

        int revision = Credentials.EditRevision;
        Credentials.TextEntered(character);
        PlayTypingSound(revision, Credentials.EditRevision);
    }

    internal void BeginLoading()
    {
        AwaitingLoginCommand = true;
        Credentials.StopEditingForLogin();
        ResetOtherFocus();
    }

    internal void DispatchPendingLogin()
    {
        AwaitingLoginCommand = false;
    }

    public void FocusLost()
    {
        bool cancelled = AwaitingLoginCommand;
        AwaitingLoginCommand = false;
        Credentials.FocusLost();
        ResetOtherFocus();
        if (cancelled)
        {
            PendingLoginCancelled?.Invoke();
        }
    }

    private void ResetOtherFocus()
    {
        SelectionInput.FocusLost();
        SelectionAnimation.ClearPointer();
        Buttons.Reset();
        Account.FocusLost();
        Registration.FocusLost();
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
        Registration.Dispose();
    }

    private bool PressAccountControls(bool mapped, int x, int y)
    {
        LoginAccountPress press = Account.PointerPressed(mapped, x, y);
        switch (press.Kind)
        {
            case LoginAccountPressKind.None:
                return false;
            case LoginAccountPressKind.AccountChosen:
                Credentials.ReplaceAccount(press.Account!);
                Play(LoginAudioCue.ButtonClick);
                break;
            case LoginAccountPressKind.OkPressed:
                Play(LoginAudioCue.ButtonClick);
                break;
            case LoginAccountPressKind.CancelPressed:
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
            case LoginButton.Register:
                RegistrationRequested?.Invoke();
                break;
            default:
                // Purchase and Help open publisher-era URLs, which stay disabled.
                break;
        }
    }

    private void Handle(LoginRegistrationCommand command)
    {
        switch (command)
        {
            case LoginRegistrationCommand.Submit:
                RegistrationSubmitRequested?.Invoke();
                break;
            case LoginRegistrationCommand.Cancel:
                Play(LoginAudioCue.Error);
                RegistrationCancelRequested?.Invoke();
                break;
        }
    }

    private void ShowError(string text)
    {
        FocusLost();
        Error.Show(text);
        Play(LoginAudioCue.Error);
    }

    private void OpenDialog(LoginAccountDialog dialog)
    {
        if (!Account.TryOpenDialog(dialog, Credentials.AccountDisplay))
        {
            return;
        }

        Play(LoginAudioCue.Error);
        Credentials.FocusLost();
    }

    private bool TryHandleClipboard(ILoginTextEditor editor, DesktopKeyEvent key, Func<string?> readClipboard, Action<string> writeClipboard)
    {
        bool shortcut = (key.Modifiers & (DesktopKeyModifiers.Control | DesktopKeyModifiers.Super)) != 0 && (key.Modifiers & DesktopKeyModifiers.Alt) == 0;
        if (!shortcut || key.IsRepeat || !editor.HasFocus || key.Key is not (DesktopKey.C or DesktopKey.X or DesktopKey.V))
        {
            return false;
        }

        try
        {
            if (key.Key == DesktopKey.V)
            {
                editor.Paste(readClipboard());
            }
            else if (editor.SelectedText() is { } text)
            {
                writeClipboard(text);
                if (key.Key == DesktopKey.X)
                {
                    editor.CutSelection();
                }
            }
        }
        catch (NotSupportedException)
        {
            _diagnostics.ClipboardUnavailable();
        }

        return true;
    }

    private void PlayTypingSound(int previousRevision, int currentRevision)
    {
        if (currentRevision != previousRevision)
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
