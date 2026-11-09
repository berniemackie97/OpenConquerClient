using System.Text.Json;
using OpenConquer.Client.Startup.LoginFlow;
using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content;
using OpenConquer.Content.Login;
using OpenConquer.Content.Text;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class ClientLoginRetryTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("oc-login-retry-").FullName;

    [Fact]
    public void DismissedFailureDoesNotResetTheNewLoginBeforeFrameSeventeen()
    {
        using ClientLogin login = Create(out LoginScreen screen, out LoginAttemptCoordinator attempts,
            out LoginLoadingScreen loading);
        PrepareRetry(login, screen, attempts);

        Assert.True(screen.AwaitingLoginCommand);
        Assert.Equal(2, loading.Sequence.Frame);
        for (int frame = 0; frame < 30; frame++)
        {
            login.UpdateLoading(false, false, TimeSpan.FromMilliseconds(34));
            Assert.True(loading.Visible);
            Assert.Equal("Account", loading.Trace);
        }

        Assert.Equal(17, loading.Sequence.Frame);
        Assert.True(loading.Sequence.ReadyForAuthentication);
        login.Update();

        Assert.False(screen.AwaitingLoginCommand);
        Assert.Equal(LoginAttemptStatus.SelectRealm, attempts.Status);
        Assert.False(loading.Visible);
        login.Update();
        Assert.False(screen.AwaitingLoginCommand);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CancelledRetryDoesNotDispatchAndErasesPassword(bool loseFocus)
    {
        using ClientLogin login = Create(out LoginScreen screen, out LoginAttemptCoordinator attempts,
            out LoginLoadingScreen loading);
        PrepareRetry(login, screen, attempts);

        if (loseFocus)
        {
            screen.FocusLost();
        }
        else
        {
            screen.KeyPressed(new DesktopKeyEvent(DesktopKey.Escape, default, false),
                static () => null, static _ => { });
        }

        Assert.False(screen.AwaitingLoginCommand);
        Assert.Equal(0, screen.Credentials.PasswordLength);
        Assert.False(loading.Visible);
        login.UpdateLoading(false, false, TimeSpan.FromMilliseconds(34));
        Assert.False(loading.Visible);
        Assert.Equal(LoginAttemptStatus.Unavailable, attempts.Status);
    }

    [Theory]
    [InlineData((int)LoginAttemptStatus.AccountRefused)]
    [InlineData((int)LoginAttemptStatus.GameRefused)]
    [InlineData((int)LoginAttemptStatus.CharacterCreation)]
    [InlineData((int)LoginAttemptStatus.Timeout)]
    [InlineData((int)LoginAttemptStatus.Unavailable)]
    [InlineData((int)LoginAttemptStatus.Cancelled)]
    public void TerminalOutcomeStillResetsLoadingWithoutPendingCommand(int status)
    {
        LoginLoadingScreen loading = CreateLoading();
        loading.Start();
        loading.Update((LoginAttemptStatus)status, false, TimeSpan.FromMilliseconds(34));

        Assert.False(loading.Visible);
        Assert.Equal(1, loading.Sequence.Frame);
    }

    private ClientLogin Create(out LoginScreen screen, out LoginAttemptCoordinator attempts,
        out LoginLoadingScreen loading)
    {
        ClientContentRoot content = new(_folder);
        screen = new LoginScreen(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)),
            new LoginAccountHistory(_folder), "5517", static _ => { });
        attempts = new LoginAttemptCoordinator(_folder, content: content);
        loading = CreateLoading();
        LoginErrorMessages errors = new(
            ClientStringResources.Parse("10249=Connection lost\n10012=Select a server\n"u8),
            ClientKeyedStringResources.Parse([]), LoginErrorEncoding.ForCodePage(1252));
        return new ClientLogin(content, screen, attempts, new LoginRegistrationCoordinator(),
            errors, loading, static () => true);
    }

    private static void PrepareRetry(ClientLogin login, LoginScreen screen, LoginAttemptCoordinator attempts)
    {
        screen.Advance(TimeSpan.Zero);
        while (screen.IsOpeningPlaying)
        {
            screen.Advance(TimeSpan.FromMilliseconds(250));
        }

        attempts.FailWorldConnection();
        login.ApplyFeedback();
        Assert.True(screen.Error.IsOpen);
        screen.Error.Advance(4);
        screen.Error.PointerPressed(true, 500, 428);
        Assert.True(screen.Error.PointerReleased(true, 500, 428));
        screen.Error.Advance(3);
        Assert.False(screen.Error.IsOpen);

        Assert.True(screen.Credentials.ReplaceAccount("Player"));
        LoginRect password = LoginCredentialLayout.Create(screen.Layout).PasswordField;
        screen.Credentials.PointerPressed(true, password.X + 2, password.Y + 2);
        screen.Credentials.TextEntered('s');
        screen.KeyPressed(new DesktopKeyEvent(DesktopKey.Enter, default, false),
            static () => null, static _ => { });
    }

    private static LoginLoadingScreen CreateLoading()
    {
        List<object[]> events = [];
        int[] matrix = [1, 0, 0, 1, 0, 0];
        for (int frame = 18; frame <= 100; frame++)
        {
            events.Add([frame, new { depth = 4, m = matrix }]);
            events.Add([frame, new { depth = 9, m = matrix }]);
        }

        byte[] timeline = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["394"] = new { frames = 100, events }
        });
        return new LoginLoadingScreen(LoginLoadingTimeline.Parse(timeline),
            "Account", "Game", "Initializing", "Map");
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
