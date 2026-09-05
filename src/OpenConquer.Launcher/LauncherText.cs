using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher;

/// <summary>Centralized launcher presentation text pending the product localization system.</summary>
internal static class LauncherText
{
    public const string ProductName = "OpenConquer";
    public const string WindowTitle = "OpenConquer Launcher";
    public const string InstallationHeading = "OpenConquer installation";

    public static (string Title, string Detail) For(LauncherState state) => state switch
    {
        LauncherState.Starting => (
            "Starting OpenConquer Launcher",
            "Preparing the managed OpenConquer installation check."
        ),
        LauncherState.EvaluatingInstallation => (
            "Checking OpenConquer installation",
            "The launcher is evaluating the installation supplied by its installed package."
        ),
        LauncherState.InstallationResolved => (
            "OpenConquer installation detected",
            "The managed product boundary is present. Release validation and Play are the next launcher responsibilities."
        ),
        LauncherState.InstallationUnavailable unavailable => (
            unavailable.Issue switch
            {
                ManagedInstallationIssue.ManifestMissing => "OpenConquer is not installed",
                ManagedInstallationIssue.ManifestInvalid => "Installation unavailable",
                ManagedInstallationIssue.UnsupportedManifest => "Installation requires an update",
                ManagedInstallationIssue.ClientComponentMissing => "OpenConquer is not installed",
                ManagedInstallationIssue.AccessDenied => "Installation unavailable",
                ManagedInstallationIssue.LinkedPath => "Installation unavailable",
                ManagedInstallationIssue.ReadFailure => "Installation unavailable",
                _ => "Installation unavailable",
            },
            unavailable.Issue switch
            {
                ManagedInstallationIssue.ManifestMissing => "The launcher package does not contain its installation descriptor. Start it from a complete OpenConquer installation.",
                ManagedInstallationIssue.ManifestInvalid => "The installed product layout descriptor is invalid. The launcher cannot continue with this installation.",
                ManagedInstallationIssue.UnsupportedManifest => "This installation was created by a newer launcher and must be updated.",
                ManagedInstallationIssue.ClientComponentMissing => "The launcher package does not contain the game client. Start it from a complete OpenConquer installation.",
                ManagedInstallationIssue.AccessDenied => "The launcher cannot read its managed installation.",
                ManagedInstallationIssue.LinkedPath => "The managed installation uses an unsupported linked component.",
                ManagedInstallationIssue.ReadFailure => "The managed installation could not be read.",
                _ => "The managed installation could not be evaluated.",
            }
        ),
        LauncherState.Faulted => (
            "Launcher startup failed",
            "The launcher could not complete its managed installation check."
        ),
        LauncherState.Stopping => (
            "Closing OpenConquer Launcher",
            "Stopping launcher operations safely."
        ),
        LauncherState.Stopped => (
            string.Empty,
            string.Empty
        ),
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
