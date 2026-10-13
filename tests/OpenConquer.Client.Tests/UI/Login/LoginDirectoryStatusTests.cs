using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginDirectoryStatusTests
{
    [Fact]
    public void UnconfiguredAuthorityIsVisibleWithAnEmptyCatalog()
    {
        LoginServerSelection selection = new();
        Assert.Equal(LoginDirectoryAvailability.NotConfigured, LoginDirectoryStatus.Resolve(false, false, false, selection));
        Assert.Equal(LoginDirectoryAvailability.NotConfigured, LoginDirectoryStatus.Resolve(false, false, true, selection));
        Assert.Equal("REALM DIRECTORY NOT CONFIGURED", LoginDirectoryStatus.Text(LoginDirectoryAvailability.NotConfigured));
    }

    [Fact]
    public void DiscoveryAndFailureNeverImplySuccessfulRealmAcquisition()
    {
        LoginServerSelection selection = new();
        Assert.Equal(LoginDirectoryAvailability.Checking, LoginDirectoryStatus.Resolve(true, false, false, selection));
        Assert.Equal(LoginDirectoryAvailability.Unavailable, LoginDirectoryStatus.Resolve(true, false, true, selection));
        Assert.Equal(LoginDirectoryAvailability.NoRegions, LoginDirectoryStatus.Resolve(true, true, false, selection));
    }

    [Fact]
    public void SignedCatalogNavigationStatusTracksOnlySelectedReferences()
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(new LoginServerCatalog([
            new LoginRegion("north", "North", [new LoginServer("realm", "Realm", "NativeRealm")]),
            new LoginRegion("empty", "Empty", []),
        ]));

        Assert.Equal(LoginDirectoryAvailability.ChooseRegion, LoginDirectoryStatus.Resolve(true, true, false, selection));
        Assert.True(selection.SelectRegionSlot(1));
        Assert.Equal(LoginDirectoryAvailability.NoServers, LoginDirectoryStatus.Resolve(true, true, false, selection));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.Equal(LoginDirectoryAvailability.ChooseServer, LoginDirectoryStatus.Resolve(true, true, false, selection));
        Assert.True(selection.SelectServerSlot(0));
        Assert.Equal(LoginDirectoryAvailability.None, LoginDirectoryStatus.Resolve(true, true, false, selection));

        selection.ReplaceCatalog(LoginServerCatalog.Empty);
        Assert.Equal(LoginDirectoryAvailability.Unavailable, LoginDirectoryStatus.Resolve(true, false, true, selection));
    }

    [Theory]
    [InlineData(1024, 768, 431, 554, 494, 26)]
    [InlineData(800, 600, 337, 432, 385, 20)]
    public void BoundsFitBetweenServerGridAndAccountPanel(int width, int height, int x, int y, int areaWidth, int areaHeight)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginSpritePlacement bounds = LoginDirectoryStatus.Bounds(layout);

        Assert.Equal(new LoginSpritePlacement(x, y, areaWidth, areaHeight), bounds);
        Assert.True(bounds.Y + bounds.Height <= layout.AccountPanel.Y);
        Assert.True(bounds.X + bounds.Width <= layout.Width);

        foreach (LoginSpritePlacement server in layout.ServerCells)
        {
            Assert.True(server.Y + server.Height < bounds.Y);
        }
    }

    [Fact]
    public void OnlyTheUnconfiguredAuthorityHasAStatusLabel()
    {
        foreach (LoginDirectoryAvailability availability in Enum.GetValues<LoginDirectoryAvailability>())
        {
            if (availability == LoginDirectoryAvailability.NotConfigured)
            {
                Assert.Equal("REALM DIRECTORY NOT CONFIGURED", LoginDirectoryStatus.Text(availability));
                continue;
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => LoginDirectoryStatus.Text(availability));
        }
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void UnconfiguredAuthorityLabelFitsReservedBounds(int width, int height)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginSpritePlacement bounds = LoginDirectoryStatus.Bounds(layout);
        (int textWidth, int textHeight, byte[] pixels) = LoginDirectoryStatusRaster.Create(LoginDirectoryStatus.Text(LoginDirectoryAvailability.NotConfigured));

        Assert.True(LoginDirectoryStatus.Scale(textWidth * 2, width) <= bounds.Width - 12);
        Assert.True(LoginDirectoryStatus.Scale(textHeight * 2, width) <= bounds.Height - 4);
        Assert.Equal(textWidth * textHeight * 4, pixels.Length);
        Assert.Contains(pixels.Where((_, index) => index % 4 == 3), alpha => alpha == 255);
        Assert.All(pixels.Where((_, index) => index % 4 == 3), alpha => Assert.True(alpha is 0 or 255));
    }

    [Fact]
    public void RasterRejectsUncontrolledContentAndNeverDrawsBlankSpace()
    {
        Assert.Throws<ArgumentException>(() => LoginDirectoryStatusRaster.Create("a"));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginDirectoryStatusRaster.Create(new string('A', 49)));
        (int width, int height, byte[] pixels) = LoginDirectoryStatusRaster.Create(" ");
        Assert.Equal(5, width);
        Assert.Equal(7, height);
        Assert.All(pixels, value => Assert.Equal((byte)0, value));
    }
}
