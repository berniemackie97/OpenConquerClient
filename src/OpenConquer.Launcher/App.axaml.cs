using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenConquer.Launcher.Installation;
using OpenConquer.Launcher.Instances;

namespace OpenConquer.Launcher;

internal sealed partial class App : Application
{
    private readonly LauncherActivationServer? _activation;
    private readonly LauncherInstanceAdmission _admission;
    private readonly Action<Exception> _reportFailure = LauncherUiEvent.RethrowOnDispatcher;

    public App()
    {
    }

    internal App(LauncherActivationServer? activation, LauncherInstanceAdmission admission, Action<Exception>? reportFailure = null)
    {
        _activation = activation;
        _admission = admission;
        _reportFailure = reportFailure ?? LauncherUiEvent.RethrowOnDispatcher;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (_admission == LauncherInstanceAdmission.ExistingUnavailable)
            {
                desktop.MainWindow = new ExistingLauncherWindow();
            }
            else
            {
                TrustedReleaseKeys trustedReleaseKeys = TrustedReleaseKeys.LoadEmbedded(Assembly.GetExecutingAssembly());
                LauncherApplication application = new(new ManagedInstallationResolver(AppContext.BaseDirectory, trustedReleaseKeys));
                desktop.MainWindow = new MainWindow(application, _activation, _reportFailure);
                desktop.ShutdownRequested += OnShutdownRequested;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs args)
    {
        if (sender is IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow window } && !window.IsShutdownComplete)
        {
            args.Cancel = true;
            _ = window.RequestShutdownAsync();
        }
    }
}
