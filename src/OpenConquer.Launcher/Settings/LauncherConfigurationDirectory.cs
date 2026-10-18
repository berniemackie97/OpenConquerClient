namespace OpenConquer.Launcher.Settings;

/// <summary>The per-user directory that holds the launcher's preference files.</summary>
internal static class LauncherConfigurationDirectory
{
    /// <summary>
    /// <c>OpenConquer/Launcher</c> under LocalApplicationData (Windows), <c>~/Library/Application Support</c> (macOS)
    /// or the XDG configuration home (Linux); null when the platform offers no absolute location.
    /// </summary>
    public static string? ForCurrentUser()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? root = OperatingSystem.IsWindows() ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : OperatingSystem.IsMacOS()
                ? Path.Combine(home, "Library", "Application Support") : OperatingSystem.IsLinux()
                    ? LinuxConfigurationHome(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"), home) : null;

        return IsAbsolute(root) ? Path.Combine(root!, "OpenConquer", "Launcher") : null;
    }

    internal static string? LinuxConfigurationHome(string? configured, string? home)
    {
        return IsAbsolute(configured) ? configured : IsAbsolute(home) ? Path.Combine(home!, ".config") : null;
    }

    private static bool IsAbsolute(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && !path.Contains('\0');
    }
}
