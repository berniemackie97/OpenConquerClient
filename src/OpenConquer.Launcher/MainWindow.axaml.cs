using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher;

internal sealed partial class MainWindow : Window
{
    private readonly InstallationSession _installation;
    private CancellationTokenSource? _inspectionCancellation;
    private Task? _activeInspection;
    private bool _pickingFolder;
    private bool _closing;
    private bool _closed;

    public MainWindow(InstallationSession installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        _installation = installation;
        InitializeComponent();
        InstallationPath.TextChanged += OnInstallationPathChanged;
        Render();
        Closing += OnWindowClosing;
        Closed += (_, _) => _closed = true;
    }

    private void OnInstallationPathChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        if (_activeInspection is null && !_closing)
        {
            _installation.ClearSelection();
            PickerError.IsVisible = false;
            Render();
        }
    }

    private async void OnCheckClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (_activeInspection is not null || _pickingFolder || _closing)
        {
            return;
        }

        PickerError.IsVisible = false;
        using CancellationTokenSource cancellation = new();
        _inspectionCancellation = cancellation;
        Task inspection = _installation.InspectAsync(InstallationPath.Text, cancellation.Token);
        _activeInspection = inspection;
        Render();
        try
        {
            await inspection;
        }
        finally
        {
            _inspectionCancellation = null;
            _activeInspection = null;
            if (!_closed)
            {
                Render();
            }
        }

        if (_closing)
        {
            Close();
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs eventArgs)
    {
        _inspectionCancellation?.Cancel();
    }

    private async void OnBrowseClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (_pickingFolder || _activeInspection is not null || _closing)
        {
            return;
        }

        if (!StorageProvider.CanPickFolder)
        {
            ShowPickerError("Folder browsing is unavailable. Enter the full folder path instead.");
            return;
        }

        _pickingFolder = true;
        bool folderSelected = false;
        PickerError.IsVisible = false;
        Render();
        try
        {
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose your OpenConquer game installation",
                AllowMultiple = false,
            });
            try
            {
                if (!_closed && !_closing && folders.Count > 0)
                {
                    string? path = folders[0].TryGetLocalPath();
                    if (path is null)
                    {
                        ShowPickerError("Choose a local folder, or enter its full path directly.");
                    }
                    else
                    {
                        InstallationPath.Text = path;
                        folderSelected = true;
                    }
                }
            }
            finally
            {
                foreach (IStorageFolder folder in folders)
                {
                    folder.Dispose();
                }
            }
        }
        catch (IOException)
        {
            ShowPickerError("The folder picker could not read this location. Enter the full path instead.");
        }
        catch (UnauthorizedAccessException)
        {
            ShowPickerError("Folder access was denied. Choose an accessible folder or enter its path.");
        }
        finally
        {
            _pickingFolder = false;
            if (!_closed)
            {
                Render();
            }
        }

        if (_closing)
        {
            Close();
        }
        else if (folderSelected)
        {
            CheckButton.Focus();
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        if (_activeInspection is null && !_pickingFolder)
        {
            return;
        }

        // The owning event handler drains its operation and disposes its resources, then closes
        // the window. A second await here would report the same unexpected failure twice.
        eventArgs.Cancel = true;
        _closing = true;
        Render();
        _inspectionCancellation?.Cancel();
    }

    private void ShowPickerError(string message)
    {
        if (!_closed && !_closing)
        {
            PickerError.Text = message;
            PickerError.IsVisible = true;
        }
    }

    private void Render()
    {
        InstallationState state = _installation.State;
        (StatusTitle.Text, StatusDetail.Text) = InstallationStatusText.For(state);
        bool checking = state is InstallationState.Checking;
        bool busy = _activeInspection is not null || _pickingFolder || _closing;
        CheckButton.IsEnabled = !busy;
        BrowseButton.IsEnabled = !busy;
        InstallationPath.IsEnabled = !busy;
        CancelButton.IsVisible = checking;
        CancelButton.IsEnabled = checking && !_closing;
        InspectionProgress.IsVisible = checking;
    }
}
