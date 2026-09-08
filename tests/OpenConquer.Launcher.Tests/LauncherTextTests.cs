using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests;

public sealed class LauncherTextTests
{
    [Fact]
    public void EveryInstallationIssueHasActionablePlayerText()
    {
        foreach (ManagedInstallationIssue issue in Enum.GetValues<ManagedInstallationIssue>())
        {
            (string title, string detail) = LauncherText.For(
                new LauncherState.InstallationUnavailable(issue));

            Assert.False(string.IsNullOrWhiteSpace(title));
            Assert.False(string.IsNullOrWhiteSpace(detail));
        }
    }
}
