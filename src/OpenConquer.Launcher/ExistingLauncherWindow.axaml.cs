using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenConquer.Launcher;

internal sealed partial class ExistingLauncherWindow : Window
{
    public ExistingLauncherWindow()
    {
        InitializeComponent();
        Title = LauncherText.WindowTitle;
        StatusTitle.Text = LauncherText.AlreadyRunningTitle;
        StatusDetail.Text = LauncherText.AlreadyRunningDetail;
        CloseButton.Content = LauncherText.Close;
    }

    private void OnCloseRequested(object? sender, RoutedEventArgs eventArgs) => Close();
}
