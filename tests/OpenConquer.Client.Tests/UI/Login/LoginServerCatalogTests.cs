using OpenConquer.Client.UI.Login;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginServerCatalogTests
{
    [Fact]
    public void Catalog_DefensivelyCopiesRegionAndServerLists()
    {
        List<LoginServer> servers = [new("realm-1", "Realm One", "ProtocolRealm1")];
        LoginRegion region = new("region-1", "Region One", servers);
        List<LoginRegion> regions = [region];
        LoginServerCatalog catalog = new(regions);

        servers.Clear();
        regions.Clear();

        Assert.Single(catalog.Regions);
        Assert.Single(catalog.Regions[0].Servers);
        Assert.Equal("ProtocolRealm1", catalog.Regions[0].Servers[0].ProtocolServerName);
        Assert.False(catalog.Regions is List<LoginRegion>);
        Assert.False(region.Servers is List<LoginServer>);
    }

    [Fact]
    public void Catalog_RejectsAmbiguousIdentities()
    {
        LoginServer server = new("realm-1", "Realm One", "ProtocolRealm1");
        LoginServer conflictingId = new("realm-1", "Realm Two", "ProtocolRealm2");
        LoginServer conflictingProtocolName = new("realm-2", "Realm Two", "ProtocolRealm1");

        Assert.Throws<ArgumentException>(() => new LoginRegion("region-1", "Region", [server, conflictingId]));
        Assert.Throws<ArgumentException>(() => new LoginRegion("region-1", "Region", [server, conflictingProtocolName]));
        Assert.Throws<ArgumentException>(() => new LoginServerCatalog([
            new LoginRegion("region-1", "One", [server]),
            new LoginRegion("region-1", "Two", [])]));
    }

    [Theory]
    [InlineData("../realm", "A", "Protocol")]
    [InlineData("realm", "Bad\nLabel", "Protocol")]
    [InlineData("realm", "Clean", "Bad\rProtocol")]
    [InlineData("realm", "Leading ", "Protocol")]
    [InlineData("realm", "a\u202Eb", "Protocol")]
    public void Server_RejectsUnsafeNames(string id, string label, string protocolName)
    {
        Assert.Throws<ArgumentException>(() => new LoginServer(id, label, protocolName));
    }

    [Fact]
    public void Catalog_EnforcesBoundedRegionAndServerCounts()
    {
        LoginRegion[] tooManyRegions = Enumerable.Range(0, LoginServerCatalog.MaximumRegions + 1)
            .Select(index => new LoginRegion($"r{index}", "Region", [])).ToArray();
        Assert.Throws<ArgumentException>(() => new LoginServerCatalog(tooManyRegions));

        LoginServer[] tooManyServers = Enumerable.Range(0, LoginServerCatalog.MaximumServersPerRegion + 1)
            .Select(index => new LoginServer($"s{index}", "Server", $"Protocol{index}")).ToArray();
        Assert.Throws<ArgumentException>(() => new LoginRegion("region", "Region", tooManyServers));

        LoginRegion[] tooManyTotal = Enumerable.Range(0, 9)
            .Select(region => new LoginRegion($"r{region}", "Region", Enumerable.Range(0, LoginServerCatalog.MaximumServersPerRegion)
                .Select(server => new LoginServer($"s{server}", "Realm", $"Protocol{region}_{server}")))).ToArray();
        Assert.Throws<ArgumentException>(() => new LoginServerCatalog(tooManyTotal));
    }

    [Fact]
    public void EmptyCatalog_HasNoSelectableIdentity()
    {
        Assert.Empty(LoginServerCatalog.Empty.Regions);
        LoginServerSelection selection = new();

        Assert.Null(selection.SelectedRegion);
        Assert.Null(selection.SelectedServer);
        Assert.False(selection.SelectRegionSlot(0));
        Assert.False(selection.SelectServerSlot(0));
    }
}
