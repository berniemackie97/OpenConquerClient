using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using OpenConquer.Client.Diagnostics;
using OpenConquer.Client.Startup;

namespace OpenConquer.Client;

internal static class Program
{
    private const int ClientFailureExitCode = 1;
    private const int InvalidStartupArgumentsExitCode = 2;

    private static int Main(string[] args)
    {
        using ILoggerFactory factory = LoggerFactory.Create(static builder => builder.AddConsole(static options =>
        {
            options.FormatterName = ConsoleFormatterNames.Json;
            options.LogToStandardErrorThreshold = LogLevel.Trace;
            options.QueueFullMode = ConsoleLoggerQueueFullMode.DropWrite;
            options.MaxQueueLength = 256;
        }));
        ClientDiagnostics diagnostics = new(factory.CreateLogger("OpenConquer.Client"));
        try
        {
            if (!ClientStartupOptions.TryParse(args, out ClientStartupOptions? options, out ClientStartupArgumentCode errorCode))
            {
                diagnostics.StartupArgumentsRejected((int)errorCode);
                return InvalidStartupArgumentsExitCode;
            }

            return RunClient(options, diagnostics);
        }
        catch (Exception failure)
        {
            diagnostics.ClientStopped(ClientFailure.Classify(failure));
            return ClientFailureExitCode;
        }
    }

    private static int RunClient(ClientStartupOptions options, ClientDiagnostics diagnostics)
    {
        ClientApplication application = new(options.ContentRootPath, options.PresentationPolicy,
            options.WindowMode, options.WindowSize, diagnostics: diagnostics);
        int exitCode;
        try
        {
            exitCode = application.Run();
        }
        catch
        {
            try
            {
                application.Dispose();
            }
            catch (Exception cleanup)
            {
                diagnostics.ClientCleanupFailed(ClientFailure.Classify(cleanup));
            }

            throw;
        }

        application.Dispose();
        return exitCode;
    }
}
