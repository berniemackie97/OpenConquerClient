using OpenConquer.Launcher.Diagnostics;

namespace OpenConquer.Launcher.Tests;

public sealed class LauncherHostExceptionObserverTests
{
    [Fact]
    public void StartMayBeCalledOnlyOnce()
    {
        string logDirectory = CreateTemporaryLogDirectoryPath();

        try
        {
            using LauncherDiagnostics diagnostics = LauncherDiagnostics.Create(logDirectory);

            using LauncherHostExceptionObserver observer = new(diagnostics);

            observer.Start();

            Assert.Throws<InvalidOperationException>(observer.Start);
        }
        finally
        {
            DeleteDirectoryIfPresent(logDirectory);
        }
    }

    [Fact]
    public void StartAfterDisposeThrowsObjectDisposedException()
    {
        string logDirectory = CreateTemporaryLogDirectoryPath();

        try
        {
            using LauncherDiagnostics diagnostics = LauncherDiagnostics.Create(logDirectory);

            LauncherHostExceptionObserver observer = new(diagnostics);

            observer.Dispose();

            Assert.Throws<ObjectDisposedException>(observer.Start);
        }
        finally
        {
            DeleteDirectoryIfPresent(logDirectory);
        }
    }

    [Fact]
    public void MatchingUiDispatcherExceptionIsClassifiedOnce()
    {
        string logDirectory = CreateTemporaryLogDirectoryPath();

        try
        {
            using LauncherDiagnostics diagnostics = LauncherDiagnostics.Create(logDirectory);

            using LauncherHostExceptionObserver observer = new(diagnostics);

            Exception exception = new InvalidOperationException(
                "This message must not be logged by the observer callback."
            );

            observer.ObserveUiDispatcherException(exception);

            Assert.Equal(
                LauncherExceptionDomain.UiDispatcher,
                observer.ClassifyTopLevelException(exception)
            );

            Assert.Equal(
                LauncherExceptionDomain.TopLevel,
                observer.ClassifyTopLevelException(exception)
            );
        }
        finally
        {
            DeleteDirectoryIfPresent(logDirectory);
        }
    }

    [Fact]
    public void DifferentTopLevelExceptionConsumesPendingUiClassification()
    {
        string logDirectory = CreateTemporaryLogDirectoryPath();

        try
        {
            using LauncherDiagnostics diagnostics = LauncherDiagnostics.Create(logDirectory);

            using LauncherHostExceptionObserver observer = new(diagnostics);

            Exception dispatcherException = new InvalidOperationException();

            Exception topLevelException = new ArgumentException();

            observer.ObserveUiDispatcherException(dispatcherException);

            Assert.Equal(
                LauncherExceptionDomain.TopLevel,
                observer.ClassifyTopLevelException(topLevelException)
            );

            Assert.Equal(
                LauncherExceptionDomain.TopLevel,
                observer.ClassifyTopLevelException(dispatcherException)
            );
        }
        finally
        {
            DeleteDirectoryIfPresent(logDirectory);
        }
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
