using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.Tests.UI.Login.Stage;

public sealed class LoginLoadingScreenTests
{
    [Fact]
    public void AccountAcceptanceInitializationAndWorldReadinessSelectAuthoredTraces()
    {
        LoginLoadingScreen screen = CreateScreen();
        screen.Start();

        screen.Update(LoginAttemptStatus.ConnectingAccount, false, TimeSpan.FromSeconds(1));
        Assert.Equal("Account", screen.Trace);
        screen.Update(LoginAttemptStatus.ConnectingGame, false, TimeSpan.FromSeconds(1));
        Assert.Equal("Game", screen.Trace);
        screen.Update(LoginAttemptStatus.ReadingCharacter, false, TimeSpan.FromSeconds(1));
        Assert.Equal("Game", screen.Trace);
        screen.Update(LoginAttemptStatus.InitializingWorld, false, TimeSpan.FromSeconds(1));
        Assert.Equal("Initializing", screen.Trace);
        screen.Update(LoginAttemptStatus.CharacterReceived, false, TimeSpan.FromSeconds(1));
        Assert.Equal("Creating maps", screen.Trace);

        screen.Update(LoginAttemptStatus.CharacterReceived, true, TimeSpan.FromMilliseconds(34));
        Assert.Equal("Creating maps", screen.Trace);
    }

    [Fact]
    public void FastAuthenticationCanReleaseTheFirstStopFromInitialization()
    {
        LoginLoadingScreen screen = CreateScreen();
        screen.Start();

        for (int i = 0; i < 40; i++)
        {
            screen.Update(LoginAttemptStatus.InitializingWorld, false, TimeSpan.FromMilliseconds(34));
        }

        Assert.Equal(36, screen.Sequence.Frame);
        Assert.Equal("Initializing", screen.Trace);
    }

    [Fact]
    public void FailureAndRetryRestoreTheCurrentStatus()
    {
        LoginLoadingScreen screen = CreateScreen();
        screen.Start();
        for (int i = 0; i < 40; i++)
        {
            screen.Update(LoginAttemptStatus.CharacterReceived, true, TimeSpan.FromMilliseconds(34));
        }

        screen.Update(LoginAttemptStatus.Timeout, false, TimeSpan.Zero);
        Assert.False(screen.Visible);
        Assert.Equal(string.Empty, screen.Trace);

        screen.Start();
        screen.Update(LoginAttemptStatus.InitializingWorld, false, TimeSpan.FromMilliseconds(34));
        Assert.Equal("Initializing", screen.Trace);
    }

    private static LoginLoadingScreen CreateScreen()
    {
        byte[] timeline = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
            "LoadingFixture", "timeline.json"));
        return new LoginLoadingScreen(LoginLoadingTimeline.Parse(timeline),
            "Account", "Game", "Initializing", "Creating maps");
    }
}
