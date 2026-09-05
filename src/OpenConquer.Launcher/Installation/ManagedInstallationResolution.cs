namespace OpenConquer.Launcher.Installation;

/// <summary>Result of resolving the install context supplied by the launcher package.</summary>
internal abstract record ManagedInstallationResolution
{
    private ManagedInstallationResolution()
    {
    }

    internal sealed record Resolved(ManagedInstallation Installation) : ManagedInstallationResolution;

    internal sealed record Rejected(ManagedInstallationIssue Issue) : ManagedInstallationResolution;
}
