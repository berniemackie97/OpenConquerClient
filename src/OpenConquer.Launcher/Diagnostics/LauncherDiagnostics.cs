using System.Diagnostics.CodeAnalysis;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace OpenConquer.Launcher.Diagnostics;

internal sealed class LauncherDiagnostics : IDisposable
{
    private const long FileSizeLimitBytes = 5 * 1024 * 1024;
    private const int RetainedFileCountLimit = 14;
    private const string LogFileName = "launcher-.jsonl";

    private readonly object _lifetimeGate = new();
    private readonly Logger _logger;
    private bool _disposed;

    private LauncherDiagnostics(Logger logger)
    {
        _logger = logger;
    }

    public static LauncherDiagnostics Create()
    {
        return !LauncherDiagnosticPaths.TryGetLogDirectory(out string? logDirectory) ? CreateFallback() : Create(logDirectory);
    }

    internal static LauncherDiagnostics Create(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

        if (!Path.IsPathFullyQualified(logDirectory))
        {
            throw new ArgumentException("The diagnostic log directory must be fully qualified.", nameof(logDirectory));
        }

        try
        {
            try
            {
                Directory.CreateDirectory(logDirectory);
            }
            catch (ArgumentException)
            {
                return CreateFallback();
            }
            catch (NotSupportedException)
            {
                return CreateFallback();
            }

            return new LauncherDiagnostics(CreatePersistentLogger(logDirectory));
        }
        catch (IOException)
        {
            return CreateFallback();
        }
        catch (UnauthorizedAccessException)
        {
            return CreateFallback();
        }
    }

    public void RecordHostStarted()
    {
        lock (_lifetimeGate)
        {
            ThrowIfDisposed();

            TryWrite(static logger => logger.Information("Launcher host started."));
        }
    }

    public void RecordHostStopped(int exitCode)
    {
        lock (_lifetimeGate)
        {
            ThrowIfDisposed();

            TryWrite(logger => logger.Information("Launcher host stopped with exit code {ExitCode}.", exitCode));
        }
    }

    public void RecordException(LauncherExceptionDomain domain, bool isTerminating, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (_lifetimeGate)
        {
            ThrowIfDisposed();

            TryWrite(logger =>
            {
                LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(exception);

                LogEventLevel level = isTerminating ? LogEventLevel.Fatal : LogEventLevel.Error;

                logger.Write(level, "Launcher exception observed in {ExceptionDomain}; terminating: {IsTerminating}. {@ExceptionDiagnostic}", domain.ToString(), isTerminating, diagnostic);
            });
        }
    }

    public void Dispose()
    {
        lock (_lifetimeGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            TryDisposeLogger();
        }
    }

    private static Logger CreatePersistentLogger(string logDirectory)
    {
        string logFilePath = Path.Combine(logDirectory, LogFileName);

        string applicationVersion = typeof(LauncherDiagnostics).Assembly.GetName().Version?.ToString() ?? "unknown";

        return new LoggerConfiguration().MinimumLevel.Is(LogEventLevel.Information)
            .Destructure.ToMaximumDepth(32)
            .Enrich.WithProperty("Application", "OpenConquer.Launcher")
            .Enrich.WithProperty("ApplicationVersion", applicationVersion)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .WriteTo.File(new JsonFormatter(renderMessage: true), logFilePath, rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: FileSizeLimitBytes, rollOnFileSizeLimit: true, retainedFileCountLimit: RetainedFileCountLimit,
                buffered: false, shared: false)
            .CreateLogger();
    }

    private static LauncherDiagnostics CreateFallback()
    {
        Logger logger = new LoggerConfiguration().CreateLogger();
        return new LauncherDiagnostics(logger);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Diagnostic emission is best-effort and must never become an availability dependency for the launcher.")]
    private void TryWrite(Action<Logger> write)
    {
        try
        {
            write(_logger);
        }
        catch (Exception)
        {
            // Diagnostics must not replace a healthy launcher operation or obscure the original
            // process failure because a sink or diagnostic projection itself failed.
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Diagnostic disposal must not override the launcher's intended process exit result.")]
    private void TryDisposeLogger()
    {
        try
        {
            _logger.Dispose();
        }
        catch (Exception)
        {
            // Best effort shutdown only.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
