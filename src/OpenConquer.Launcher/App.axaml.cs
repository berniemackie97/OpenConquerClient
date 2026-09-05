using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher;

internal sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            LauncherApplication application = new(
                new ManagedInstallationResolver(AppContext.BaseDirectory)
            );
            desktop.MainWindow = new MainWindow(application);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
