using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login.Selection;

namespace OpenConquer.Client.Tests.Startup.Catalog;

public sealed class SignedLoginCatalogTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 11, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SignedCatalog_ExposesValidatedViewAndSeparateAccountIngress()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        LoginCatalogAuthority authority = Authority(signer);
        byte[] payload = ValidPayload();
        SignedLoginCatalog verified = SignedLoginCatalog.Read(payload, Sign(signer, payload), authority, s_now);

        Assert.Equal(7, verified.Sequence);
        Assert.True(verified.IsCurrent(s_now));
        Assert.False(verified.IsCurrent(s_now.AddHours(1)));
        Assert.Equal("north", verified.View.Regions[0].Id);
        Assert.Equal("Realm One", verified.View.Regions[0].Servers[0].DisplayName);
        Assert.Equal("NativeRealm1", verified.View.Regions[0].Servers[0].ProtocolServerName);
        Assert.True(verified.TryGetAccountEndpoint(verified.View.Regions[0], verified.View.Regions[0].Servers[0], out LoginAccountEndpoint? endpoint));
        Assert.Equal("login.example.org", endpoint!.Host);
        Assert.Equal(9958, endpoint.Port);
        Assert.False(verified.TryGetAccountEndpoint(
            new LoginRegion("north", "North", []),
            verified.View.Regions[0].Servers[0], out _));
        Assert.False(verified.TryGetAccountEndpoint(
            verified.View.Regions[0],
            new LoginServer("realm-1", "Realm One", "NativeRealm1"), out _));
    }

    [Fact]
    public void SignatureRejectsChangedBytesAndOtherIssuer()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa unrelated = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] payload = ValidPayload();
        byte[] signature = Sign(signer, payload);
        payload[^4] ^= 1;

        Assert.Throws<InvalidDataException>(() => SignedLoginCatalog.Read(payload, signature, Authority(signer), s_now));
        Assert.Throws<InvalidDataException>(() => SignedLoginCatalog.Read(ValidPayload(), signature, Authority(unrelated), s_now));
        Assert.Throws<InvalidDataException>(() => SignedLoginCatalog.Read(ValidPayload(), signature.AsSpan()[..63], Authority(signer), s_now));
    }

    [Fact]
    public void SignedButMalformedOrExpiredDocumentsFailClosed()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        LoginCatalogAuthority authority = Authority(signer);
        byte[] valid = ValidPayload();
        string json = Encoding.UTF8.GetString(valid);

        string[] invalid =
        [
            json.Replace("\"productId\":\"OpenConquer\"", "\"productId\":\"OpenConquer\",\"productId\":\"OpenConquer\"", StringComparison.Ordinal),
            json.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal),
            json.Replace("\"accountPort\":9958", "\"accountPort\":0", StringComparison.Ordinal),
            json.Replace("\"accountHost\":\"login.example.org\"", "\"accountHost\":\"http://evil.example/\"", StringComparison.Ordinal),
            json.Replace("\"protocolServerName\":\"NativeRealm1\"", "\"protocolServerName\":\"Bad\\nName\"", StringComparison.Ordinal),
            json.Replace("\"issuedAt\":\"2026-10-11T19:00:00Z\"", "\"issuedAt\":\"2026-10-12T19:00:00Z\"", StringComparison.Ordinal),
            json.Replace("\"expiresAt\":\"2026-10-11T21:00:00Z\"", "\"expiresAt\":\"2026-10-13T21:00:00Z\"", StringComparison.Ordinal),
            json.Replace("\"expiresAt\":\"2026-10-11T21:00:00Z\"", "\"expiresAt\":\"2026-10-11T19:00:00Z\"", StringComparison.Ordinal),
            json.Replace("\"sequence\":7", "\"sequence\":0", StringComparison.Ordinal),
            json.Replace("\"regions\":[", "\"unexpected\":123,\"regions\":[", StringComparison.Ordinal),
        ];

        foreach (string source in invalid)
        {
            byte[] payload = Encoding.UTF8.GetBytes(source);
            Assert.NotEqual(json, source);
            Assert.Throws<InvalidDataException>(() => SignedLoginCatalog.Read(payload, Sign(signer, payload), authority, s_now));
        }

        Assert.Throws<InvalidDataException>(() => SignedLoginCatalog.Read(valid, Sign(signer, valid), authority, s_now.AddHours(2)));
        Assert.Throws<InvalidDataException>(() => SignedLoginCatalog.Read(new byte[SignedLoginCatalog.MaximumCatalogBytes + 1], new byte[64], authority, s_now));
    }

    [Theory]
    [InlineData("ABCDEFGHIJKLMNOP")]
    [InlineData("Main World")]
    [InlineData("naïve")]
    [InlineData("Name.With.Dot")]
    [InlineData("../realm")]
    public void SignedCatalogRejectsAuthenticatedButUnrepresentableProtocolIdentity(string name)
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string json = Encoding.UTF8.GetString(ValidPayload());
        string changed = json.Replace("NativeRealm1", name, StringComparison.Ordinal);
        Assert.NotEqual(json, changed);
        byte[] payload = Encoding.UTF8.GetBytes(changed);

        Assert.Throws<InvalidDataException>(() =>
            SignedLoginCatalog.Read(payload, Sign(signer, payload), Authority(signer), s_now));
    }

    [Fact]
    public void SignedCatalogAcceptsFullFifteenByteProtocolIdentity()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string json = Encoding.UTF8.GetString(ValidPayload());
        byte[] payload = Encoding.UTF8.GetBytes(json.Replace("NativeRealm1", "ABCDEFGHIJKLMNO", StringComparison.Ordinal));
        SignedLoginCatalog catalog = SignedLoginCatalog.Read(payload, Sign(signer, payload), Authority(signer), s_now);
        Assert.Equal("ABCDEFGHIJKLMNO", catalog.View.Regions[0].Servers[0].ProtocolServerName);
    }

    [Fact]
    public void Authority_RequiresP256AndHttpsLocations()
    {
        using ECDsa p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        byte[] key = p256.ExportSubjectPublicKeyInfo();

        Assert.Throws<ArgumentException>(() => new LoginCatalogAuthority(new Uri("http://catalog.example.org/realms.json"), new Uri("https://catalog.example.org/realms.sig"), key));
        Assert.Throws<ArgumentException>(() => new LoginCatalogAuthority(new Uri("https://catalog.example.org/realms.json"), new Uri("https://catalog.example.org/realms.json"), key));
        Assert.Throws<ArgumentException>(() => new LoginCatalogAuthority(new Uri("https://catalog.example.org/realms.json"), new Uri("https://catalog.example.org/realms.sig"), p384.ExportSubjectPublicKeyInfo()));
    }

    internal static LoginCatalogAuthority Authority(ECDsa signer) => new(
        new Uri("https://catalog.example.org/realms.json"), new Uri("https://catalog.example.org/realms.sig"), signer.ExportSubjectPublicKeyInfo());

    internal static byte[] Sign(ECDsa signer, byte[] payload) => signer.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    internal static byte[] ValidPayload() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schemaVersion = 1,
        productId = "OpenConquer",
        sequence = 7,
        issuedAt = "2026-10-11T19:00:00Z",
        expiresAt = "2026-10-11T21:00:00Z",
        regions = new[]
        {
            new
            {
                id = "north",
                name = "North",
                servers = new[]
                {
                    new { id = "realm-1", name = "Realm One", protocolServerName = "NativeRealm1", accountHost = "login.example.org", accountPort = 9958 },
                },
            },
        },
    });
}
