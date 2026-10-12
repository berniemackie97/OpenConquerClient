using OpenConquer.Client.UI.Login;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginCatalogUnicodeTests
{
    [Fact]
    public void Catalog_AcceptsValidSupplementaryPlaneDisplayNames()
    {
        LoginServer server = new("realm", "Realm \U0001F680", "RealmProtocol");
        LoginRegion region = new("region", "Region \U0001F30D", [server]);

        Assert.Equal("Realm \U0001F680", server.DisplayName);
        Assert.Equal("Region \U0001F30D", region.DisplayName);
    }

    [Fact]
    public void Catalog_DoesNotExpandNativeProtocolServerNameSemantics()
    {
        Assert.Throws<ArgumentException>(() => new LoginServer("realm", "Realm \U0001F680", "Native\U0001F680"));
    }

    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public void Catalog_RejectsUnpairedSurrogateCodeUnits(int codeUnit)
    {
        string name = string.Concat("Realm", new string((char)codeUnit, 1));

        Assert.Throws<ArgumentException>(() => new LoginServer("realm", name, "Protocol"));
    }

    private static readonly char[] s_value = new[] { 'A', '\uD800', 'B' };

    [Fact]
    public void Catalog_RejectsUnpairedSurrogateEmbeddedInDisplayName()
    {
        string name = new(s_value);

        Assert.Throws<ArgumentException>(() => new LoginRegion("region", name, []));
    }

    [Theory]
    [InlineData("Realm\nBreak")]
    [InlineData("Realm\u202EReversed")]
    [InlineData("Realm\u200DJoin")]
    public void Catalog_StillRejectsControlsAndDirectionalFormatting(string name)
    {
        Assert.Throws<ArgumentException>(() => new LoginServer("realm", name, "Protocol"));
    }
}
