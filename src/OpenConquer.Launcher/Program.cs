using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using OpenConquer.Launcher.Diagnostics;
using OpenConquer.Launcher.Instances;

namespace OpenConquer.Launcher;

internal static class Program
{
    private const int FatalHostFailureExitCode = 1;

    [STAThread]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The executable entry point is the final process boundary and must convert an otherwise-unhandled launcher failure into a diagnostic event and nonzero exit code.")]
    public static int Main(string[] args)
    {
        try
        {
            LauncherInstanceIdentity identity = LauncherInstanceIdentity.ForCurrentUser();
            using LauncherProcessLease lease = new(identity.LeaseName);
            LauncherInstanceAdmission admission = LauncherInstanceStartup.Enter(lease, identity.PipeName);
            if (admission == LauncherInstanceAdmission.ActivatedExisting)
            {
                return 0;
            }

            if (admission == LauncherInstanceAdmission.ExistingUnavailable)
            {
                AppBuilder.Configure(() => new App(null, admission)).UsePlatformDetect()
                    .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
                return 2;
            }

            return RunDesktop(args, identity);
        }
        catch (Exception exception)
        {
            using LauncherDiagnostics diagnostics = LauncherDiagnostics.Create();
            diagnostics.RecordException(LauncherExceptionDomain.TopLevel, isTerminating: true, exception);
            return FatalHostFailureExitCode;
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This host boundary preserves desktop and cleanup failures for redacted diagnostics and a nonzero process result.")]
    private static int RunDesktop(string[] args, LauncherInstanceIdentity identity)
    {
        using LauncherDiagnostics diagnostics = LauncherDiagnostics.Create();
        using LauncherHostExceptionObserver exceptionObserver = new(diagnostics);
        LauncherActivationServer? activation = null;
        LauncherFailures failures = new();
        LauncherExceptionDomain failureDomain = LauncherExceptionDomain.TopLevel;
        int exitCode = FatalHostFailureExitCode;
        try
        {
            activation = new LauncherActivationServer(identity.PipeName);

            exceptionObserver.Start();
            diagnostics.RecordHostStarted();
            exitCode = BuildRuntimeAvaloniaApp(exceptionObserver, activation, failure =>
            {
                failures.Add(failure);
                failureDomain = LauncherExceptionDomain.UiDispatcher;
                LauncherUiEvent.RethrowOnDispatcher(failure);
            })
                .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
            failureDomain = exceptionObserver.ClassifyTopLevelException(exception);
        }
        finally
        {
            // The UI loop has exited. The listener and its cancellation drain without a UI context.
            failures.Run(() => activation?.StopAsync().GetAwaiter().GetResult());
        }

        if (failures.Failure is { } failure)
        {
            diagnostics.RecordException(failureDomain, isTerminating: true, failure);
            return FatalHostFailureExitCode;
        }

        diagnostics.RecordHostStopped(exitCode);
        return exitCode;
    }

    /// <summary>
    /// Configures the Avalonia application without starting its desktop lifetime.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect();
    }

    private static AppBuilder BuildRuntimeAvaloniaApp(LauncherHostExceptionObserver exceptionObserver,
        LauncherActivationServer activation, Action<Exception> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(exceptionObserver);

        return AppBuilder.Configure(() => new App(activation, LauncherInstanceAdmission.Primary, reportFailure)).UsePlatformDetect()
            .AfterSetup(_ => exceptionObserver.AttachUiDispatcher(Dispatcher.UIThread));
    }
}
