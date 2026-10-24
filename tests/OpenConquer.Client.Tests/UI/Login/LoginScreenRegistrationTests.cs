using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginScreenRegistrationTests : IDisposable
{
    private const int SignUpX = 60, SignUpY = 722;
    private static readonly LoginCredentialLayout s_fields = LoginCredentialLayout.Create(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)));

    private readonly string _folder = Directory.CreateTempSubdirectory("oc-login-registration-").FullName;

    [Fact]
    public void SignUpClickRequestsTheRegistrationToggle()
    {
        using LoginScreen screen = CreateReady();
        int toggles = 0;
        screen.RegistrationToggleRequested += () => toggles++;

        Click(screen, SignUpX, SignUpY);

        Assert.Equal(1, toggles);
    }

    [Fact]
    public void RegisteringRoutesEnterToRegistrationAndHoldsSignUpDown()
    {
        using LoginScreen screen = CreateReady();
        int logins = 0, submits = 0;
        screen.LoginRequested += () => logins++;
        screen.RegistrationSubmitRequested += () => submits++;

        Apply(screen, new LoginRegistrationFeedback(LoginRegistrationStep.EnterCredentials));
        Assert.Equal(LoginButtonState.Down, screen.ButtonState(LoginButton.Register));
        Type(screen, s_fields.AccountField, "newuser");
        screen.KeyPressed(new DesktopKeyEvent(DesktopKey.Enter, default, false), static () => null, static _ => { });

        Assert.Equal(1, submits);
        Assert.Equal(0, logins);

        Apply(screen, new LoginRegistrationFeedback(LoginRegistrationStep.None));
        Assert.NotEqual(LoginButtonState.Down, screen.ButtonState(LoginButton.Register));
        Type(screen, s_fields.AccountField, "x");
        screen.KeyPressed(new DesktopKeyEvent(DesktopKey.Enter, default, false), static () => null, static _ => { });
        Assert.Equal(1, logins);
    }

    [Fact]
    public void EscapeLeavesRegistrationWithoutOpeningQuit()
    {
        using LoginScreen screen = CreateReady();
        int cancels = 0;
        screen.RegistrationCancelRequested += () => cancels++;
        Apply(screen, new LoginRegistrationFeedback(LoginRegistrationStep.ConfirmPassword));

        screen.KeyPressed(new DesktopKeyEvent(DesktopKey.Escape, default, false), static () => null, static _ => { });

        Assert.Equal(1, cancels);
        Assert.False(screen.Quit.IsOpen);
        Assert.False(screen.LoadingQuit.IsOpen);
    }

    [Fact]
    public void SubmittingBlocksInputAndEscapeOpensTheLoadingQuitDialog()
    {
        using LoginScreen screen = CreateReady();
        Type(screen, s_fields.AccountField, "newuser");
        Apply(screen, new LoginRegistrationFeedback(LoginRegistrationStep.Submitting));

        screen.TextEntered('z');
        Assert.Equal("newuser", screen.Credentials.AccountDisplay);

        screen.KeyPressed(new DesktopKeyEvent(DesktopKey.Escape, default, false), static () => null, static _ => { });
        Assert.True(screen.LoadingQuit.IsOpen);
    }

    [Fact]
    public void NoticeOpensTheErrorDialogWithProjectText()
    {
        using LoginScreen screen = CreateReady();

        Apply(screen, new LoginRegistrationFeedback(LoginRegistrationStep.EnterCredentials,
            LoginFailure.Registration(LoginRegistrationNotice.Begin)));

        Assert.True(screen.Error.IsOpen);
        Assert.Equal("Enter a new account name and password, then press Enter.", screen.Error.Text);
    }

    [Fact]
    public void EveryNoticeHasText()
    {
        foreach (LoginRegistrationNotice notice in Enum.GetValues<LoginRegistrationNotice>())
        {
            Assert.False(string.IsNullOrWhiteSpace(LoginRegistrationText.For(notice)));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => LoginFailure.Registration((LoginRegistrationNotice)99));
    }

    private static void Apply(LoginScreen screen, LoginRegistrationFeedback feedback) =>
        screen.ApplyRegistrationFeedback(feedback, LoginErrorTests.Messages());

    private LoginScreen CreateReady()
    {
        LoginScreen screen = new(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)),
            new LoginAccountHistory(_folder), "version: 5517", static _ => { });
        screen.Advance(TimeSpan.Zero);
        while (screen.IsOpeningPlaying)
        {
            screen.Advance(TimeSpan.FromMilliseconds(100));
        }

        return screen;
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

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
