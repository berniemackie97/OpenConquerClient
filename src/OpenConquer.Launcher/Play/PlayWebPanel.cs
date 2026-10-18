using Avalonia.Controls;
using Avalonia.Platform;
using OpenConquer.Launcher.Settings;

namespace OpenConquer.Launcher.Play;

/// <summary>
/// Control 1000: the platform web view showing the launcher home page inside the parchment. Browsing data is
/// never persisted, script messaging and developer tools stay off, and links that leave the home page's site
/// open in the system browser.
/// </summary>
internal sealed class PlayWebPanel : Border
{
    private readonly LauncherHomePage _homePage;
    private readonly NativeWebView _view;

    public PlayWebPanel(LauncherHomePage homePage)
    {
        _homePage = homePage ?? throw new ArgumentNullException(nameof(homePage));
        ClipToBounds = true;
        _view = new NativeWebView();
        _view.EnvironmentRequested += OnEnvironmentRequested;
        _view.NavigationStarted += OnNavigationStarted;
        _view.NewWindowRequested += OnNewWindowRequested;
        _view.Source = homePage.Address;
        Child = _view;
    }

    private static void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
        e.EnableDevTools = false;
        switch (e)
        {
            case WindowsWebView2EnvironmentRequestedEventArgs windows:
                windows.IsInPrivateModeEnabled = true;
                // WebView2 needs a writable folder even in private mode; the default sits beside the executable.
                if (LauncherConfigurationDirectory.ForCurrentUser() is { } directory)
                {
                    windows.UserDataFolder = Path.Combine(directory, "WebView");
                }

                break;
            case AppleWKWebViewEnvironmentRequestedEventArgs apple:
                apple.NonPersistentDataStore = true;
                break;
            case GtkWebViewEnvironmentRequestedEventArgs gtk:
                gtk.EphemeralDataManager = true;
                break;
        }
    }

    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (e.Request is not { } target || _homePage.KeepsInPanel(target))
        {
            return;
        }

        e.Cancel = true;
        OpenExternally(target);
    }

    private void OnNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.Request is { } target)
        {
            OpenExternally(target);
        }
    }

    private void OpenExternally(Uri target)
    {
        if (LauncherHomePage.OpensExternally(target) && TopLevel.GetTopLevel(this) is { } topLevel)
        {
            _ = topLevel.Launcher.LaunchUriAsync(target);
        }
    }
}
