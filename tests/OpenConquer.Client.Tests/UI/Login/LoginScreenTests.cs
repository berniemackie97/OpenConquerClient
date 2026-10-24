using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginScreenTests : IDisposable
{
    // Reference-stage points inside each control.
    private const int AccountsX = 601, AccountsY = 650;
    private const int SaveX = 630, SaveY = 652;
    private const int ClearX = 659, ClearY = 650;
    private const int OkX = 410, OkY = 428;
    private const int CancelX = 579, CancelY = 426;

    private static readonly LoginCredentialLayout s_fields = LoginCredentialLayout.Create(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)));

    private readonly string _folder = Directory.CreateTempSubdirectory("oc-login-screen-").FullName;
    private readonly List<LoginAudioCue> _sounds = [];

    [Fact]
    public void Opening_TakesNoInputUntilItsLastFrame()
    {
        using LoginScreen screen = Create(out _);

        Assert.True(screen.IsOpeningPlaying);
        Click(screen, SaveX, SaveY);
        Assert.Empty(_sounds);

        SkipOpening(screen);
        Assert.False(screen.IsOpeningPlaying);
        Assert.Equal(0, screen.LogoFrame);
    }

    [Fact]
    public void Save_RecordsTheAccountOnlyAfterOkAndNeverThePassword()
    {
        using LoginScreen screen = Create(out LoginAccountHistory history);
        SkipOpening(screen);
        Type(screen, s_fields.AccountField, "Player_01");
        Type(screen, s_fields.PasswordField, "Secret");

        Click(screen, SaveX, SaveY);
        Assert.Equal(LoginAccountDialog.Save, screen.Account.Dialog);
        Assert.Empty(history.Accounts);
        HoldDialog(screen);
        Click(screen, CancelX, CancelY);
        Assert.Empty(history.Accounts);
        Advance(screen, frames: 4);
        Assert.Null(screen.Account.Dialog);

        Click(screen, SaveX, SaveY);
        HoldDialog(screen);
        Click(screen, OkX, OkY);

        Assert.Equal("Player_01", Assert.Single(history.Accounts));
        string persisted = File.ReadAllText(Path.Combine(_folder, "login-accounts.json"));
        Assert.DoesNotContain("Secret", persisted, StringComparison.Ordinal);
    }

    [Fact]
    public void Dialog_PlaysItsShowAndHideSoundsAndRetailButtonClicks()
    {
        using LoginScreen screen = Create(out _);
        SkipOpening(screen);
        Type(screen, s_fields.AccountField, "Player");
        _sounds.Clear();

        Click(screen, SaveX, SaveY);
        HoldDialog(screen);
        Click(screen, CancelX, CancelY);

        // Save rollover and press (Button_2, Button_1), dialog show (error), Cancel rollover and press (both
        // Button_2), dialog hide (error).
        Assert.Equal(
        [
            LoginAudioCue.ButtonHover, LoginAudioCue.ButtonClick, LoginAudioCue.Error,
            LoginAudioCue.ButtonHover, LoginAudioCue.ButtonHover, LoginAudioCue.Error,
        ], _sounds);
    }

    [Fact]
    public void Clear_RemovesASavedAccountWithoutTouchingTheTypedOne()
    {
        using LoginScreen screen = Create(out LoginAccountHistory history);
        Assert.True(history.TryRecord("Recorded"));
        SkipOpening(screen);
        Assert.True(screen.Credentials.ReplaceAccount("Recorded"));

        Click(screen, ClearX, ClearY);
        Assert.Equal(LoginAccountDialog.Clear, screen.Account.Dialog);
        HoldDialog(screen);
        Click(screen, OkX, OkY);

        Assert.Empty(history.Accounts);
        Assert.Equal("Recorded", screen.Credentials.AccountDisplay);
    }

    [Fact]
    public void AccountList_SlidesInTwentyMillisecondStepsAndRecallsAnAccount()
    {
        using LoginScreen screen = Create(out LoginAccountHistory history);
        Assert.True(history.TryRecord("First"));
        Assert.True(history.TryRecord("Second"));
        SkipOpening(screen);
        Type(screen, s_fields.PasswordField, "S");

        Click(screen, AccountsX, AccountsY);
        Assert.Equal(LoginAccountList.Opening, screen.Account.List);
        screen.Advance(TimeSpan.FromMilliseconds(20));
        Assert.Equal(-14, screen.Account.ListOffset);
        screen.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(LoginAccountList.Open, screen.Account.List);

        LoginRect row = screen.Account.RowBounds(1);
        Click(screen, row.X + 5, row.Y + 5);

        Assert.Equal("Second", screen.Credentials.AccountDisplay);
        Assert.Equal(0, screen.Credentials.PasswordLength);
        Assert.Equal(LoginAccountList.Closing, screen.Account.List);
    }

    [Fact]
    public void Enter_PlaysConfirmAndExitRaisesItsRequest()
    {
        using LoginScreen screen = Create(out _);
        SkipOpening(screen);
        bool exitRequested = false;
        screen.ExitRequested += () => exitRequested = true;

        Click(screen, 700, 720);
        Assert.Equal([LoginAudioCue.ButtonHover, LoginAudioCue.Confirm], _sounds);
        Assert.False(exitRequested);

        Click(screen, 900, 720);
        Assert.True(exitRequested);
    }

    [Fact]
    public void EnterButtonAndKeyboardIssueOneRequestEachAfterOpening()
    {
        using LoginScreen screen = Create(out _);
        int attempts = 0;
        screen.LoginRequested += () => attempts++;

        Click(screen, 700, 720);
        Assert.Equal(0, attempts);
        SkipOpening(screen);

        Click(screen, 700, 720);
        Assert.Equal(1, attempts);

        LoginRect field = s_fields.AccountField;
        Click(screen, field.X + 2, field.Y + 2);
        screen.KeyPressed(new DesktopKeyEvent(DesktopKey.Enter, DesktopKeyModifiers.None, false),
            () => null, _ => { });
        Assert.Equal(2, attempts);

        screen.KeyPressed(new DesktopKeyEvent(DesktopKey.Enter, DesktopKeyModifiers.None, true),
            () => null, _ => { });
        screen.KeyPressed(new DesktopKeyEvent(DesktopKey.Enter, DesktopKeyModifiers.Control, false),
            () => null, _ => { });
        Assert.Equal(2, attempts);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private LoginScreen Create(out LoginAccountHistory history)
    {
        history = new LoginAccountHistory(_folder);
        return new LoginScreen(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)), history, "version: 5517", _sounds.Add);
    }

    private void SkipOpening(LoginScreen screen)
    {
        screen.Advance(TimeSpan.Zero);
        while (screen.IsOpeningPlaying)
        {
            screen.Advance(TimeSpan.FromMilliseconds(100));
        }

        _sounds.Clear();
    }

    private static void HoldDialog(LoginScreen screen) => Advance(screen, frames: LoginAccountArtwork.DialogHeldFrame);

    private static void Advance(LoginScreen screen, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            screen.Advance(TimeSpan.FromSeconds(1d / LoginStageClock.FramesPerSecond));
        }
    }

    private static void Click(LoginScreen screen, int x, int y)
    {
        screen.PointerMoved(true, x, y);
        screen.PointerPressed(true, x, y, extendSelection: false);
        screen.PointerReleased(true, x, y);
    }

    private static void Type(LoginScreen screen, LoginRect field, string text)
    {
        Click(screen, field.X + 2, field.Y + 2);
        foreach (char character in text)
        {
            screen.TextEntered(character);
        }
    }
}
