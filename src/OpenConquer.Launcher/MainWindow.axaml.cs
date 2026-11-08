using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using OpenConquer.Launcher.Installation;
using OpenConquer.Launcher.Instances;
using OpenConquer.Launcher.Play;
using OpenConquer.Launcher.Settings;

namespace OpenConquer.Launcher;

/// <summary>
/// The launcher window: the retail <c>play.exe</c> dialog. A start button verifies the installation again,
/// records the start mode, starts the game client and closes the launcher, as play.exe handed over to AutoPatch.
/// </summary>
internal sealed partial class MainWindow : Window, IDisposable
{
    private enum HintPurpose
    {
        None,
        LowConfirmation,
        Problem,
    }

    private readonly LauncherApplication _application;
    private readonly LauncherActivationServer? _activation;
    private readonly IDisplaySettingsStore _settings;
    private readonly IGameModeStore _gameModes;
    private readonly Action<ManagedInstallation, DisplayPreferences> _startClient;
    private readonly Action<Exception> _reportFailure;
    private readonly LauncherFailures _failures = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly PlayScreen _screen;
    private PlayCursor? _cursor;
    private DisplaySettingsWindow? _settingsWindow;
    private GameModeUsage _usage;
    private HintPurpose _hintPurpose;
    private GameMode? _retryMode;
    private Task? _opening;
    private Task<bool>? _starting;
    private Task? _dialog;
    private Task? _shutdown;
    private bool _closeAccepted;
    private bool _disposed;

    public MainWindow(LauncherApplication application, LauncherActivationServer? activation, Action<Exception> reportFailure,
        PlayScreen? screen = null, IDisplaySettingsStore? settings = null, IGameModeStore? gameModes = null,
        Action<ManagedInstallation, DisplayPreferences>? startClient = null)
    {
        _application = application;
        _activation = activation;
        _reportFailure = reportFailure;
        _settings = settings ?? DisplaySettingsStore.ForCurrentUser();
        _gameModes = gameModes ?? GameModeStore.ForCurrentUser();
        _startClient = startClient ?? ClientProcessLauncher.Start;
        InitializeComponent();
        Title = LauncherText.WindowTitle;

        _screen = screen ?? new PlayScreen(new PlayWebPanel(LauncherHomePage.FromAssembly(Assembly.GetExecutingAssembly()),
            () => !IsClosing, OnOperationFailed));
        _screen.ButtonClicked += OnPlayButtonClicked;
        _screen.BackgroundPressed += OnBackgroundPressed;
        _screen.ContextMenu = CreateContextMenu();
        Content = _screen;

        Opened += OnOpened;
        Closing += OnClosing;
        Closed += (_, _) => _failures.Run(Dispose);
        Render(_application.State);
    }

    internal bool IsShutdownComplete => _closeAccepted;

