using System.Diagnostics;

namespace OpenConquer.Launcher.Diagnostics;

/// <summary>
/// Projects exceptions into the bounded diagnostic representation safe for launcher host logging.
/// </summary>
internal static class LauncherExceptionDiagnosticProjector
{
    private const int MaximumDepth = 8;
    private const int MaximumExceptionCount = 16;

    public static LauncherExceptionDiagnostic Project(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        int remainingExceptionCount = MaximumExceptionCount;

        return Project(exception, depth: 0, ref remainingExceptionCount);
    }

    private static LauncherExceptionDiagnostic Project(Exception exception, int depth, ref int remainingExceptionCount)
    {
        remainingExceptionCount--;

        int innerExceptionCount = GetInnerExceptionCount(exception);
        List<LauncherExceptionDiagnostic> projectedInnerExceptions = [];
        bool innerExceptionsTruncated = false;

        if (depth >= MaximumDepth)
        {
            innerExceptionsTruncated = innerExceptionCount > 0;
        }
        else
        {
            for (int index = 0; index < innerExceptionCount; index++)
            {
                if (remainingExceptionCount == 0)
                {
                    innerExceptionsTruncated = true;
                    break;
                }

                projectedInnerExceptions.Add(Project(GetInnerException(exception, index), depth + 1, ref remainingExceptionCount));
            }
        }

        string exceptionType = exception.GetType().FullName ?? exception.GetType().Name;

        string stackTrace = new StackTrace(exception, fNeedFileInfo: false).ToString();

        return new LauncherExceptionDiagnostic(ExceptionType: exceptionType, HResult: exception.HResult,
            StackTrace: string.IsNullOrWhiteSpace(stackTrace) ? null : stackTrace,
            InnerExceptions: projectedInnerExceptions.ToArray(), InnerExceptionsTruncated: innerExceptionsTruncated);
    }

    private static int GetInnerExceptionCount(Exception exception)
    {
        return exception switch
        {
            AggregateException aggregateException => aggregateException.InnerExceptions.Count,

            { InnerException: not null } => 1,

            _ => 0,
        };
    }

    private static Exception GetInnerException(Exception exception, int index)
    {
        if (exception is AggregateException aggregateException)
        {
            return aggregateException.InnerExceptions[index];
        }

        if (index == 0 && exception.InnerException is { } innerException)
        {
            return innerException;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }
}
