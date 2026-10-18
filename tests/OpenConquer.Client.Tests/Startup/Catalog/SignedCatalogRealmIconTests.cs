using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login.Selection;

namespace OpenConquer.Client.Tests.Startup.Catalog;

public sealed class SignedCatalogRealmIconTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 11, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VersionTwoRequiresSignedClosedSetIconAndPreservesIngressIdentity()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        LoginCatalogAuthority authority = SignedLoginCatalogTests.Authority(signer);
        byte[] payload = VersionTwoPayload();
        SignedLoginCatalog verified = SignedLoginCatalog.Read(payload, SignedLoginCatalogTests.Sign(signer, payload), authority, s_now);
        LoginRegion region = Assert.Single(verified.View.Regions);
        LoginServer server = Assert.Single(region.Servers);

        Assert.Equal("g01s01", server.IconId);
        Assert.Equal("RealmProtocol", server.ProtocolServerName);
        Assert.True(verified.TryGetAccountEndpoint(region, server, out LoginAccountEndpoint? endpoint));
        Assert.Equal("login.example.org", endpoint!.Host);
        Assert.Equal(9958, endpoint.Port);
    }

    [Fact]
    public void OlderSignedCatalogsRemainCompatibleAndCannotInjectIconFields()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        LoginCatalogAuthority authority = SignedLoginCatalogTests.Authority(signer);
        byte[] old = SignedLoginCatalogTests.ValidPayload();
        SignedLoginCatalog original = SignedLoginCatalog.Read(old, SignedLoginCatalogTests.Sign(signer, old), authority, s_now);
        Assert.Null(Assert.Single(Assert.Single(original.View.Regions).Servers).IconId);

        string injected = Encoding.UTF8.GetString(old).Replace("\"accountPort\":9958", "\"accountPort\":9958,\"iconId\":\"g01s01\"", StringComparison.Ordinal);
        byte[] forbidden = Encoding.UTF8.GetBytes(injected);
        Assert.Throws<InvalidDataException>(() => SignedLoginCatalog.Read(forbidden, SignedLoginCatalogTests.Sign(signer, forbidden), authority, s_now));
    }

    [Fact]
    public void UnknownIconMissingIconOrTamperingFailClosed()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        LoginCatalogAuthority authority = SignedLoginCatalogTests.Authority(signer);
        byte[] payload = VersionTwoPayload();
        string original = Encoding.UTF8.GetString(payload);
        string[] invalid =
        [
            original.Replace("g01s01", "g99s99", StringComparison.Ordinal),
            original.Replace("g01s01", "../bad", StringComparison.Ordinal),
            original.Replace(",\"iconId\":\"g01s01\"", "", StringComparison.Ordinal),
            original.Replace("\"iconId\":\"g01s01\"", "\"iconId\":\"g01s01\",\"iconId\":\"g01s01\"", StringComparison.Ordinal),
            original.Replace("\"schemaVersion\":2", "\"schemaVersion\":3", StringComparison.Ordinal),
        ];

        foreach (string json in invalid)
        {
            Assert.NotEqual(original, json);
            byte[] changed = Encoding.UTF8.GetBytes(json);
            Assert.Throws<InvalidDataException>(() => SignedLoginCatalog.Read(changed, SignedLoginCatalogTests.Sign(signer, changed), authority, s_now));
        }

        payload[^4] ^= 1;
        Assert.Throws<InvalidDataException>(() => SignedLoginCatalog.Read(payload, SignedLoginCatalogTests.Sign(signer, VersionTwoPayload()), authority, s_now));
    }

    private static byte[] VersionTwoPayload() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schemaVersion = 2,
        productId = "OpenConquer",
        sequence = 7,
        issuedAt = "2026-10-11T19:00:00Z",
        expiresAt = "2026-10-11T21:00:00Z",
        regions = new[]
        {
            new
            {
                id = "north", name = "North",
                servers = new[] { new { id = "realm-1", name = "Realm One", protocolServerName = "RealmProtocol", accountHost = "login.example.org", accountPort = 9958, iconId = "g01s01" } },
            },
        },
    });
}
