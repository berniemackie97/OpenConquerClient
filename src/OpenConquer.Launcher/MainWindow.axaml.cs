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
    private readonly DisplaySettingsStore _settings = DisplaySettingsStore.ForCurrentUser();
    private readonly GameModeStore _gameModes = GameModeStore.ForCurrentUser();
    private readonly PlayScreen _screen;
    private PlayCursor? _cursor;
    private DisplaySettingsWindow? _settingsWindow;
    private GameModeUsage _usage;
    private HintPurpose _hintPurpose;
    private GameMode? _retryMode;
    private bool _starting;
    private bool _closing;
    private bool _closeAccepted;

    public MainWindow(LauncherApplication application, LauncherActivationServer? activation = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        _application = application;
        _activation = activation;
        InitializeComponent();
        Title = LauncherText.WindowTitle;

        _screen = new PlayScreen(new PlayWebPanel(LauncherHomePage.FromAssembly(Assembly.GetExecutingAssembly())));
        _screen.ButtonClicked += OnPlayButtonClicked;
        _screen.BackgroundPressed += OnBackgroundPressed;
        _screen.ContextMenu = CreateContextMenu();
        Content = _screen;

        Opened += OnOpened;
        Closing += OnClosing;
        Closed += (_, _) => Dispose();
        Render(_application.State);
    }

    internal bool IsShutdownComplete => _closeAccepted;

    public void Dispose()
    {
        _cursor?.Dispose();
        _cursor = null;
        _screen.Dispose();
    }

    internal async Task RequestShutdownAsync()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Task stopping = Task.WhenAll(_application.StopAsync(), _activation?.StopAsync() ?? Task.CompletedTask, _settingsWindow?.StopAsync() ?? Task.CompletedTask);
        _screen.SetStartEnabled(false);
        try
        {
            await stopping;
        }
        finally
        {
            _closeAccepted = true;
            Close();
        }
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

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        if (_closing)
        {
            return;
        }

        PositionLikeCenteredDialog();
        PlayWindowShape.Apply(this);
        _cursor = new PlayCursor(this);

        if (_activation is not null)
        {
            ObserveActivationAsync(_activation.RunAsync(ActivateAsync));
        }

        Task evaluation = _application.StartAsync();
        _usage = (await _gameModes.LoadAsync(CancellationToken.None)).Usage;
        _screen.ShowUsage(_usage);
        await RenderEvaluationAsync(evaluation);
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

    private static async void ObserveActivationAsync(Task listener)
    {
        // Started by Opened on the UI dispatcher: unexpected listener failures reach the existing
        // fatal host boundary immediately, rather than waiting until process shutdown to surface.
        await listener;
    }

    private Task<bool> ActivateAsync(CancellationToken cancellationToken)
    {
        return LauncherActivationDispatcher.InvokeAsync(() =>
        {
            if (_closing)
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

    private async void OnPlayButtonClicked(object? sender, PlayButton button)
    {
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
        if (_starting || _closing)
        {
            return;
        }

        _starting = true;
        _screen.SetStartEnabled(false);
        try
        {
            // A start during the first check waits for it; afterwards every start checks again.
            Task verification = _application.State is LauncherState.EvaluatingInstallation or LauncherState.InstallationResolved
                or LauncherState.InstallationUnavailable
                ? _application.VerifyInstallationAsync()
                : Task.CompletedTask;
            await RenderEvaluationAsync(verification);
            if (_closing)
            {
                return;
            }

            switch (_application.State)
            {
                case LauncherState.InstallationResolved resolved:
                    await LaunchAsync(resolved.Installation, mode);
                    break;
                case LauncherState.InstallationUnavailable unavailable:
                    ShowProblem(LauncherText.For(unavailable), mode);
                    break;
                case LauncherState.Faulted faulted:
                    ShowProblem(LauncherText.For(faulted), retryMode: null);
                    break;
                default:
                    break;
            }
        }
        finally
        {
            _starting = false;
            if (!_closing)
            {
                _screen.SetStartEnabled(true);
            }
        }
    }

    private async Task LaunchAsync(ManagedInstallation installation, GameMode mode)
    {
        // The counters carry no authority: like WritePrivateProfileStringA, a failed write never blocks the start.
        await _gameModes.RecordStartAsync(mode, CancellationToken.None);
        DisplayPreferences preferences = await _settings.LoadAsync(CancellationToken.None) is SettingsReadResult.Loaded loaded
            ? loaded.Preferences
            : DisplayPreferences.Default;

        try
        {
            ClientProcessLauncher.Start(installation, preferences);
        }
        catch (Win32Exception)
        {
            ShowProblem(LauncherText.ClientStartFailed, mode);
            return;
        }
        catch (InvalidOperationException)
        {
            ShowProblem(LauncherText.ClientStartFailed, mode);
            return;
        }

        await RequestShutdownAsync();
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

    private async void OnDisplaySettings(object? sender, EventArgs eventArgs)
    {
        if (_closing || _settingsWindow is not null)
        {
            return;
        }

        DisplaySettingsWindow window = new(_settings);
        _settingsWindow = window;
        try
        {
            await window.ShowDialog(this);
        }
        finally
        {
            try
            {
                await window.StopAsync();
            }
            finally { _settingsWindow = null; }
        }
    }

    private async Task RenderEvaluationAsync(Task evaluation)
    {
        Render(_application.State);
        try
        {
            await evaluation;
        }
        catch (OperationCanceledException) when (_closing)
        {
            return;
        }

        if (!_closing)
        {
            Render(_application.State);
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        if (_closeAccepted)
        {
            return;
        }

        eventArgs.Cancel = true;
        await RequestShutdownAsync();
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