    private bool IsClosing => _shutdown is not null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        LauncherFailures cleanup = new();
        cleanup.Run(() => _cursor?.Dispose());
        _cursor = null;
        cleanup.Run(_screen.Dispose);
        cleanup.ThrowIfFailed();
    }

    internal Task RequestShutdownAsync() => _shutdown ??= StopAsync();

    private async Task StopAsync()
    {
        // Publish the shared shutdown task before cancellation or Close can reenter the window.
        await Task.Yield();
        _failures.Run(() => _screen.SetStartEnabled(false));
        await Task.WhenAll(
            _failures.RunAsync(_lifetime.CancelAsync),
            _failures.RunAsync(_application.StopAsync),
            _failures.RunAsync(() => _activation?.StopAsync() ?? Task.CompletedTask),
            _failures.RunAsync(StopDisplaySettingsAsync),
            _failures.RunAsync(() => _opening ?? Task.CompletedTask),
            _failures.RunAsync(() => _starting ?? Task.CompletedTask));
        _failures.Run(_lifetime.Dispose);
        _closeAccepted = true;
        _failures.Run(Close);
        _failures.Run(Dispose);

        // The desktop loop can already be stopping. Hand failures directly to its host.
        if (_failures.Failure is { } failure)
        {
            _reportFailure(failure);
        }
    }

    private void OnOperationFailed(Exception failure)
    {
        _failures.Add(failure);
        _ = RequestShutdownAsync();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // CDialog::OnCancel: Escape closes the dialog.
        if (e.Key == Key.Escape && !e.Handled)
        {
            e.Handled = true;
            if (_screen.IsHintVisible)
            {
                HideHint();
            }
            else
            {
                Close();
            }
        }
    }

    private void OnOpened(object? sender, EventArgs eventArgs) =>
        LauncherUiEvent.Run(() => _opening ??= OpenAsync(), OnOperationFailed);

    private async Task OpenAsync()
    {
        await Task.Yield();
        if (IsClosing)
        {
            return;
        }

        PositionLikeCenteredDialog();
        PlayWindowShape.Apply(this);
        _cursor = new PlayCursor(this);

        if (_activation is not null)
        {
            LauncherUiEvent.Run(() => _activation.RunAsync(ActivateAsync), OnOperationFailed);
        }

        Task evaluation = _application.StartAsync();
        CancellationToken token = _lifetime.Token;
        try
        {
            GameModeRecord modes = await _gameModes.LoadAsync(token);
            if (!IsClosing)
            {
                _usage = modes.Usage;
                _screen.ShowUsage(_usage);
                await RenderEvaluationAsync(evaluation);
            }
        }
        catch (OperationCanceledException failure) when (token.IsCancellationRequested && failure.CancellationToken == token)
        {
            // The window closed before its preferences were ready.
        }
    }

    /// <summary><c>CenterWindow</c> centred the 900x650 dialog on the work area; its art is the top-left 800x600.</summary>
    private void PositionLikeCenteredDialog()
    {
        if ((Screens.ScreenFromWindow(this) ?? Screens.Primary) is not { } screen)
        {
            return;
        }

        PixelRect area = screen.WorkingArea;
        double scale = screen.Scaling;
        Position = new PixelPoint(
            area.X + (int)((area.Width - PlayLayout.CenteredDialog.Width * scale) / 2),
            area.Y + (int)((area.Height - PlayLayout.CenteredDialog.Height * scale) / 2));
    }

    private Task<bool> ActivateAsync(CancellationToken cancellationToken)
    {
        return LauncherActivationDispatcher.InvokeAsync(() =>
        {
            if (IsClosing)
            {
                return false;
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Show();
            if (_settingsWindow is not null)
            {
                return _settingsWindow.ActivateSettings();
            }

            Activate();
            return true;
        }, cancellationToken);
    }

    private void OnBackgroundPressed(object? sender, PointerPressedEventArgs e)
    {
        // CPlayDlg_OnLButtonDown_BeginWindowDrag: the dialog moves with a drag from anywhere outside its controls.
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnPlayButtonClicked(object? sender, PlayButton button) =>
        LauncherUiEvent.Run(() => HandlePlayButtonAsync(button), OnOperationFailed);

    private async Task HandlePlayButtonAsync(PlayButton button)
    {
        if (IsClosing)
        {
            return;
        }

        switch (button)
        {
            case PlayButton.High:
                await StartAsync(GameMode.High);
                break;
            case PlayButton.Standard:
                await StartAsync(GameMode.Standard);
                break;
            case PlayButton.Low when _usage.LowNeedsConfirmation:
                // CPlayDlg_OnLowModeButton: confirm until Low Definition has been used once.
                _hintPurpose = HintPurpose.LowConfirmation;
                _screen.ShowHint(PlayText.LowConfirmation, PlayButton.Continue, PlayButton.Return);
                break;
            case PlayButton.Low:
                await StartAsync(GameMode.Low);
                break;
            case PlayButton.Continue:
                await ContinueFromHintAsync();
                break;
            case PlayButton.Return:
                HideHint();
                break;
            case PlayButton.Exit:
                Close();
                break;
            case PlayButton.Site:
            case PlayButton.Register:
                // The retail buttons open publisher-era pages; no OpenConquer pages are configured.
                break;
        }
    }

    private async Task ContinueFromHintAsync()
    {
        HintPurpose purpose = _hintPurpose;
        GameMode? retryMode = _retryMode;
        HideHint();
        if (purpose == HintPurpose.LowConfirmation)
        {
            await StartAsync(GameMode.Low);
        }
        else if (purpose == HintPurpose.Problem && retryMode is { } mode)
        {
            await StartAsync(mode);
        }
    }

    private async Task StartAsync(GameMode mode)
    {
        if (IsClosing || _starting is { IsCompleted: false })
        {
            return;
        }

        Task<bool> starting = TryStartAsync(mode);
        _starting = starting;
        if (await starting)
        {
            // The launch operation has finished. Shutdown can safely join it.
            await RequestShutdownAsync();
        }
    }

    private async Task<bool> TryStartAsync(GameMode mode)
    {
        await Task.Yield();
        if (IsClosing)
        {
            return false;
        }

        _screen.SetStartEnabled(false);
        CancellationToken token = _lifetime.Token;
        bool started = false;
        try
        {
            // A start during the first check waits for it; afterwards every start checks again.
            Task verification = _application.State is LauncherState.EvaluatingInstallation or LauncherState.InstallationResolved
                or LauncherState.InstallationUnavailable
                ? _application.VerifyInstallationAsync()
                : Task.CompletedTask;
            await RenderEvaluationAsync(verification);
            if (IsClosing)
            {
                return false;
            }

            switch (_application.State)
            {
                case LauncherState.InstallationResolved resolved:
                    started = await LaunchAsync(resolved.Installation, mode, token);
                    return started;
                case LauncherState.InstallationUnavailable unavailable:
                    ShowProblem(LauncherText.For(unavailable), mode);
                    break;
                case LauncherState.Faulted faulted:
                    ShowProblem(LauncherText.For(faulted), retryMode: null);
                    break;
            }
        }
        catch (OperationCanceledException failure) when (token.IsCancellationRequested && failure.CancellationToken == token)
        {
            // The launcher closed while preparing the handover.
        }
        finally
        {
            if (!started && !IsClosing)
            {
                _screen.SetStartEnabled(true);
            }
        }

        return false;
    }

    private async Task<bool> LaunchAsync(ManagedInstallation installation, GameMode mode, CancellationToken token)
    {
        // The counters carry no authority: like WritePrivateProfileStringA, a failed write never blocks the start.
        await _gameModes.RecordStartAsync(mode, token);
        if (IsClosing)
        {
            return false;
        }

        DisplayPreferences preferences = await _settings.LoadAsync(token) is SettingsReadResult.Loaded loaded
            ? loaded.Preferences
            : DisplayPreferences.Default;
        if (IsClosing)
        {
            return false;
        }

        try
        {
            _startClient(installation, preferences);
            return true;
        }
        catch (Win32Exception)
        {
            ShowProblem(LauncherText.ClientStartFailed, mode);
            return false;
        }
        catch (InvalidOperationException)
        {
            ShowProblem(LauncherText.ClientStartFailed, mode);
            return false;
        }
    }

    private void ShowProblem((string Title, string Detail) text, GameMode? retryMode)
    {
        _hintPurpose = HintPurpose.Problem;
        _retryMode = retryMode;
        PlayButton[] buttons = retryMode is null ? [PlayButton.Return] : [PlayButton.Continue, PlayButton.Return];
        _screen.ShowHintMessage(text.Title, text.Detail, buttons);
    }

    private void HideHint()
    {
        _hintPurpose = HintPurpose.None;
        _retryMode = null;
        _screen.HideHint();
    }

    private ContextMenu CreateContextMenu()
    {
        MenuItem settings = new()
        {
            Header = LauncherText.DisplaySettings
        };
        settings.Click += OnDisplaySettings;
        MenuItem exit = new()
        {
            Header = LauncherText.Exit
        };
        exit.Click += (_, _) => Close();
        return new ContextMenu { ItemsSource = new[] { settings, exit } };
    }

    private void OnDisplaySettings(object? sender, EventArgs eventArgs)
    {
        if (!IsClosing && _dialog is not { IsCompleted: false })
        {
            LauncherUiEvent.Run(() => _dialog = ShowDisplaySettingsAsync(), OnOperationFailed);
        }
    }

    private async Task ShowDisplaySettingsAsync()
    {
        await Task.Yield();
        if (IsClosing)
        {
            return;
        }

        DisplaySettingsWindow window = new(_settings, OnOperationFailed);
        _settingsWindow = window;
        LauncherFailures cleanup = new();
        try
        {
            await cleanup.RunAsync(() => window.ShowDialog(this));
            await cleanup.RunAsync(window.StopAsync);
        }
        finally
        {
            _settingsWindow = null;
        }
        cleanup.ThrowIfFailed();
    }

    private async Task StopDisplaySettingsAsync()
    {
        if (_settingsWindow is { } window)
        {
            // If the platform close fails, the modal task may never resume. Its I/O is already drained.
            await window.StopAsync();
        }

        if (_dialog is { } dialog)
        {
            await dialog;
        }
    }

    private async Task RenderEvaluationAsync(Task evaluation)
    {
        Render(_application.State);
        try
        {
            await evaluation;
        }
        catch (OperationCanceledException) when (IsClosing && evaluation.IsCanceled)
        {
            return;
        }

        if (!IsClosing)
        {
            Render(_application.State);
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        if (_closeAccepted)
        {
            return;
        }

        eventArgs.Cancel = true;
        _ = RequestShutdownAsync();
    }

    private void Render(LauncherState state)
    {
        if (state is LauncherState.Stopped)
        {
            return;
        }

        // Control 1012 showed the client's version.dat; the launcher shows the verified release it will start.
        _screen.ShowVersion(state is LauncherState.InstallationResolved resolved ? resolved.Installation.Release.Version : null);
    }
}
