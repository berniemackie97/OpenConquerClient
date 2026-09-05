namespace OpenConquer.Launcher.Installation;

/// <summary>Non-sensitive, actionable outcomes of inspecting local game files.</summary>
internal enum InstallationIssue
{
    MissingFiles,
    AccessDenied,
    InvalidLayout,
    UnsupportedLayout,
    LinkedPath,
    ReadFailure,
}
