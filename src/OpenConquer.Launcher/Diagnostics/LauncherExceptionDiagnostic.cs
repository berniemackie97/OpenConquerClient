namespace OpenConquer.Launcher.Diagnostics;

internal sealed record LauncherExceptionDiagnostic(string ExceptionType, bool ExceptionTypeTruncated, int HResult, string? StackTrace, bool StackTraceTruncated, IReadOnlyList<LauncherExceptionDiagnostic> InnerExceptions, bool InnerExceptionsTruncated);
