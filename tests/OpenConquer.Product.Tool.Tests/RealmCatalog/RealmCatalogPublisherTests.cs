using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenConquer.Product.Tool.Tests;

public sealed class RealmCatalogPublisherTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 12, 22, 0, 0, TimeSpan.Zero);
    private const string Definition = """
        {"regions":[{"id":"north","name":"North","servers":[{"id":"realm-one","name":"Realm One","protocolServerName":"RealmOne","accountHost":"login.example.org","accountPort":9958,"iconId":"g02s13"}]}]}
        """;

    [Fact]
    public void Publisher_EmitsClientCompatibleFieldsAndRawP1363Signature()
    {
        using Fixture fixture = new();
        RealmCatalogPublishOptions options = fixture.Options();
        RealmCatalogPublisher.Create(options, s_now);

        byte[] payload = File.ReadAllBytes(Path.Combine(options.OutputPath, "realms.json"));
        byte[] signature = File.ReadAllBytes(Path.Combine(options.OutputPath, "realms.sig"));
        using JsonDocument authority = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(options.OutputPath, "authority.json")));
        Assert.Equal(64, signature.Length);
        Assert.Equal("https://catalog.example.org/realms.json", authority.RootElement.GetProperty("catalogUri").GetString());
        Assert.Equal("https://catalog.example.org/realms.sig", authority.RootElement.GetProperty("signatureUri").GetString());

        byte[] publicKey = authority.RootElement.GetProperty("publicKeySpki").GetBytesFromBase64();
        using ECDsa verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(publicKey, out int consumed);
        Assert.Equal(publicKey.Length, consumed);
        Assert.Equal(256, verifier.KeySize);
        Assert.True(verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        payload[^1] ^= 1;
        Assert.False(verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(options.OutputPath, "realms.json")));
        JsonElement root = document.RootElement;
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("OpenConquer", root.GetProperty("productId").GetString());
        Assert.Equal(9, root.GetProperty("sequence").GetInt64());
        Assert.Equal("2026-10-12T21:59:00Z", root.GetProperty("issuedAt").GetString());
        Assert.Equal("2026-10-13T02:00:00Z", root.GetProperty("expiresAt").GetString());
        JsonElement region = Assert.Single(root.GetProperty("regions").EnumerateArray());
        JsonElement server = Assert.Single(region.GetProperty("servers").EnumerateArray());
        Assert.Equal("g02s13", server.GetProperty("iconId").GetString());
        Assert.Equal("RealmOne", server.GetProperty("protocolServerName").GetString());
        Assert.Equal(9958, server.GetProperty("accountPort").GetInt32());
        Assert.Equal(3, Directory.EnumerateFiles(options.OutputPath).Count());
    }

    [Fact]
    public void Publishing_RejectsAnExistingDirectoryWithoutChangingIt()
    {
        using Fixture fixture = new();
        RealmCatalogPublishOptions options = fixture.Options();
        RealmCatalogPublisher.Create(options, s_now);
        byte[] original = File.ReadAllBytes(Path.Combine(options.OutputPath, "realms.json"));
        Assert.Throws<InvalidOperationException>(() => RealmCatalogPublisher.Create(options, s_now));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(options.OutputPath, "realms.json")));
    }

    [Theory]
    [InlineData("g02s13", "g02s14")]
    [InlineData("g02s13", "../../key")]
    [InlineData("login.example.org", "http://bad/")]
    [InlineData("9958", "0")]
    [InlineData("RealmOne", "AProtocolServerNameThatExceedsTheNativeField")]
    [InlineData("\"iconId\":\"g02s13\"", "\"iconId\":\"g02s13\",\"iconId\":\"g02s13\"")]
    [InlineData("\"accountPort\":9958", "\"accountPort\":9958,\"unrecognized\":true")]
    public void Publisher_RejectsUntrustedOrUnsupportedMetadata(string oldValue, string newValue)
    {
        using Fixture fixture = new();
        string changed = Definition.Replace(oldValue, newValue, StringComparison.Ordinal);
        Assert.NotEqual(Definition, changed);
        File.WriteAllText(fixture.DefinitionPath, changed);
        Assert.Throws<InvalidDataException>(() => RealmCatalogPublisher.Create(fixture.Options(), s_now));
        Assert.False(Directory.Exists(fixture.OutputPath));
    }

    [Theory]
    [InlineData("{\"regions\":[],\"regions\":[]}")]
    [InlineData("{\"regions\":{},\"extra\":1}")]
    [InlineData("{\"regions\":[{\"id\":\"a\",\"name\":\"A\",\"servers\":[]},{\"id\":\"a\",\"name\":\"B\",\"servers\":[]}]}")]
    [InlineData("{\"regions\":null}")]
    public void Publisher_RejectsDuplicateOrMalformedDefinitionStructure(string definition)
    {
        using Fixture fixture = new();
        File.WriteAllText(fixture.DefinitionPath, definition);
        Assert.Throws<InvalidDataException>(() => RealmCatalogPublisher.Create(fixture.Options(), s_now));
        Assert.False(Directory.Exists(fixture.OutputPath));
    }

    [Fact]
    public void Publisher_RejectsExpiredOrTooLongCatalogs()
    {
        using Fixture fixture = new();
        RealmCatalogPublishOptions options = fixture.Options();
        Assert.Throws<InvalidDataException>(() => RealmCatalogPublisher.Create(options with { ExpiresAt = s_now.AddHours(25) }, s_now));
        Assert.Throws<InvalidDataException>(() => RealmCatalogPublisher.Create(options with { ExpiresAt = s_now }, s_now));
        Assert.Throws<InvalidDataException>(() => RealmCatalogPublisher.Create(options with { Sequence = 0 }, s_now));
        Assert.Throws<InvalidDataException>(() => RealmCatalogPublisher.Create(options with { IssuedAt = s_now.AddMinutes(6) }, s_now));
        Assert.False(Directory.Exists(fixture.OutputPath));
    }

    [Theory]
    [InlineData("http://catalog.example.org/realms.json")]
    [InlineData("https://catalog.example.org/")]
    [InlineData("https://user@catalog.example.org/realms.json")]
    [InlineData("https://catalog.example.org/realms.json?b=1")]
    public void Publisher_RejectsUntrustedPublicationUris(string catalogUri)
    {
        using Fixture fixture = new();
        RealmCatalogPublishOptions options = fixture.Options() with
        {
            CatalogUri = new Uri(catalogUri)
        };
        Assert.Throws<InvalidDataException>(() => RealmCatalogPublisher.Create(options, s_now));
        Assert.False(Directory.Exists(fixture.OutputPath));
    }

    [Fact]
    public void Publisher_RejectsNonP256PrivateKeysAndLeavesNoOutput()
    {
        using Fixture fixture = new();
        using ECDsa other = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        File.WriteAllBytes(fixture.PrivateKeyPath, other.ExportPkcs8PrivateKey());
        Assert.Throws<InvalidDataException>(() => RealmCatalogPublisher.Create(fixture.Options(), s_now));
        Assert.False(Directory.Exists(fixture.OutputPath));
    }

    [Fact]
    public void Publisher_RejectsWorldReadablePrivateKeysOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        using Fixture fixture = new();
        File.SetUnixFileMode(fixture.PrivateKeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        Assert.Throws<UnauthorizedAccessException>(() => RealmCatalogPublisher.Create(fixture.Options(), s_now));
        Assert.False(Directory.Exists(fixture.OutputPath));
    }

    [Fact]
    public void CommandLine_ParsesExplicitPathsAndRejectsExtraOptions()
    {
        string root = Path.GetTempPath();
        string[] args = ["create-realm-catalog", "--definition", "realms.json", "--private-key", "key.pk8", "--sequence", "9", "--issued-utc", "2026-10-12T21:59:00Z", "--expires-utc", "2026-10-13T02:00:00Z", "--catalog-uri", "https://catalog.example.org/realms.json", "--signature-uri", "https://catalog.example.org/realms.sig", "--output", "publish"];
        Assert.True(RealmCatalogTool.TryParse(args, root, out RealmCatalogPublishOptions? options, out string? error), error);
        Assert.NotNull(options);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "publish")), options.OutputPath);
        Assert.Equal(9, options.Sequence);
        Assert.True(RealmCatalogTool.IsCommand("create-realm-catalog"));
        Assert.False(RealmCatalogTool.IsCommand("create-release-catalog"));
        Assert.False(RealmCatalogTool.TryParse([.. args, "--sequence", "10"], root, out _, out _));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"openconquer-realm-publish-{Guid.NewGuid():N}");
        public string DefinitionPath => Path.Combine(_root, "definition.json");
        public string PrivateKeyPath => Path.Combine(_root, "issuer.pk8");
        public string OutputPath => Path.Combine(_root, "publication");

        public Fixture()
        {
            Directory.CreateDirectory(_root);
            using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            File.WriteAllBytes(PrivateKeyPath, signer.ExportPkcs8PrivateKey());
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(PrivateKeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            File.WriteAllText(DefinitionPath, Definition, Encoding.UTF8);
        }

        public RealmCatalogPublishOptions Options() => new(DefinitionPath, PrivateKeyPath, 9, s_now.AddMinutes(-1), s_now.AddHours(4),
            new Uri("https://catalog.example.org/realms.json"), new Uri("https://catalog.example.org/realms.sig"), OutputPath);

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
