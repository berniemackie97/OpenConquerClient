using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using OpenConquer.Launcher.Diagnostics;

namespace OpenConquer.Launcher;

internal static class Program
{
    private const int FatalHostFailureExitCode = 1;

    [STAThread]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The executable entry point is the final process boundary and must convert an otherwise-unhandled launcher failure into a diagnostic event and nonzero exit code.")]
    public static int Main(string[] args)
    {
        using LauncherDiagnostics diagnostics = LauncherDiagnostics.Create();
        using LauncherHostExceptionObserver exceptionObserver = new(diagnostics);

        try
        {
            exceptionObserver.Start();
            diagnostics.RecordHostStarted();

            int exitCode = BuildRuntimeAvaloniaApp(exceptionObserver).StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);

            diagnostics.RecordHostStopped(exitCode);

            return exitCode;
        }
        catch (Exception exception)
        {
            LauncherExceptionDomain domain = exceptionObserver.ClassifyTopLevelException(exception);
            diagnostics.RecordException(domain, isTerminating: true, exception);

            return FatalHostFailureExitCode;
        }
    }

    /// <summary>
    /// Configures the Avalonia application without starting its desktop lifetime.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect();
    }

    private static AppBuilder BuildRuntimeAvaloniaApp(LauncherHostExceptionObserver exceptionObserver)
    {
        ArgumentNullException.ThrowIfNull(exceptionObserver);

        return BuildAvaloniaApp().AfterSetup(_ => exceptionObserver.AttachUiDispatcher(Dispatcher.UIThread));
    }
}
