namespace OpenConquer.Launcher.Installation;

/// <summary>Presentation copy for application states; paths and exception text never become status copy.</summary>
internal static class InstallationStatusText
{
    public static (string Title, string Detail) For(InstallationState state)
    {
        return state switch
        {
            InstallationState.Unselected => ("Choose your game folder", "Select an unpacked OpenConquer game installation to check its identity and folder layout."),
            InstallationState.InvalidPath => ("Enter a full folder path", "Use Browse or enter an absolute path to the game installation folder."),
            InstallationState.Checking => ("Checking game files", "Reading the game identity and startup metadata. Your files will not be changed."),
            InstallationState.Located located => ("Game files located", $"OpenConquer.Client {located.AssemblyVersion} was identified. Integrity verification and sign-in are still required before Play."),
            InstallationState.Cancelled => ("Check cancelled", "Your files were not changed. You can check this folder again or choose another."),
            InstallationState.Faulted => ("The launcher encountered an unexpected failure", "Close and restart the launcher."),
            InstallationState.Rejected rejected => For(rejected.Issue),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
    }

    private static (string Title, string Detail) For(InstallationIssue issue)
    {
        return issue switch
        {
            InstallationIssue.MissingFiles => ("Game files are missing", "Choose the unpacked game installation folder. One or more expected files or folders could not be found."),
            InstallationIssue.AccessDenied => ("This folder could not be read", "Choose a folder your account can read, or check its permissions and try again."),
            InstallationIssue.InvalidLayout => ("This game folder could not be recognized", "Choose a complete, unpacked OpenConquer game installation. Its startup files may be incomplete or damaged."),
            InstallationIssue.UnsupportedLayout => ("This installation format is not supported", "Choose an unpacked OpenConquer installation compatible with this launcher."),
            InstallationIssue.LinkedPath => ("This folder contains linked installation paths", "Choose the actual installation folder with its game files stored directly inside it."),
            InstallationIssue.ReadFailure => ("The game files could not be read", "Check that the drive is available and no update is changing these files, then try again."),
            _ => throw new ArgumentOutOfRangeException(nameof(issue)),
        };
    }
}
