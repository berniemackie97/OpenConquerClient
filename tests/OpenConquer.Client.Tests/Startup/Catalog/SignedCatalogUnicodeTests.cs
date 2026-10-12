using System.Security.Cryptography;
using System.Text;
using OpenConquer.Client.Startup.Catalog;

namespace OpenConquer.Client.Tests.Startup.Catalog;

public sealed class SignedCatalogUnicodeTests
{
    [Fact]
    public void SignedCatalog_PreservesSupplementaryUnicodeInVerifiedDisplayName()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string json = Encoding.UTF8.GetString(SignedLoginCatalogTests.ValidPayload());
        string changed = json.Replace("\"Realm One\"", "\"Realm \\ud83d\\ude80\"", StringComparison.Ordinal);
        Assert.NotEqual(json, changed);

        byte[] payload = Encoding.UTF8.GetBytes(changed);
        SignedLoginCatalog catalog = SignedLoginCatalog.Read(payload, SignedLoginCatalogTests.Sign(signer, payload),
            SignedLoginCatalogTests.Authority(signer), new DateTimeOffset(2026, 10, 11, 20, 0, 0, TimeSpan.Zero));

        Assert.Equal("Realm \U0001F680", catalog.View.Regions[0].Servers[0].DisplayName);
        Assert.Equal("NativeRealm1", catalog.View.Regions[0].Servers[0].ProtocolServerName);
    }

    [Fact]
    public void SignedCatalog_RejectsSupplementaryProtocolNameWithoutRelaxingRoutingContract()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string json = Encoding.UTF8.GetString(SignedLoginCatalogTests.ValidPayload());
        string changed = json.Replace("\"NativeRealm1\"", "\"Native\\ud83d\\ude80\"", StringComparison.Ordinal);
        Assert.NotEqual(json, changed);

        byte[] payload = Encoding.UTF8.GetBytes(changed);

        Assert.Throws<InvalidDataException>(() => SignedLoginCatalog.Read(payload, SignedLoginCatalogTests.Sign(signer, payload),
            SignedLoginCatalogTests.Authority(signer), new DateTimeOffset(2026, 10, 11, 20, 0, 0, TimeSpan.Zero)));
    }
}
