using Avalonia.Controls;

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

        Task evaluation = _application.StartAsync();
        Render(_application.State);
        try
        {
            await evaluation;
        }
        catch (OperationCanceledException) when (_closing)
        {
            return;
        }

        Render(_application.State);
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
            // Shutdown still owns the close decision. Preserve the failure for the host observer,
            // but do not leave a window open after the close request has already been accepted.
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
            // Stopped is an application-lifecycle terminal state. The window closes after the
            // shutdown drain; it must never remain visible as a stale "closed" screen.
            return;
        }

        (StatusTitle.Text, StatusDetail.Text) = LauncherText.For(state);
    }
}
