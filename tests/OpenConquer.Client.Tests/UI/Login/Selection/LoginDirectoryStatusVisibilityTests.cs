using OpenConquer.Client.UI.Login.Selection;

namespace OpenConquer.Client.Tests.UI.Login.Selection;

public sealed class LoginDirectoryStatusVisibilityTests
{
    [Fact]
    public void OnlyAnAbsentBuildAuthorityMayDrawTheOperationalBanner()
    {
        foreach (LoginDirectoryAvailability availability in Enum.GetValues<LoginDirectoryAvailability>())
        {
            Assert.Equal(availability == LoginDirectoryAvailability.NotConfigured, LoginDirectoryStatus.IsVisible(availability));
        }
    }

    [Fact]
    public void AuthenticatedDirectoryAndSelectionStatesNeverRequireAStatusOverlay()
    {
        LoginServerSelection selection = new();
        Assert.True(LoginDirectoryStatus.IsVisible(LoginDirectoryStatus.Resolve(false, false, false, selection)));
        Assert.False(LoginDirectoryStatus.IsVisible(LoginDirectoryStatus.Resolve(true, false, false, selection)));
        Assert.False(LoginDirectoryStatus.IsVisible(LoginDirectoryStatus.Resolve(true, false, true, selection)));
        Assert.False(LoginDirectoryStatus.IsVisible(LoginDirectoryStatus.Resolve(true, true, false, selection)));

        selection.ReplaceCatalog(new LoginServerCatalog([
            new LoginRegion("local", "Local Development", [new LoginServer("local-realm", "OpenConquer Local", "LocalRealm")]),
        ]));

        Assert.False(LoginDirectoryStatus.IsVisible(LoginDirectoryStatus.Resolve(true, true, false, selection)));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.False(LoginDirectoryStatus.IsVisible(LoginDirectoryStatus.Resolve(true, true, false, selection)));
        Assert.True(selection.SelectServerSlot(0));
        Assert.False(LoginDirectoryStatus.IsVisible(LoginDirectoryStatus.Resolve(true, true, false, selection)));
    }
}
