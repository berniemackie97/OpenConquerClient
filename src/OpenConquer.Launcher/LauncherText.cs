using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher;

/// <summary>Player-facing text for the launcher lifecycle and installation check.</summary>
internal static class LauncherText
{
    public const string ProductName = "OpenConquer";
    public const string WindowTitle = "OpenConquer Launcher";
    public const string InstallationHeading = "Game status";
    public const string CheckAgain = "Check again";
    public const string Close = "Close";
    public const string AlreadyRunningTitle = "OpenConquer is already running";
    public const string AlreadyRunningDetail = "The existing launcher isn't responding. Close it and open OpenConquer again. It may be running in another desktop session.";

    public static (string Title, string Detail) For(LauncherState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state switch
        {
            LauncherState.Starting => ("Starting OpenConquer", "Preparing the launcher."),
            LauncherState.EvaluatingInstallation => ("Preparing OpenConquer", "Checking your game installation."),
            LauncherState.InstallationResolved resolved => ("OpenConquer is ready", $"Release {resolved.Installation.Release.Version} is verified for this device."),
            LauncherState.InstallationUnavailable unavailable => ForUnavailableInstallation(unavailable.Issue),
            LauncherState.Faulted => ("OpenConquer Launcher couldn't start", "Close the launcher and try again."),
            LauncherState.Stopping => ("Closing OpenConquer", "Finishing up."),
            LauncherState.Stopped => (string.Empty, string.Empty),

            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported launcher state."),
        };
    }

    private static (string Title, string Detail) ForUnavailableInstallation(ManagedInstallationIssue issue)
    {
        return issue switch
        {
            ManagedInstallationIssue.ManifestMissing => ("Installation incomplete", "Required OpenConquer installation information is missing."),
            ManagedInstallationIssue.ManifestInvalid => ("Installation needs repair", "OpenConquer installation information is damaged or invalid."),
            ManagedInstallationIssue.UnsupportedManifest => ("Launcher update required", "This installation requires a newer version of OpenConquer Launcher."),
            ManagedInstallationIssue.ActiveReleaseMissing => ("Installation needs repair", "The selected OpenConquer release is missing."),
            ManagedInstallationIssue.ReleaseIdentityMismatch => ("Release not trusted", "The selected release does not match its authenticated release information."),
            ManagedInstallationIssue.ClientComponentMissing => ("Installation needs repair", "Required OpenConquer game files are missing."),
            ManagedInstallationIssue.ReleaseMetadataMissing => ("Installation incomplete", "Trusted OpenConquer release information is missing."),
            ManagedInstallationIssue.ReleaseManifestInvalid => ("Installation needs repair", "OpenConquer release information is damaged or invalid."),
            ManagedInstallationIssue.UnsupportedReleaseManifest => ("Launcher update required", "This release uses information that this launcher cannot read."),
            ManagedInstallationIssue.ReleaseAuthorityUnavailable => ("Launcher setup incomplete", "This launcher does not contain an OpenConquer release trust authority."),
            ManagedInstallationIssue.ReleaseSignatureInvalid => ("Release not trusted", "The installed game release could not be authenticated."),
            ManagedInstallationIssue.LauncherUpdateRequired => ("Launcher update required", "This game release requires a newer OpenConquer Launcher."),
            ManagedInstallationIssue.ClientPlatformMismatch => ("Wrong game package", "The installed game files are for a different operating system or processor."),
            ManagedInstallationIssue.ClientIntegrityFailure => ("Installation needs repair", "OpenConquer game files are missing, changed, or unexpected."),
            ManagedInstallationIssue.AccessDenied => ("Can't access OpenConquer", "The launcher doesn't have permission to read the game installation."),
            ManagedInstallationIssue.LinkedPath => ("Installation needs repair", "OpenConquer is installed in an unsupported layout."),
            ManagedInstallationIssue.ReadFailure => ("Can't check OpenConquer", "The launcher couldn't read the game installation."),

            _ => throw new ArgumentOutOfRangeException(nameof(issue), issue, "Unsupported managed installation issue."),
        };
    }
}
