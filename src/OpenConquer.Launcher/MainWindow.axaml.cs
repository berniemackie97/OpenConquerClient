using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenConquer.Launcher;

internal sealed partial class MainWindow : Window
{
    private readonly LauncherApplication _application;
    private bool _closing;
    private bool _closeAccepted;

    public MainWindow(LauncherApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _application = application;
        InitializeComponent();
        Title = LauncherText.WindowTitle;
        ProductName.Text = LauncherText.ProductName.ToUpperInvariant();
        InstallationHeading.Text = LauncherText.InstallationHeading;
        RetryInstallationButton.Content = LauncherText.CheckAgain;
        CloseButton.Content = LauncherText.Close;
        Opened += OnOpened;
        Closing += OnClosing;
        Render(_application.State);
    }

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        if (_closing)
        {
            return;
        }

        await RenderEvaluationAsync(_application.StartAsync());
    }

    private async void OnRetryInstallation(object? sender, RoutedEventArgs eventArgs)
    {
        if (_closing || _application.State is not LauncherState.InstallationUnavailable)
        {
            return;
        }

        await RenderEvaluationAsync(_application.RetryInstallationAsync());
    }

    private void OnCloseRequested(object? sender, RoutedEventArgs eventArgs)
    {
        Close();
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

        if (_closing)
        {
            eventArgs.Cancel = true;
            return;
        }

        eventArgs.Cancel = true;
        _closing = true;
        Task stopping = _application.StopAsync();
        Render(_application.State);

        try
        {
            await stopping;
        }
        catch
        {
            _closeAccepted = true;
            Close();
            throw;
        }

        _closeAccepted = true;
        Close();
    }

    private void Render(LauncherState state)
    {
        if (state is LauncherState.Stopped)
        {
            return;
        }

        (StatusTitle.Text, StatusDetail.Text) = LauncherText.For(state);
        RetryInstallationButton.IsEnabled = state is LauncherState.InstallationUnavailable && !_closing;
        CloseButton.IsEnabled = !_closing;
    }
}
