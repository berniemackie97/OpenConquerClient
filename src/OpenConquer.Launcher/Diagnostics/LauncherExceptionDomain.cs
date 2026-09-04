namespace OpenConquer.Launcher.Diagnostics;

/// <summary>
/// Identifies the launcher host boundary at which an exception was observed.
/// </summary>
internal enum LauncherExceptionDomain
{
    UiDispatcher,
    AppDomain,
    UnobservedTask,
    TopLevel,
}
