namespace OpenConquer.Launcher.Diagnostics;

/// <summary>
/// Represents the deliberately limited, non-secret-bearing diagnostic view of an exception.
/// </summary>
internal sealed record LauncherExceptionDiagnostic(string ExceptionType, int HResult, string? StackTrace, IReadOnlyList<LauncherExceptionDiagnostic> InnerExceptions, bool InnerExceptionsTruncated);
