using System.Security.Cryptography;
using System.Text.Json;

namespace OpenConquer.Product.Tool.Tests;

public sealed class ProductReleaseTrustTests
{
    [Fact]
    public void CreateWritesCanonicalTrustForDerPublicKey()
    {
        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] expectedPublicKey = publisher.ExportSubjectPublicKeyInfo();
        string publicKeyPath = Path.Combine(temporary.RootPath, "publisher-public.der");
        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseTrust.FileName);

        File.WriteAllBytes(publicKeyPath, expectedPublicKey);

        ProductReleaseTrust.Create(new ReleaseTrustOptions([publicKeyPath], outputPath));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(outputPath));

        JsonElement root = document.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());

        JsonElement keys = root.GetProperty("keys");

        Assert.Equal(JsonValueKind.Array, keys.ValueKind);
        Assert.Equal(1, keys.GetArrayLength());

        JsonElement entry = keys[0];

        Assert.Equal(JsonValueKind.Object, entry.ValueKind);
        Assert.Equal(2, entry.EnumerateObject().Count());
        Assert.Equal(ProductReleaseSignature.Algorithm, entry.GetProperty("algorithm").GetString());

        string encodedPublicKey = Assert.IsType<string>(entry.GetProperty("publicKey").GetString());

        Assert.Equal(expectedPublicKey, Convert.FromBase64String(encodedPublicKey));
    }

    [Fact]
    public void CreateAcceptsPublicKeyPemAndCanonicalizesToDer()
    {
        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] expectedPublicKey = publisher.ExportSubjectPublicKeyInfo();
        string publicKeyPath = Path.Combine(temporary.RootPath, "publisher-public.pem");
        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseTrust.FileName);

        File.WriteAllText(publicKeyPath, publisher.ExportSubjectPublicKeyInfoPem());

        ProductReleaseTrust.Create(new ReleaseTrustOptions([publicKeyPath], outputPath));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(outputPath));

        string encodedPublicKey = Assert.IsType<string>(
            document.RootElement.GetProperty("keys")[0].GetProperty("publicKey").GetString());

        Assert.Equal(expectedPublicKey, Convert.FromBase64String(encodedPublicKey));
    }

    [Fact]
    public void CreateWritesDeterministicallyIndependentOfInputOrder()
    {
        using TemporaryDirectory temporary = new();
        using ECDsa firstPublisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa secondPublisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        string firstKeyPath = Path.Combine(temporary.RootPath, "first.der");
        string secondKeyPath = Path.Combine(temporary.RootPath, "second.der");
        string firstOutputPath = Path.Combine(temporary.RootPath, "first-trust.json");
        string secondOutputPath = Path.Combine(temporary.RootPath, "second-trust.json");

        File.WriteAllBytes(firstKeyPath, firstPublisher.ExportSubjectPublicKeyInfo());
        File.WriteAllBytes(secondKeyPath, secondPublisher.ExportSubjectPublicKeyInfo());

        ProductReleaseTrust.Create(new ReleaseTrustOptions([firstKeyPath, secondKeyPath], firstOutputPath));
        ProductReleaseTrust.Create(new ReleaseTrustOptions([secondKeyPath, firstKeyPath], secondOutputPath));

        Assert.Equal(File.ReadAllBytes(firstOutputPath), File.ReadAllBytes(secondOutputPath));
    }

    [Fact]
    public void CreateRejectsEmptyKeySet()
    {
        using TemporaryDirectory temporary = new();

        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseTrust.FileName);

        Assert.Throws<ArgumentException>(() => ProductReleaseTrust.Create(new ReleaseTrustOptions([], outputPath)));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void CreateRejectsMoreThanMaximumKeyCount()
    {
        using TemporaryDirectory temporary = new();

        List<string> publicKeyPaths = [];

        for (int index = 0; index <= ProductReleaseTrust.MaximumKeyCount; index++)
        {
            using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            string publicKeyPath = Path.Combine(temporary.RootPath, $"publisher-{index}.der");

            File.WriteAllBytes(publicKeyPath, publisher.ExportSubjectPublicKeyInfo());

            publicKeyPaths.Add(publicKeyPath);
        }

        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseTrust.FileName);

        Assert.Throws<ArgumentException>(() => ProductReleaseTrust.Create(new ReleaseTrustOptions(publicKeyPaths, outputPath)));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void CreateRejectsDuplicatePublicKeys()
    {
        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] publicKey = publisher.ExportSubjectPublicKeyInfo();
        string firstKeyPath = Path.Combine(temporary.RootPath, "first.der");
        string secondKeyPath = Path.Combine(temporary.RootPath, "second.der");
        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseTrust.FileName);

        File.WriteAllBytes(firstKeyPath, publicKey);
        File.WriteAllBytes(secondKeyPath, publicKey);

        Assert.Throws<InvalidDataException>(() => ProductReleaseTrust.Create(new ReleaseTrustOptions([firstKeyPath, secondKeyPath], outputPath)));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void CreateRejectsPrivateKeyPem()
    {
        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        string privateKeyPath = Path.Combine(temporary.RootPath, "publisher-private.pem");
        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseTrust.FileName);

        File.WriteAllText(privateKeyPath, publisher.ExportPkcs8PrivateKeyPem());

        Assert.Throws<InvalidDataException>(() => ProductReleaseTrust.Create(new ReleaseTrustOptions([privateKeyPath], outputPath)));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void CreateRejectsNonP256PublicKey()
    {
        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        string publicKeyPath = Path.Combine(temporary.RootPath, "publisher-public.der");
        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseTrust.FileName);

        File.WriteAllBytes(publicKeyPath, publisher.ExportSubjectPublicKeyInfo());

        Assert.Throws<InvalidDataException>(() => ProductReleaseTrust.Create(new ReleaseTrustOptions([publicKeyPath], outputPath)));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void CreateRejectsMalformedPublicKey()
    {
        using TemporaryDirectory temporary = new();

        string publicKeyPath = Path.Combine(temporary.RootPath, "publisher-public.der");
        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseTrust.FileName);

        File.WriteAllBytes(publicKeyPath, [0x01, 0x02, 0x03, 0x04]);

        Assert.Throws<InvalidDataException>(() => ProductReleaseTrust.Create(new ReleaseTrustOptions([publicKeyPath], outputPath)));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void CreateRejectsLinkedPublicKey()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        string targetPath = Path.Combine(temporary.RootPath, "publisher-target.der");
        string linkedPath = Path.Combine(temporary.RootPath, "publisher-linked.der");
        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseTrust.FileName);

        File.WriteAllBytes(targetPath, publisher.ExportSubjectPublicKeyInfo());
        File.CreateSymbolicLink(linkedPath, targetPath);

        Assert.Throws<InvalidDataException>(() => ProductReleaseTrust.Create(new ReleaseTrustOptions([linkedPath], outputPath)));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void CreateRejectsOutputReplacingPublicKeyInput()
    {
        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        string publicKeyPath = Path.Combine(temporary.RootPath, "publisher-public.der");
        byte[] originalBytes = publisher.ExportSubjectPublicKeyInfo();

        File.WriteAllBytes(publicKeyPath, originalBytes);

        Assert.Throws<InvalidOperationException>(() => ProductReleaseTrust.Create(new ReleaseTrustOptions([publicKeyPath], publicKeyPath)));

        Assert.Equal(originalBytes, File.ReadAllBytes(publicKeyPath));
    }

    [Fact]
    public void CreateRejectsExistingOutputWithoutChangingIt()
    {
        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        string publicKeyPath = Path.Combine(temporary.RootPath, "publisher-public.der");
        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseTrust.FileName);

        File.WriteAllBytes(publicKeyPath, publisher.ExportSubjectPublicKeyInfo());
        File.WriteAllText(outputPath, "keep");

        Assert.Throws<InvalidOperationException>(() => ProductReleaseTrust.Create(new ReleaseTrustOptions([publicKeyPath], outputPath)));

        Assert.Equal("keep", File.ReadAllText(outputPath));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"openconquer-release-trust-{Guid.NewGuid():N}");

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(_path);
        }

        public string RootPath => _path;

        public void Dispose()
        {
            try
            {
                Directory.Delete(_path, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
