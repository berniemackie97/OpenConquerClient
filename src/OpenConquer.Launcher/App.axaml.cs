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
                LauncherApplication application = new(new ManagedInstallationResolver(AppContext.BaseDirectory));
                desktop.MainWindow = new MainWindow(application, _activation);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
