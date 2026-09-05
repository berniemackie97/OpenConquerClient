using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Launcher.Diagnostics;

/// <summary>
/// Defines the per user filesystem locations owned by launcher diagnostics.
/// </summary>
internal static class LauncherDiagnosticPaths
{
    private const string ProductDirectoryName = "OpenConquer";
    private const string LauncherDirectoryName = "Launcher";
    private const string LogsDirectoryName = "Logs";

    /// <summary>
    /// Attempts to resolve the directory in which launcher diagnostic logs are stored for the current user.
    /// </summary>
    public static bool TryGetLogDirectory([NotNullWhen(true)] out string? logDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            string localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);

            return TryGetWindowsLogDirectory(localApplicationDataPath, out logDirectory);
        }

        if (OperatingSystem.IsMacOS())
        {
            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);

            return TryGetMacOsLogDirectory(userProfilePath, out logDirectory);
        }

        if (OperatingSystem.IsLinux())
        {
            string? xdgStateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");

            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);

            return TryGetLinuxLogDirectory(xdgStateHome, userProfilePath, out logDirectory);
        }

        logDirectory = null;

        return false;
    }

    internal static bool TryGetWindowsLogDirectory(string? localApplicationDataPath, [NotNullWhen(true)] out string? logDirectory)
    {
        if (!IsUsableBasePath(localApplicationDataPath))
        {
            logDirectory = null;

            return false;
        }

        logDirectory = Path.Combine(localApplicationDataPath, ProductDirectoryName, LauncherDirectoryName, LogsDirectoryName);

        return true;
    }

    internal static bool TryGetMacOsLogDirectory(string? userProfilePath, [NotNullWhen(true)] out string? logDirectory)
    {
        if (!IsUsableBasePath(userProfilePath))
        {
            logDirectory = null;

            return false;
        }

        logDirectory = Path.Combine(userProfilePath, "Library", LogsDirectoryName, ProductDirectoryName, LauncherDirectoryName);

        return true;
    }

    internal static bool TryGetLinuxLogDirectory(string? xdgStateHome, string? userProfilePath, [NotNullWhen(true)] out string? logDirectory)
    {
        string? stateHome = IsUsableBasePath(xdgStateHome) ? xdgStateHome : GetDefaultLinuxStateHome(userProfilePath);

        if (stateHome is null)
        {
            logDirectory = null;

            return false;
        }

        logDirectory = Path.Combine(stateHome, ProductDirectoryName, LauncherDirectoryName, LogsDirectoryName);

        return true;
    }

    private static string? GetDefaultLinuxStateHome(string? userProfilePath)
    {
        if (!IsUsableBasePath(userProfilePath))
        {
            return null;
        }

        return Path.Combine(userProfilePath, ".local", "state");
    }

    private static bool IsUsableBasePath([NotNullWhen(true)] string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && !path.AsSpan().ContainsAny(Path.GetInvalidPathChars());
    }
}
