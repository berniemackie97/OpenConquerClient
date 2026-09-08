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

    public App()
    {
    }

    internal App(LauncherActivationServer? activation, LauncherInstanceAdmission admission)
    {
        _activation = activation;
        _admission = admission;
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
                desktop.MainWindow = new MainWindow(application, _activation);
                desktop.ShutdownRequested += OnShutdownRequested;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs args)
    {
        if (sender is IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow window } && !window.IsShutdownComplete)
        {
            args.Cancel = true;
            await window.RequestShutdownAsync();
        }
    }
}
