using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Registration;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginScreenRegistrationTests : IDisposable
{
    private const int SignUpX = 60, SignUpY = 722;
    private static readonly LoginCredentialLayout s_fields = LoginCredentialLayout.Create(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)));

    private readonly string _folder = Directory.CreateTempSubdirectory("oc-login-registration-").FullName;

    [Fact]
    public void SignUpClickAsksTheHostToOpenTheDialog()
    {
        using LoginScreen screen = CreateReady();
        int requests = 0;
        screen.RegistrationRequested += () => requests++;

        Click(screen, SignUpX, SignUpY);

        Assert.Equal(1, requests);
        Assert.False(screen.Registration.IsOpen);
    }

    [Fact]
    public void EditingOpensTheDialogWhichTakesEveryKey()
    {
        using LoginScreen screen = CreateOpen();
        int logins = 0, submits = 0;
        screen.LoginRequested += () => logins++;
        screen.RegistrationSubmitRequested += () => submits++;

        Assert.Equal(LoginRegistrationText.For(LoginRegistrationNotice.Guidance), screen.Registration.Message);
        TypeText(screen, "newuser");
        Press(screen, DesktopKey.Enter);

        Assert.Equal("newuser", screen.Registration.AccountName);
        Assert.Equal(string.Empty, screen.Credentials.AccountDisplay);
        Assert.Equal((1, 0), (submits, logins));
    }

    [Fact]
    public void TheDialogIsModal()
    {
        using LoginScreen screen = CreateOpen();
        int requests = 0;
        screen.RegistrationRequested += () => requests++;

        Click(screen, SignUpX, SignUpY);
        Click(screen, s_fields.AccountField.X + 2, s_fields.AccountField.Y + 2);

        Assert.Equal(0, requests);
        Assert.Equal(LoginCredentialFocus.None, screen.Credentials.Focus);
    }

    [Fact]
    public void OkAndCancelButtonsRequestTheirCommands()
    {
        using LoginScreen screen = CreateOpen();
        int submits = 0, cancels = 0;
        screen.RegistrationSubmitRequested += () => submits++;
        screen.RegistrationCancelRequested += () => cancels++;
        LoginRegistrationLayout layout = screen.Registration.Layout;

        Click(screen, layout.Ok.X + 4, layout.Ok.Y + 4);
        Click(screen, layout.Cancel.X + 4, layout.Cancel.Y + 4);

        Assert.Equal((1, 1), (submits, cancels));
    }

    [Fact]
    public void EscapeCancelsTheDialogWithoutOpeningQuit()
    {
        using LoginScreen screen = CreateOpen();
        int cancels = 0;
        screen.RegistrationCancelRequested += () => cancels++;

        Press(screen, DesktopKey.Escape);

        Assert.Equal(1, cancels);
        Assert.False(screen.Quit.IsOpen);
        Assert.False(screen.LoadingQuit.IsOpen);
    }

    [Fact]
    public void ClosedFeedbackClosesTheDialog()
    {
        using LoginScreen screen = CreateOpen();

        Apply(screen, new LoginRegistrationFeedback(LoginRegistrationStatus.Closed));
        Settle(screen);

        Assert.False(screen.Registration.IsOpen);
        Assert.False(screen.Error.IsOpen);
    }

    [Fact]
    public void SubmittingLocksTheDialogAndEscapeOpensTheLoadingQuitDialog()
    {
        using LoginScreen screen = CreateOpen();
        TypeText(screen, "newuser");

        Apply(screen, new LoginRegistrationFeedback(LoginRegistrationStatus.Submitting));
        TypeText(screen, "z");

        Assert.Equal("newuser", screen.Registration.AccountName);
        Assert.Equal(LoginRegistrationText.For(LoginRegistrationNotice.Submitting), screen.Registration.Message);
        Press(screen, DesktopKey.Escape);
        Assert.True(screen.LoadingQuit.IsOpen);
    }

    [Fact]
    public void ProblemsAreShownInsideTheDialog()
    {
        using LoginScreen screen = CreateOpen();
        TypeText(screen, "newuser");
        Press(screen, DesktopKey.Tab);
        TypeText(screen, "secret1");

        Apply(screen, new LoginRegistrationFeedback(LoginRegistrationStatus.Editing,
            LoginFailure.Registration(LoginRegistrationNotice.PasswordMismatch), LoginRegistrationField.Password));

        Assert.False(screen.Error.IsOpen);
        Assert.True(screen.Registration.MessageIsError);
        Assert.Equal("The passwords do not match.", screen.Registration.Message);
        Assert.Equal(LoginRegistrationField.Password, screen.Registration.Focus);
        Assert.Equal(0, screen.Registration.Field(LoginRegistrationField.Password).Length);
    }

    [Fact]
    public void RefusalToOpenUsesTheErrorDialog()
    {
        using LoginScreen screen = CreateReady();

        Apply(screen, new LoginRegistrationFeedback(LoginRegistrationStatus.Closed,
            LoginFailure.Registration(LoginRegistrationNotice.ServerNotSelected)));

        Assert.False(screen.Registration.IsOpen);
        Assert.True(screen.Error.IsOpen);
        Assert.Equal("Select a server before signing up.", screen.Error.Text);
    }

    [Fact]
    public void CreatedAccountClosesTheDialogAndFillsTheLoginAccount()
    {
        using LoginScreen screen = CreateOpen();
        TypeText(screen, "newuser");

        Apply(screen, new LoginRegistrationFeedback(LoginRegistrationStatus.Created,
            LoginFailure.Registration(LoginRegistrationNotice.Created)));

        Assert.Equal("newuser", screen.Credentials.AccountDisplay);
        Assert.Equal(0, screen.Credentials.PasswordLength);
        Assert.True(screen.Error.IsOpen);
        Assert.Equal(LoginRegistrationText.For(LoginRegistrationNotice.Created), screen.Error.Text);
        Settle(screen);
        Assert.False(screen.Registration.IsOpen);
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

    private LoginScreen CreateOpen()
    {
        LoginScreen screen = CreateReady();
        Apply(screen, new LoginRegistrationFeedback(LoginRegistrationStatus.Editing));
        Settle(screen);
        Assert.True(screen.Registration.AcceptsInput);
        return screen;
    }

    /// <summary>Runs the dialog timeline to its next resting frame.</summary>
    private static void Settle(LoginScreen screen) => screen.Advance(TimeSpan.FromSeconds(1));

    private static void Click(LoginScreen screen, int x, int y)
    {
        screen.PointerMoved(true, x, y);
        screen.PointerPressed(true, x, y, extendSelection: false);
        screen.PointerReleased(true, x, y);
    }

    private static void Press(LoginScreen screen, DesktopKey key) =>
        screen.KeyPressed(new DesktopKeyEvent(key, default, false), static () => null, static _ => { });

    private static void TypeText(LoginScreen screen, string text)
    {
        foreach (char character in text)
        {
            screen.TextEntered(character);
        }
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
