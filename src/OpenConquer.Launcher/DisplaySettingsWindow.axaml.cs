using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenConquer.Launcher.Settings;

namespace OpenConquer.Launcher;

internal sealed partial class DisplaySettingsWindow : Window, IAsyncDisposable
{
    private readonly DisplaySettingsSession _session;
    private readonly Action<Exception> _onFailure;
    private SettingsReadResult? _loaded;
    private Task? _stop;
    private Task? _operation;
    private bool _busy;
    private bool _resetInvalid;
    private bool _closing;
    private bool _closeAccepted;

    public DisplaySettingsWindow(IDisplaySettingsStore store, Action<Exception> onFailure)
    {
        _session = new DisplaySettingsSession(store);
        _onFailure = onFailure;
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
        RefreshControls();
    }

    internal bool ActivateSettings()
    {
        if (_closing)
        {
            return false;
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        return true;
    }

    private void OnOpened(object? sender, EventArgs args) => StartLoad();

    private void OnReload(object? sender, RoutedEventArgs args) => StartLoad();

    private void StartLoad()
    {
        if (!_busy && !_closing)
        {
            LauncherUiEvent.Run(() => _operation = BeginLoad(), _onFailure);
        }
    }

    private Task BeginLoad()
    {
        _busy = true;
        _resetInvalid = false;
        Status.Text = "Loading display settings…";
        RefreshControls();
        return LoadAsync();
    }

    private async Task LoadAsync()
    {
        await Task.Yield();
        if (_closing)
        {
            return;
        }

        try
        {
            SettingsReadResult loadedResult = await _session.LoadAsync();
            if (_closing)
            {
                return;
            }

            _loaded = loadedResult;
            if (_loaded is SettingsReadResult.Loaded loaded)
            {
                Populate(loaded.Preferences);
                Status.Text = "Changes are saved only when you choose Save.";
            }
            else if (_loaded is SettingsReadResult.Rejected rejected)
            {
                Status.Text = Describe(rejected.Issue);
            }
        }
        catch (OperationCanceledException failure) when (_session.IsStoppingCancellation(failure))
        {
            // Closing this settings session canceled the operation.
        }
        finally
        {
            _busy = false;
            if (!_closing)
            {
                RefreshControls();
            }
        }
    }

    private void Populate(DisplayPreferences preferences)
    {
        WidthInput.Text = preferences.Width.ToString(CultureInfo.InvariantCulture);
        HeightInput.Text = preferences.Height.ToString(CultureInfo.InvariantCulture);
        ModeInput.SelectedIndex = preferences.WindowMode switch
        {
            GameWindowMode.Resizable => 0,
            GameWindowMode.Fixed => 1,
            GameWindowMode.Fullscreen => 2,
            _ => throw new InvalidOperationException("Invalid window mode.")
        };
        PresentationInput.SelectedIndex = preferences.Presentation switch
        {
            GamePresentation.Fit => 0,
            GamePresentation.Integer => 1,
            GamePresentation.Stretch => 2,
            _ => throw new InvalidOperationException("Invalid presentation.")
        };
    }

    private void OnDefaults(object? sender, RoutedEventArgs args)
    {
        if (_busy || _closing || !CanReset)
        {
            return;
        }

        _resetInvalid = _loaded is SettingsReadResult.Rejected;
        Populate(DisplayPreferences.Default);
        Status.Text = "Default values restored. Choose Save to replace your saved preferences.";
        RefreshControls();
    }

    private void OnSave(object? sender, RoutedEventArgs args) => LauncherUiEvent.Run(BeginSave, _onFailure);

    private Task BeginSave()
    {
        if (_busy || _closing || !CanEdit)
        {
            return Task.CompletedTask;
        }

        if (!TryGetPreferences(out DisplayPreferences? preferences) || preferences is null)
        {
            Status.Text = "Enter positive whole-pixel dimensions, up to 16384 per side and 33,177,600 total pixels.";
            WidthInput.Focus();
            return Task.CompletedTask;
        }
        _busy = true;
        Status.Text = "Saving display settings…";
        RefreshControls();
        return _operation = SaveAsync(preferences);
    }

    private async Task SaveAsync(DisplayPreferences preferences)
    {
        await Task.Yield();
        if (_closing)
        {
            return;
        }

        string? revision = _loaded switch
        {
            SettingsReadResult.Loaded loaded => loaded.Revision,
            SettingsReadResult.Rejected rejected => rejected.Revision,
            _ => throw new InvalidOperationException("Settings have not been loaded.")
        };
        try
        {
            SettingsIssue? failure = await _session.SaveAsync(preferences, revision, _resetInvalid);
            if (_closing)
            {
                return;
            }

            if (failure is null)
            {
                Close();
                return;
            }
            Status.Text = failure == SettingsIssue.Unavailable
                ? "Could not save display settings. Your changes are still here; check access and try Save again."
                : Describe(failure.Value);
            if (failure is SettingsIssue.Changed or SettingsIssue.NewerVersion)
            {
                _loaded = new SettingsReadResult.Rejected(failure.Value);
            }
        }
        catch (OperationCanceledException failure) when (_session.IsStoppingCancellation(failure))
        {
            // Closing this settings session canceled the operation.
        }
        finally
        {
            _busy = false;
            if (!_closing)
            {
                RefreshControls();
            }
        }
    }

    private bool IsDraftValid(out int width, out int height)
    {
        height = 0;
        return int.TryParse(WidthInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out width) &&
            int.TryParse(HeightInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out height) &&
            DisplayPreferences.IsValidSize(width, height) && ModeInput.SelectedIndex is >= 0 and <= 2 &&
            PresentationInput.SelectedIndex is >= 0 and <= 2;
    }

    private bool TryGetPreferences(out DisplayPreferences? preferences)
    {
        preferences = null;
        if (!IsDraftValid(out int width, out int height))
        {
            return false;
        }

        preferences = new DisplayPreferences(width, height,
            ModeInput.SelectedIndex switch
            {
                0 => GameWindowMode.Resizable,
                1 => GameWindowMode.Fixed,
                _ => GameWindowMode.Fullscreen
            },
            PresentationInput.SelectedIndex switch
            {
                0 => GamePresentation.Fit,
                1 => GamePresentation.Integer,
                _ => GamePresentation.Stretch
            });
        return true;
    }

    private void OnEdited(object? sender, TextChangedEventArgs args) => Edited();
    private void OnSelectionEdited(object? sender, SelectionChangedEventArgs args) => Edited();

    private void Edited()
    {
        RefreshControls();
        if (!_busy && !_closing && CanEdit && IsDraftValid(out _, out _))
        {
            Status.Text = "Changes are saved only when you choose Save.";
        }
    }
    private void OnCancel(object? sender, RoutedEventArgs args) => Close();
    private bool CanReset => _loaded is SettingsReadResult.Loaded or SettingsReadResult.Rejected { Issue: SettingsIssue.Invalid, Revision: not null };
    private bool CanEdit => _loaded is SettingsReadResult.Loaded || (_resetInvalid && CanReset);

    private void RefreshControls()
    {
        if (SaveButton is null)
        {
            return; // XAML initialization raises selection/text events before all named controls exist.
        }

        Fields.IsEnabled = !_busy && !_closing && CanEdit;
        SaveButton.IsEnabled = !_busy && !_closing && CanEdit;
        DefaultsButton.IsEnabled = !_busy && !_closing && CanReset;
        ReloadButton.IsEnabled = !_busy && !_closing;
    }

    public Task StopAsync() => _stop ??= StopCoreAsync();

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task StopCoreAsync()
    {
        _closing = true;
        // Publish the stop task before Close can resume the owner's ShowDialog continuation.
        await Task.Yield();

        LauncherFailures cleanup = new();
        cleanup.Run(RefreshControls);
        await Task.WhenAll(
            cleanup.RunAsync(_session.StopAsync),
            cleanup.RunAsync(() => _operation ?? Task.CompletedTask));
        _closeAccepted = true;
        cleanup.Run(Close);
        cleanup.ThrowIfFailed();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_closeAccepted)
        {
            return;
        }

        args.Cancel = true;
        LauncherUiEvent.Run(StopAsync, _onFailure);
    }

    private static string Describe(SettingsIssue issue) => issue switch
    {
        SettingsIssue.Invalid => "Saved display settings are damaged. Restore defaults, then choose Save to replace them.",
        SettingsIssue.NewerVersion => "These settings require a newer launcher. They have been left unchanged.",
        SettingsIssue.Changed => "Settings changed outside this window. Choose Reload before saving again.",
        SettingsIssue.Unavailable => "Display settings could not be read. Check access or move the unsupported settings file aside, then Reload. The file has been left unchanged.",
        _ => throw new ArgumentOutOfRangeException(nameof(issue))
    };
}
