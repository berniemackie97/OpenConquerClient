using System.Diagnostics;
using System.Globalization;
using OpenConquer.Launcher.Settings;

namespace OpenConquer.Launcher.Installation;

/// <summary>Starts the verified client executable of a resolved installation with the player's display preferences.</summary>
internal static class ClientProcessLauncher
{
    public static ProcessStartInfo CreateStartInfo(ManagedInstallation installation, DisplayPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(preferences);

        ProcessStartInfo startInfo = new(installation.ClientExecutablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = installation.ClientRootPath,
        };
        startInfo.ArgumentList.Add("--window-size");
        startInfo.ArgumentList.Add(string.Create(CultureInfo.InvariantCulture, $"{preferences.Width}x{preferences.Height}"));
        startInfo.ArgumentList.Add("--window-mode");
        startInfo.ArgumentList.Add(preferences.WindowMode switch
        {
            GameWindowMode.Resizable => "resizable",
            GameWindowMode.Fixed => "fixed",
            GameWindowMode.Fullscreen => "fullscreen",
            _ => throw new ArgumentOutOfRangeException(nameof(preferences), preferences.WindowMode, "Unknown window mode."),
        });
        startInfo.ArgumentList.Add("--presentation");
        startInfo.ArgumentList.Add(preferences.Presentation switch
        {
            GamePresentation.Fit => "fit",
            GamePresentation.Integer => "integer",
            GamePresentation.Stretch => "stretch",
            _ => throw new ArgumentOutOfRangeException(nameof(preferences), preferences.Presentation, "Unknown presentation."),
        });
        return startInfo;
    }

    /// <summary>Starts the client detached from the launcher; the launcher closes afterwards, as play.exe did.</summary>
    public static void Start(ManagedInstallation installation, DisplayPreferences preferences)
    {
        using Process process = Process.Start(CreateStartInfo(installation, preferences))
            ?? throw new InvalidOperationException("The game client did not start.");
    }
}
