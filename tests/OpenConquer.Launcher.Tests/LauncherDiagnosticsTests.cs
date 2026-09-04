using System.Text.Json;
using OpenConquer.Launcher.Diagnostics;

namespace OpenConquer.Launcher.Tests;

public sealed class LauncherDiagnosticsTests
{
    [Fact]
    public void CreateCreatesDirectoryAndWritesStructuredHostLifecycleEvents()
    {
        string logDirectory = CreateTemporaryLogDirectoryPath();

        try
        {
            using (LauncherDiagnostics diagnostics = LauncherDiagnostics.Create(logDirectory))
            {
                diagnostics.RecordHostStarted();
                diagnostics.RecordHostStopped(exitCode: 17);
            }

            string logFilePath = GetSingleLogFilePath(logDirectory);

            string[] logLines = File.ReadAllLines(logFilePath);

            Assert.Equal(2, logLines.Length);

            using JsonDocument startedEvent = JsonDocument.Parse(logLines[0]);

            using JsonDocument stoppedEvent = JsonDocument.Parse(logLines[1]);

            Assert.Equal(
                "Launcher host started.",
                startedEvent.RootElement.GetProperty("RenderedMessage").GetString()
            );

            Assert.Equal(
                "OpenConquer.Launcher",
                startedEvent
                    .RootElement.GetProperty("Properties")
                    .GetProperty("Application")
                    .GetString()
            );

            Assert.Equal(
                Environment.ProcessId,
                startedEvent
                    .RootElement.GetProperty("Properties")
                    .GetProperty("ProcessId")
                    .GetInt32()
            );

            Assert.Equal(
                "Launcher host stopped with exit code 17.",
                stoppedEvent.RootElement.GetProperty("RenderedMessage").GetString()
            );

            Assert.Equal(
                17,
                stoppedEvent
                    .RootElement.GetProperty("Properties")
                    .GetProperty("ExitCode")
                    .GetInt32()
            );
        }
        finally
        {
            DeleteDirectoryIfPresent(logDirectory);
        }
    }

    [Fact]
    public void RecordExceptionWritesOnlyRedactedDiagnosticProjection()
    {
        const string secretMessage = "account-secret-message";
        const string secretData = "account-secret-data";

        string logDirectory = CreateTemporaryLogDirectoryPath();

        try
        {
            Exception exception = new InvalidOperationException(secretMessage)
            {
                Data = { ["AccessToken"] = secretData },
            };

            using (LauncherDiagnostics diagnostics = LauncherDiagnostics.Create(logDirectory))
            {
                diagnostics.RecordException(
                    LauncherExceptionDomain.TopLevel,
                    isTerminating: true,
                    exception
                );
            }

            string logFilePath = GetSingleLogFilePath(logDirectory);

            string logContent = File.ReadAllText(logFilePath);

            Assert.DoesNotContain(secretMessage, logContent, StringComparison.Ordinal);

            Assert.DoesNotContain(secretData, logContent, StringComparison.Ordinal);

            using JsonDocument logEvent = JsonDocument.Parse(logContent);

            JsonElement properties = logEvent.RootElement.GetProperty("Properties");

            Assert.Equal(
                nameof(LauncherExceptionDomain.TopLevel),
                properties.GetProperty("ExceptionDomain").GetString()
            );

            Assert.True(properties.GetProperty("IsTerminating").GetBoolean());

            JsonElement diagnostic = properties.GetProperty("ExceptionDiagnostic");

            Assert.Equal(
                typeof(InvalidOperationException).FullName,
                diagnostic.GetProperty("ExceptionType").GetString()
            );

            Assert.Equal(exception.HResult, diagnostic.GetProperty("HResult").GetInt32());
        }
        finally
        {
            DeleteDirectoryIfPresent(logDirectory);
        }
    }

    [Fact]
    public void CreateFallsBackWhenPersistentLogDirectoryCannotBeCreated()
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenConquer.Launcher.Tests",
            Guid.NewGuid().ToString("N")
        );

        string filePath = Path.Combine(temporaryRoot, "not-a-directory");

        Directory.CreateDirectory(temporaryRoot);

        File.WriteAllText(
            filePath,
            "This file intentionally prevents creation of a directory at the same path."
        );

        try
        {
            using LauncherDiagnostics diagnostics = LauncherDiagnostics.Create(filePath);

            diagnostics.RecordHostStarted();
            diagnostics.RecordHostStopped(exitCode: 0);
            diagnostics.RecordException(
                LauncherExceptionDomain.TopLevel,
                isTerminating: true,
                new InvalidOperationException("Fallback diagnostics must remain non-disruptive.")
            );
        }
        finally
        {
            DeleteDirectoryIfPresent(temporaryRoot);
        }
    }

    [Fact]
    public void CreateRejectsRelativeLogDirectory()
    {
        Assert.Throws<ArgumentException>(() => LauncherDiagnostics.Create("relative/logs"));
    }

    [Fact]
    public void RecordingAfterDisposeThrowsObjectDisposedException()
    {
        string logDirectory = CreateTemporaryLogDirectoryPath();

        try
        {
            LauncherDiagnostics diagnostics = LauncherDiagnostics.Create(logDirectory);

            diagnostics.Dispose();

            Assert.Throws<ObjectDisposedException>(diagnostics.RecordHostStarted);
        }
        finally
        {
            DeleteDirectoryIfPresent(logDirectory);
        }
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        string logDirectory = CreateTemporaryLogDirectoryPath();

        try
        {
            LauncherDiagnostics diagnostics = LauncherDiagnostics.Create(logDirectory);

            diagnostics.Dispose();
            diagnostics.Dispose();
        }
        finally
        {
            DeleteDirectoryIfPresent(logDirectory);
        }
    }

    private static string GetSingleLogFilePath(string logDirectory)
    {
        Assert.True(Directory.Exists(logDirectory));

        return Assert.Single(
            Directory.GetFiles(logDirectory, "launcher-*.jsonl", SearchOption.TopDirectoryOnly)
        );
    }

    private static string CreateTemporaryLogDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "OpenConquer.Launcher.Tests",
            Guid.NewGuid().ToString("N"),
            "Logs"
        );
    }

    private static void DeleteDirectoryIfPresent(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
