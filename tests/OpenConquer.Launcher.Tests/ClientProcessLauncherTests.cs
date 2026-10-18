using System.Diagnostics;
using OpenConquer.Launcher.Installation;
using OpenConquer.Launcher.Settings;

namespace OpenConquer.Launcher.Tests;

public sealed class ClientProcessLauncherTests
{
    [Fact]
    public void StartInfo_RunsTheVerifiedClientWithTheDisplayPreferences()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "managed-installation"));
        string clientRoot = Path.Combine(root, "client");
        string runtime = ReleaseTargetRuntime.Current ?? "linux-x64";
        ManagedInstallation installation = ManagedInstallation.Create(root, clientRoot,
            Path.Combine(root, ManagedInstallationManifest.FileName), Path.Combine(root, ManagedReleaseManifest.FileName),
            Path.Combine(root, ManagedReleaseSignature.FileName), Path.Combine(clientRoot, ReleaseTargetRuntime.ClientExecutable(runtime)),
            new ManagedReleaseIdentity(1, "test-release", runtime));

        ProcessStartInfo startInfo = ClientProcessLauncher.CreateStartInfo(installation,
            new DisplayPreferences(1024, 768, GameWindowMode.Fullscreen, GamePresentation.Integer));

        Assert.Equal(installation.ClientExecutablePath, startInfo.FileName);
        Assert.Equal(clientRoot, startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(["--window-size", "1024x768", "--window-mode", "fullscreen", "--presentation", "integer"], startInfo.ArgumentList);
    }
}
