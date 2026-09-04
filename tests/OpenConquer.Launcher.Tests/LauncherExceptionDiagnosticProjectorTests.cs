using System.Text.Json;
using OpenConquer.Launcher.Diagnostics;

namespace OpenConquer.Launcher.Tests;

public sealed class LauncherExceptionDiagnosticProjectorTests
{
    [Fact]
    public void ProjectPreservesUsefulDiagnosticIdentityAndStack()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            ThrowDiagnosticException
        );

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(
            exception
        );

        Assert.Equal(typeof(InvalidOperationException).FullName, diagnostic.ExceptionType);

        Assert.Equal(exception.HResult, diagnostic.HResult);

        Assert.NotNull(diagnostic.StackTrace);

        Assert.Contains(
            nameof(ThrowDiagnosticException),
            diagnostic.StackTrace,
            StringComparison.Ordinal
        );

        Assert.Empty(diagnostic.InnerExceptions);

        Assert.False(diagnostic.InnerExceptionsTruncated);
    }

    [Fact]
    public void ProjectExcludesMessageDataAndSourceFileInformation()
    {
        const string secretMessage = "secret-message-value";
        const string secretData = "secret-data-value";

        Exception exception;

        try
        {
            throw new InvalidOperationException(secretMessage)
            {
                Data = { ["Token"] = secretData },
            };
        }
        catch (Exception capturedException)
        {
            exception = capturedException;
        }

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(
            exception
        );

        string serializedDiagnostic = JsonSerializer.Serialize(diagnostic);

        Assert.DoesNotContain(secretMessage, serializedDiagnostic, StringComparison.Ordinal);

        Assert.DoesNotContain(secretData, serializedDiagnostic, StringComparison.Ordinal);

        Assert.DoesNotContain(
            nameof(LauncherExceptionDiagnosticProjectorTests) + ".cs",
            serializedDiagnostic,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void ProjectPreservesNestedExceptionStructureWithoutMessages()
    {
        const string outerSecret = "outer-secret";
        const string innerSecret = "inner-secret";

        Exception exception = new InvalidOperationException(
            outerSecret,
            new ArgumentException(innerSecret)
        );

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(
            exception
        );

        LauncherExceptionDiagnostic innerDiagnostic = Assert.Single(diagnostic.InnerExceptions);

        Assert.Equal(typeof(ArgumentException).FullName, innerDiagnostic.ExceptionType);

        string serializedDiagnostic = JsonSerializer.Serialize(diagnostic);

        Assert.DoesNotContain(outerSecret, serializedDiagnostic, StringComparison.Ordinal);

        Assert.DoesNotContain(innerSecret, serializedDiagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectBoundsAggregateExceptionTraversal()
    {
        Exception[] innerExceptions = Enumerable
            .Range(0, 32)
            .Select(static index => new InvalidOperationException($"message-{index}"))
            .ToArray();

        AggregateException exception = new(innerExceptions);

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(
            exception
        );

        Assert.Equal(15, diagnostic.InnerExceptions.Count);

        Assert.True(diagnostic.InnerExceptionsTruncated);
    }

    private static void ThrowDiagnosticException()
    {
        throw new InvalidOperationException("This message must never enter launcher diagnostics.");
    }
}
