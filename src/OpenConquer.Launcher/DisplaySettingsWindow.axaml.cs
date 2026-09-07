using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenConquer.Launcher.Settings;

namespace OpenConquer.Launcher;

internal sealed partial class DisplaySettingsWindow : Window, IAsyncDisposable
{
    private readonly DisplaySettingsSession _session;
    private SettingsReadResult? _loaded;
    private Task? _stop;
    private bool _busy;
    private bool _resetInvalid;
    private bool _closing;
    private bool _closeAccepted;

    public DisplaySettingsWindow(DisplaySettingsStore store)
    {
        _session = new DisplaySettingsSession(store);
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

    private async void OnOpened(object? sender, EventArgs args)
    {
        if (!_closing)
        {
            await BeginLoad();
        }
    }
    private async void OnReload(object? sender, RoutedEventArgs args)
    {
        if (!_busy && !_closing)
        {
            await BeginLoad();
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
        try
        {
            _loaded = await _session.LoadAsync();
            if (_closing)
            {
                return;
            }

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
        catch (OperationCanceledException) when (_closing) { }
        finally { _busy = false; RefreshControls(); }
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

    private async void OnSave(object? sender, RoutedEventArgs args)
    {
        if (_busy || _closing || !CanEdit)
        {
            return;
        }

        if (!TryGetPreferences(out DisplayPreferences? preferences) || preferences is null)
        {
            Status.Text = "Enter positive whole-pixel dimensions, up to 16384 per side and 33,177,600 total pixels.";
            WidthInput.Focus();
            return;
        }
        _busy = true;
        Status.Text = "Saving display settings…";
        RefreshControls();
        await SaveAsync(preferences);
    }

    private async Task SaveAsync(DisplayPreferences preferences)
    {
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
        catch (OperationCanceledException) when (_closing) { }
        finally { _busy = false; RefreshControls(); }
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

    public Task StopAsync()
    {
        if (_stop is not null)
        {
            return _stop;
        }

        // Publish before Close can synchronously resume the owner's ShowDialog continuation.
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _stop = completion.Task;
        _closing = true;
        RefreshControls();
        _ = StopCoreAsync(completion);
        return _stop;
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task StopCoreAsync(TaskCompletionSource completion)
    {
        try
        {
            try
            {
                await _session.StopAsync();
            }
            finally
            {
                _closeAccepted = true;
                Close();
            }
            completion.SetResult();
        }
        catch (Exception exception) { completion.SetException(exception); }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_closeAccepted)
        {
            return;
        }

        args.Cancel = true;
        await StopAsync();
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
