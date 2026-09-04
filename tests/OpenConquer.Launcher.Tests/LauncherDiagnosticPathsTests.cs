using OpenConquer.Launcher.Diagnostics;

namespace OpenConquer.Launcher.Tests;

public sealed class LauncherDiagnosticPathsTests
{
    [Fact]
    public void WindowsLogDirectoryUsesLocalApplicationData()
    {
        string localApplicationDataPath = CreateFullyQualifiedTestPath();

        bool succeeded = LauncherDiagnosticPaths.TryGetWindowsLogDirectory(
            localApplicationDataPath,
            out string? logDirectory
        );

        Assert.True(succeeded);

        Assert.Equal(
            Path.Combine(localApplicationDataPath, "OpenConquer", "Launcher", "Logs"),
            logDirectory
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/path")]
    public void WindowsLogDirectoryRejectsInvalidBasePath(string? localApplicationDataPath)
    {
        bool succeeded = LauncherDiagnosticPaths.TryGetWindowsLogDirectory(
            localApplicationDataPath,
            out string? logDirectory
        );

        Assert.False(succeeded);

        Assert.Null(logDirectory);
    }

    [Fact]
    public void MacOsLogDirectoryUsesUserLibraryLogs()
    {
        string userProfilePath = CreateFullyQualifiedTestPath();

        bool succeeded = LauncherDiagnosticPaths.TryGetMacOsLogDirectory(
            userProfilePath,
            out string? logDirectory
        );

        Assert.True(succeeded);

        Assert.Equal(
            Path.Combine(userProfilePath, "Library", "Logs", "OpenConquer", "Launcher"),
            logDirectory
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/path")]
    public void MacOsLogDirectoryRejectsInvalidUserProfilePath(string? userProfilePath)
    {
        bool succeeded = LauncherDiagnosticPaths.TryGetMacOsLogDirectory(
            userProfilePath,
            out string? logDirectory
        );

        Assert.False(succeeded);

        Assert.Null(logDirectory);
    }

    [Fact]
    public void LinuxLogDirectoryUsesAbsoluteXdgStateHome()
    {
        string xdgStateHome = CreateFullyQualifiedTestPath();

        string userProfilePath = CreateFullyQualifiedTestPath();

        bool succeeded = LauncherDiagnosticPaths.TryGetLinuxLogDirectory(
            xdgStateHome,
            userProfilePath,
            out string? logDirectory
        );

        Assert.True(succeeded);

        Assert.Equal(Path.Combine(xdgStateHome, "OpenConquer", "Launcher", "Logs"), logDirectory);
    }

    [Fact]
    public void LinuxLogDirectoryIgnoresRelativeXdgStateHome()
    {
        string userProfilePath = CreateFullyQualifiedTestPath();

        bool succeeded = LauncherDiagnosticPaths.TryGetLinuxLogDirectory(
            "relative/state",
            userProfilePath,
            out string? logDirectory
        );

        Assert.True(succeeded);

        Assert.Equal(
            Path.Combine(userProfilePath, ".local", "state", "OpenConquer", "Launcher", "Logs"),
            logDirectory
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LinuxLogDirectoryFallsBackToDefaultStateHome(string? xdgStateHome)
    {
        string userProfilePath = CreateFullyQualifiedTestPath();

        bool succeeded = LauncherDiagnosticPaths.TryGetLinuxLogDirectory(
            xdgStateHome,
            userProfilePath,
            out string? logDirectory
        );

        Assert.True(succeeded);

        Assert.Equal(
            Path.Combine(userProfilePath, ".local", "state", "OpenConquer", "Launcher", "Logs"),
            logDirectory
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/home")]
    public void LinuxLogDirectoryReturnsFalseWhenNoUsableStateLocationExists(
        string? userProfilePath
    )
    {
        bool succeeded = LauncherDiagnosticPaths.TryGetLinuxLogDirectory(
            xdgStateHome: null,
            userProfilePath,
            out string? logDirectory
        );

        Assert.False(succeeded);

        Assert.Null(logDirectory);
    }

    [Fact]
    public void CurrentPlatformResolvesFullyQualifiedLogDirectory()
    {
        bool succeeded = LauncherDiagnosticPaths.TryGetLogDirectory(out string? logDirectory);

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            Assert.True(succeeded);

            Assert.NotNull(logDirectory);

            Assert.True(Path.IsPathFullyQualified(logDirectory));

            return;
        }

        Assert.False(succeeded);

        Assert.Null(logDirectory);
    }

    private static string CreateFullyQualifiedTestPath()
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetTempPath(),
                "OpenConquer.Launcher.Tests",
                Guid.NewGuid().ToString("N")
            )
        );
    }
}
