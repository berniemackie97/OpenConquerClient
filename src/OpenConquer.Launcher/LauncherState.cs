using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher;

/// <summary>Application-owned launcher lifecycle state.</summary>
internal abstract record LauncherState
{
    private LauncherState()
    {
    }

    internal sealed record Starting : LauncherState;

    internal sealed record EvaluatingInstallation : LauncherState;

    internal sealed record InstallationResolved(ManagedInstallation Installation) : LauncherState;

    internal sealed record InstallationUnavailable(ManagedInstallationIssue Issue) : LauncherState;

    internal sealed record Faulted : LauncherState;

    internal sealed record Stopping : LauncherState;

    internal sealed record Stopped : LauncherState;
}
