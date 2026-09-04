using Avalonia;
using Avalonia.Controls;

namespace OpenConquer.Launcher;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
    }

    /// <summary>
    /// Configures the Avalonia application without starting its desktop lifetime.
    /// </summary>
    /// <remarks>
    /// This method intentionally remains separate from <see cref="Main"/> because Avalonia tooling
    /// and future host-level tests may need to construct the application without entering the
    /// native desktop event loop.
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect();
    }
}
