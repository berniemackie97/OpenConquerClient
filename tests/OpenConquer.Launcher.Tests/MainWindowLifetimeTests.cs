using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using OpenConquer.Launcher.Installation;
using OpenConquer.Launcher.Play;
using OpenConquer.Launcher.Settings;

namespace OpenConquer.Launcher.Tests;

[Collection(nameof(LauncherHeadlessUi))]
public sealed class MainWindowLifetimeTests(LauncherHeadlessFixture fixture)
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);
    private static readonly DisplayPreferences s_preferences = new(1024, 768, GameWindowMode.Fixed, GamePresentation.Integer);

    [Fact]
    public Task CloseDuringOpening_CancelsAndDrainsWithoutUpdatingTheScreen() => OnDispatcher(async () =>
    {
        PendingRead<GameModeRecord> modes = new();
        LauncherApplication application = CreateApplication();
        PlayScreen screen = new(new Border());
        List<Exception> failures = [];
        using MainWindow window = new(application, null, failures.Add, screen, new SettingsStore(),
            new ModeStore { Load = modes.ReadAsync }, (_, _) => throw new InvalidOperationException("Unexpected client start."));
        PlaySkinButton[] startButtons = screen.Children.OfType<PlaySkinButton>().Take(3).ToArray();
        Assert.Equal(3, startButtons.Length);

        window.Show();
        await WaitFor(modes.Entered.Task);
        bool[] before = startButtons.Select(button => button.Faded).ToArray();

        try
        {
            window.Close();
            Task stopping = window.RequestShutdownAsync();
            await WaitFor(modes.Canceled.Task);
            Assert.True(window.IsVisible);
            Assert.False(stopping.IsCompleted);
            Assert.Same(stopping, window.RequestShutdownAsync());

            // A completed disk read can win its own race with cancellation.
            modes.Release.SetResult(new GameModeRecord(new GameModeUsage(0, 0, 5), GameMode.Low));
            await WaitFor(stopping);
            Assert.Equal(before, startButtons.Select(button => button.Faded));
            Assert.False(window.IsVisible);
            Assert.IsType<LauncherState.Stopped>(application.State);
            Assert.Empty(failures);
        }
        finally
        {
            modes.Release.TrySetResult(default);
            await WaitFor(window.RequestShutdownAsync());
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CloseDuringPlay_PreventsProcessStartAfterEitherPreferenceOperation(bool readingDisplaySettings) => OnDispatcher(async () =>
    {
        PendingRead<bool> modeWrite = new();
        PendingRead<SettingsReadResult> settingsRead = new();
        List<Exception> failures = [];
        int launches = 0;
        int settingsReads = 0;
        ModeStore modes = new();
        SettingsStore settings = new();
        if (readingDisplaySettings)
        {
            settings.Load = token =>
            {
                settingsReads++;
                return settingsRead.ReadAsync(token);
            };
        }
        else
        {
            modes.Record = (_, token) => modeWrite.ReadAsync(token);
            settings.Load = _ =>
            {
                settingsReads++;
                return Task.FromResult<SettingsReadResult>(new SettingsReadResult.Loaded(s_preferences, null));
            };
        }

        using MainWindow window = new(CreateApplication(), null, failures.Add, new PlayScreen(new Border()),
            settings, modes, (_, _) => launches++);
        window.Show();
        ClickHigh(window);
        try
        {
            await WaitFor(readingDisplaySettings ? settingsRead.Entered.Task : modeWrite.Entered.Task);
            window.Close();
            Task stopping = window.RequestShutdownAsync();
            await WaitFor(readingDisplaySettings ? settingsRead.Canceled.Task : modeWrite.Canceled.Task);
            Assert.False(stopping.IsCompleted);
            Assert.True(window.IsVisible);

            modeWrite.Release.TrySetResult(true);
            settingsRead.Release.TrySetResult(new SettingsReadResult.Loaded(s_preferences, null));
            await WaitFor(stopping);
            Assert.Equal(0, launches);
            Assert.Equal(readingDisplaySettings ? 1 : 0, settingsReads);
            Assert.False(window.IsVisible);
            Assert.Empty(failures);
        }
        finally
        {
            modeWrite.Release.TrySetResult(true);
            settingsRead.Release.TrySetResult(new SettingsReadResult.Loaded(s_preferences, null));
            await WaitFor(window.RequestShutdownAsync());
        }
    });

    [Fact]
    public Task SuccessfulPlay_StartsOnceAndThenClosesTheLauncher() => OnDispatcher(async () =>
    {
        TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<GameMode> recorded = [];
        List<DisplayPreferences> launched = [];
        List<Exception> failures = [];
        ModeStore modes = new()
        {
            Record = (mode, _) =>
            {
                recorded.Add(mode);
                // A mode counter write failure does not prevent the handover.
                return Task.FromResult(false);
            }
        };
        using MainWindow window = new(CreateApplication(), null, failures.Add, new PlayScreen(new Border()),
            new SettingsStore(), modes, (_, preferences) => launched.Add(preferences));
        window.Closed += (_, _) => closed.SetResult();
        try
        {
            window.Show();
            ClickHigh(window);
            await WaitFor(closed.Task);
            await WaitFor(window.RequestShutdownAsync());
            Assert.Equal([GameMode.High], recorded);
            Assert.Equal([s_preferences], launched);
            Assert.False(window.IsVisible);
            Assert.Empty(failures);
        }
        finally
        {
            await WaitFor(window.RequestShutdownAsync());
        }
    });

    [Fact]
    public Task Shutdown_RetainsTheOperationAndCancellationFailuresAndStillCloses() => OnDispatcher(async () =>
    {
        PendingRead<GameModeRecord> modes = new();
        IOException operationFailure = new("Mode read failed.");
        InvalidOperationException cancellationFailure = new("Cancellation callback failed.");
        List<Exception> failures = [];
        using MainWindow window = new(CreateApplication(), null, failures.Add, new PlayScreen(new Border()), new SettingsStore(),
            new ModeStore { Load = modes.ReadAsync }, (_, _) => throw new InvalidOperationException("Unexpected client start."));
        window.Show();
        await WaitFor(modes.Entered.Task);
        using CancellationTokenRegistration registration = modes.Token.Register(() => throw cancellationFailure);
        try
        {
            Task stop = window.RequestShutdownAsync();
            await WaitFor(modes.Canceled.Task);
            Assert.False(stop.IsCompleted);
            modes.Release.SetException(operationFailure);

            await WaitFor(stop);
            Assert.Same(stop, window.RequestShutdownAsync());
            Assert.False(window.IsVisible);
            AggregateException failure = Assert.IsType<AggregateException>(Assert.Single(failures));
            Assert.Equal(2, failure.InnerExceptions.Count);
            Assert.Contains(operationFailure, failure.InnerExceptions);
            Assert.Contains(cancellationFailure, failure.InnerExceptions);
        }
        finally
        {
            modes.Release.TrySetResult(default);
            await WaitFor(window.RequestShutdownAsync());
        }
    });

    [Fact]
    public Task ClosingSettings_DrainsTheReadAndDiscardsLateValues() => OnDispatcher(async () =>
    {
        PendingRead<SettingsReadResult> read = new();
        List<Exception> failures = [];
        DisplaySettingsWindow window = new(new SettingsStore { Load = read.ReadAsync }, failures.Add);
        TextBox width = Assert.IsType<TextBox>(window.FindControl<TextBox>("WidthInput"));
        string? originalWidth = width.Text;
        window.Show();
        try
        {
            await WaitFor(read.Entered.Task);
            window.Close();
            Task stopping = window.StopAsync();
            await WaitFor(read.Canceled.Task);
            Assert.False(stopping.IsCompleted);
            Assert.Same(stopping, window.StopAsync());

            read.Release.SetResult(new SettingsReadResult.Loaded(s_preferences, null));
            await WaitFor(stopping);
            Assert.Equal(originalWidth, width.Text);
            Assert.False(window.IsVisible);
            Assert.Empty(failures);
        }
        finally
        {
            read.Release.TrySetResult(new SettingsReadResult.Loaded(s_preferences, null));
            await WaitFor(window.StopAsync());
        }
    });

    private Task OnDispatcher(Func<Task> test) => fixture.Dispatch(test, TestContext.Current.CancellationToken);

    private static Task WaitFor(Task task) => task.WaitAsync(s_timeout, TestContext.Current.CancellationToken);

    private static void ClickHigh(Window window)
    {
        Point high = PlayLayout.High + new Point(105, 40);
        window.MouseDown(high, MouseButton.Left);
        window.MouseUp(high, MouseButton.Left);
    }

    private static LauncherApplication CreateApplication()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "managed-installation"));
        string client = Path.Combine(root, "client");
        string runtime = ReleaseTargetRuntime.Current ?? "linux-x64";
        ManagedInstallation installation = ManagedInstallation.Create(root, client,
            Path.Combine(root, ManagedInstallationManifest.FileName), Path.Combine(root, ManagedReleaseManifest.FileName),
            Path.Combine(root, ManagedReleaseSignature.FileName), Path.Combine(client, ReleaseTargetRuntime.ClientExecutable(runtime)),
            new ManagedReleaseIdentity(1, "test-release", runtime));
        return new LauncherApplication(new ResolvedInstallation(installation));
    }

    private sealed class ResolvedInstallation(ManagedInstallation installation) : IManagedInstallationResolver
    {
        public Task<ManagedInstallationResolution> ResolveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ManagedInstallationResolution>(new ManagedInstallationResolution.Resolved(installation));
    }

    private sealed class PendingRead<T>
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<T> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token
        {
            get;
            private set;
        }

        public async Task<T> ReadAsync(CancellationToken token)
        {
            Token = token;
            using CancellationTokenRegistration registration = token.Register(() => Canceled.TrySetResult());
            Entered.SetResult();
            return await Release.Task;
        }
    }

    private sealed class ModeStore : IGameModeStore
    {
        public Func<CancellationToken, Task<GameModeRecord>> Load { get; init; } = _ => Task.FromResult(default(GameModeRecord));
        public Func<GameMode, CancellationToken, Task<bool>> Record { get; set; } = (_, _) => Task.FromResult(true);

        public Task<GameModeRecord> LoadAsync(CancellationToken cancellationToken) => Load(cancellationToken);
        public Task<bool> RecordStartAsync(GameMode mode, CancellationToken cancellationToken) => Record(mode, cancellationToken);
    }

    private sealed class SettingsStore : IDisplaySettingsStore
    {
        public Func<CancellationToken, Task<SettingsReadResult>> Load
        {
            get;
            set;
        } =
            _ => Task.FromResult<SettingsReadResult>(new SettingsReadResult.Loaded(s_preferences, null));

        public Task<SettingsReadResult> LoadAsync(CancellationToken cancellationToken) => Load(cancellationToken);

        public Task<SettingsIssue?> SaveAsync(DisplayPreferences preferences, string? expectedRevision, bool resetInvalid,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
