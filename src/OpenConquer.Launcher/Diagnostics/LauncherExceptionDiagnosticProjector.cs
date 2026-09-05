using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace OpenConquer.Launcher.Diagnostics;

/// <summary>
/// Projects exceptions into the bounded diagnostic representation safe for launcher host logging.
/// </summary>
internal static class LauncherExceptionDiagnosticProjector
{
    private const int MaximumDepth = 8;
    private const int MaximumExceptionCount = 16;
    private const int MaximumExceptionTypeLength = 512;
    private const int MaximumStackFrameCount = 32;
    private const int MaximumStackTraceLength = 4096;

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
        List<LauncherExceptionDiagnostic>? projectedInnerExceptions = null;
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

                projectedInnerExceptions ??= new List<LauncherExceptionDiagnostic>();
                projectedInnerExceptions.Add(Project(GetInnerException(exception, index), depth + 1, ref remainingExceptionCount));
            }
        }

        string exceptionType = exception.GetType().FullName ?? exception.GetType().Name;
        bool exceptionTypeTruncated = exceptionType.Length > MaximumExceptionTypeLength;
        if (exceptionTypeTruncated)
        {
            exceptionType = exceptionType[..GetPrefixLength(exceptionType, MaximumExceptionTypeLength)];
        }

        string? stackTrace = ProjectStackTrace(exception, out bool stackTraceTruncated);
        IReadOnlyList<LauncherExceptionDiagnostic> children = projectedInnerExceptions is null
            ? []
            : projectedInnerExceptions.AsReadOnly();

        return new LauncherExceptionDiagnostic(
            ExceptionType: exceptionType,
            ExceptionTypeTruncated: exceptionTypeTruncated,
            HResult: exception.HResult,
            StackTrace: stackTrace,
            StackTraceTruncated: stackTraceTruncated,
            InnerExceptions: children,
            InnerExceptionsTruncated: innerExceptionsTruncated);
    }

    private static string? ProjectStackTrace(Exception exception, out bool truncated)
    {
        // The runtime still materializes the captured stack. Do not additionally format the entire
        // trace or consult virtual exception properties: they may contain arbitrary application data.
        StackTrace trace = new(exception, fNeedFileInfo: false);
        int frameCount = Math.Min(trace.FrameCount, MaximumStackFrameCount);
        truncated = trace.FrameCount > frameCount;
        if (frameCount == 0)
        {
            return null;
        }

        StringBuilder builder = new();
        for (int index = 0; index < frameCount; index++)
        {
            if (index > 0 && !AppendStackText(builder, "\n"))
            {
                truncated = true;
                break;
            }

            MethodBase? method = trace.GetFrame(index)?.GetMethod();
            string? declaringType = method?.DeclaringType?.FullName;
            if ((declaringType is not null &&
                    (!AppendStackText(builder, declaringType) || !AppendStackText(builder, "."))) ||
                !AppendStackText(builder, method?.Name ?? "<unknown>"))
            {
                truncated = true;
                break;
            }
        }

        return builder.ToString();
    }

    private static bool AppendStackText(StringBuilder builder, string text)
    {
        int length = GetPrefixLength(text, MaximumStackTraceLength - builder.Length);
        builder.Append(text.AsSpan(0, length));
        return length == text.Length;
    }

    private static int GetPrefixLength(string text, int maximumLength)
    {
        int length = Math.Min(text.Length, maximumLength);
        if (length > 0 && length < text.Length && char.IsHighSurrogate(text[length - 1]))
        {
            length--;
        }

        return length;
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
