using System.IO.Compression;
using System.Security.Cryptography;

namespace OpenConquer.Product.Tool.Tests;

public sealed class ProductReleasePackageTests
{
    [Fact]
    public void CreateProducesDeterministicStrictPackage()
    {
        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateSignedRelease(4, "four");
        string secondPackage = Path.Combine(fixture.RootPath, "release-second.zip");

        ProductReleasePackage.Create(new ReleasePackageOptions(release.ClientRoot,
            release.ManifestPath, release.SignaturePath, fixture.PublicKeyPath,
            secondPackage));

        ProductReleaseManifest manifest = ProductReleasePackage.Validate(release.PackagePath);
        Assert.Equal(4UL, manifest.ReleaseSequence);
        Assert.Equal(File.ReadAllBytes(release.PackagePath), File.ReadAllBytes(secondPackage));

        using ZipArchive archive = ZipFile.OpenRead(release.PackagePath);
        Assert.DoesNotContain(archive.Entries, entry => string.IsNullOrEmpty(entry.Name));
        Assert.Equal(
            [
                ProductReleaseManifest.FileName,
                ProductReleaseSignature.FileName,
                $"client/{ProductTargetRuntime.ClientExecutable(fixture.Runtime)}",
                "client/data/content.bin",
            ],
            archive.Entries.Select(entry => entry.FullName));
    }

    [Fact]
    public void CreateRejectsChangedClientAndWrongPublisherKey()
    {
        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateSignedRelease(5, "five");
        File.AppendAllText(Path.Combine(release.ClientRoot,
            ProductTargetRuntime.ClientExecutable(fixture.Runtime)), "changed");

        Assert.Throws<InvalidDataException>(() => ProductReleasePackage.Create(
            new ReleasePackageOptions(release.ClientRoot, release.ManifestPath,
                release.SignaturePath, fixture.PublicKeyPath,
                Path.Combine(fixture.RootPath, "changed.zip"))));

        using ECDsa otherPublisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string otherKeyPath = Path.Combine(fixture.RootPath, "other.der");
        File.WriteAllBytes(otherKeyPath, otherPublisher.ExportSubjectPublicKeyInfo());
        Assert.Throws<InvalidDataException>(() => ProductReleasePackage.Create(
            new ReleasePackageOptions(release.ClientRoot, release.ManifestPath,
                release.SignaturePath, otherKeyPath,
                Path.Combine(fixture.RootPath, "wrong-key.zip"))));
    }

    [Fact]
    public void ValidateRejectsUnexpectedAndLinkedArchiveEntries()
    {
        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateSignedRelease(6, "six");
        string extraPackage = Path.Combine(fixture.RootPath, "extra.zip");
        File.Copy(release.PackagePath, extraPackage);
        using (ZipArchive archive = ZipFile.Open(extraPackage, ZipArchiveMode.Update))
        {
            archive.CreateEntry("unexpected");
        }

        Assert.Throws<InvalidDataException>(() =>
            ProductReleasePackage.Validate(extraPackage));

        string linkedPackage = Path.Combine(fixture.RootPath, "linked.zip");
        File.Copy(release.PackagePath, linkedPackage);
        using (ZipArchive archive = ZipFile.Open(linkedPackage, ZipArchiveMode.Update))
        {
            ZipArchiveEntry executable = archive.GetEntry(
                $"client/{ProductTargetRuntime.ClientExecutable(fixture.Runtime)}")!;
            executable.ExternalAttributes = (0xa000 | 0x01ff) << 16;
        }

        Assert.Throws<InvalidDataException>(() =>
            ProductReleasePackage.Validate(linkedPackage));
    }

    [Fact]
    public void CatalogAuthenticatesPackageIdentitiesAndSupportsMultipleRuntimes()
    {
        using Fixture fixture = new();
        ReleaseFiles first = fixture.CreateSignedRelease(7, "seven", "first.zip");
        string otherRuntime = fixture.Runtime == "win-x64" ? "linux-x64" : "win-x64";
        ReleaseFiles second = fixture.CreateSignedRelease(8, "eight", "second.zip",
            otherRuntime);
        string catalogPath = Path.Combine(fixture.RootPath, ProductReleaseCatalog.FileName);

        ProductReleaseCatalog.Create(new ReleaseCatalogOptions(
            [second.PackagePath, first.PackagePath],
            new Uri("https://releases.example.test/client/"),
            [fixture.PublicKeyPath], DateTimeOffset.UtcNow.AddDays(7), catalogPath));

        byte[] catalogBytes = File.ReadAllBytes(catalogPath);
        IReadOnlyList<ProductReleaseCatalogEntry> entries =
            ProductReleaseCatalog.Read(catalogBytes);
        Assert.Equal(2, entries.Count);
        Assert.Equal(entries.OrderBy(entry => entry.TargetRuntime, StringComparer.Ordinal)
            .ThenBy(entry => entry.ReleaseSequence), entries);
        ProductReleaseCatalogEntry firstEntry = Assert.Single(entries,
            entry => entry.ReleaseSequence == 7);
        Assert.Equal(new Uri("https://releases.example.test/client/first.zip"),
            firstEntry.PackageUri);
        Assert.Equal(new FileInfo(first.PackagePath).Length, firstEntry.PackageLength);
        Assert.Equal(SHA256.HashData(File.ReadAllBytes(first.PackagePath)),
            firstEntry.PackageSha256);

        string rawSignaturePath = fixture.Sign(catalogPath, "catalog-raw.sig");
        string envelopePath = Path.Combine(fixture.RootPath,
            ProductReleaseCatalog.SignatureFileName);
        ProductReleaseSignature.Create(new CatalogSignatureOptions(catalogPath,
            fixture.PublicKeyPath, rawSignaturePath, envelopePath));
        ProductReleaseSignature.VerifyEnvelope(catalogBytes,
            File.ReadAllBytes(envelopePath), fixture.PublicKeyPath);
    }

    [Fact]
    public void CatalogRejectsDuplicateReleaseIdentityAndUnsafeOrigin()
    {
        using Fixture fixture = new();
        ReleaseFiles first = fixture.CreateSignedRelease(9, "first", "first.zip");
        ReleaseFiles duplicate = fixture.CreateSignedRelease(9, "second", "second.zip");

        Assert.Throws<InvalidDataException>(() => ProductReleaseCatalog.Create(
            new ReleaseCatalogOptions([first.PackagePath, duplicate.PackagePath],
                new Uri("https://releases.example.test/client/"),
                [fixture.PublicKeyPath],
                DateTimeOffset.UtcNow.AddDays(7),
                Path.Combine(fixture.RootPath, "duplicate.json"))));
        Assert.False(ProductReleaseUri.TryParseBase("http://releases.example.test/",
            out _));
        Assert.False(ProductReleaseUri.TryParseBase(
            "https://user:secret@releases.example.test/", out _));
        Assert.False(ProductReleaseUri.TryParseBase(
            "https://releases.example.test/client", out _));
    }

    [Fact]
    public void CatalogRejectsPackageFromUnacceptedPublisher()
    {
        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateSignedRelease(10, "ten", "ten.zip");
        using ECDsa otherPublisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string otherKeyPath = Path.Combine(fixture.RootPath, "unaccepted.der");
        File.WriteAllBytes(otherKeyPath, otherPublisher.ExportSubjectPublicKeyInfo());

        Assert.Throws<InvalidDataException>(() => ProductReleaseCatalog.Create(
            new ReleaseCatalogOptions([release.PackagePath],
                new Uri("https://releases.example.test/client/"), [otherKeyPath],
                DateTimeOffset.UtcNow.AddDays(7),
                Path.Combine(fixture.RootPath, "unaccepted.json"))));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _rootPath = Path.Combine(Path.GetTempPath(),
            $"openconquer-package-tests-{Guid.NewGuid():N}");
        private readonly ECDsa _publisher =
            ECDsa.Create(ECCurve.NamedCurves.nistP256);

        public Fixture()
        {
            Directory.CreateDirectory(_rootPath);
            PublicKeyPath = Path.Combine(_rootPath, "publisher.der");
            File.WriteAllBytes(PublicKeyPath, _publisher.ExportSubjectPublicKeyInfo());
            Runtime = ProductTargetRuntime.Current ?? "linux-x64";
        }

        public string RootPath => _rootPath;

        public string PublicKeyPath
        {
            get;
        }

        public string Runtime
        {
            get;
        }

        public ReleaseFiles CreateSignedRelease(
            ulong sequence,
            string contents,
            string? packageName = null,
            string? runtime = null)
        {
            string releaseRoot = Path.Combine(_rootPath,
                $"release-{sequence}-{Guid.NewGuid():N}");
            string clientRoot = Path.Combine(releaseRoot, "client");
            Directory.CreateDirectory(Path.Combine(clientRoot, "data"));
            string selectedRuntime = runtime ?? Runtime;
            File.WriteAllText(Path.Combine(clientRoot,
                ProductTargetRuntime.ClientExecutable(selectedRuntime)), contents);
            File.WriteAllBytes(Path.Combine(clientRoot, "data", "content.bin"),
                [0, 1, 2, 255]);
            string manifestPath = Path.Combine(releaseRoot,
                ProductReleaseManifest.FileName);
            ProductReleaseManifest.Create(new ReleaseManifestOptions(clientRoot,
                selectedRuntime, $"1.0.{sequence}", sequence, 1, manifestPath));
            string rawSignaturePath = Sign(manifestPath, "release-raw.sig");
            string signaturePath = Path.Combine(releaseRoot,
                ProductReleaseSignature.FileName);
            ProductReleaseSignature.Create(new ReleaseSignatureOptions(manifestPath,
                PublicKeyPath, rawSignaturePath, signaturePath));
            string packagePath = Path.Combine(_rootPath,
                packageName ?? $"release-{sequence}.zip");
            ProductReleasePackage.Create(new ReleasePackageOptions(clientRoot, manifestPath,
                signaturePath, PublicKeyPath, packagePath));
            return new ReleaseFiles(clientRoot, manifestPath, signaturePath, packagePath);
        }

        public string Sign(string path, string fileName)
        {
            string signaturePath = Path.Combine(Path.GetDirectoryName(path)!, fileName);
            File.WriteAllBytes(signaturePath, _publisher.SignData(File.ReadAllBytes(path),
                HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
            return signaturePath;
        }

        public void Dispose()
        {
            _publisher.Dispose();
            try
            {
                Directory.Delete(_rootPath, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed record ReleaseFiles(
        string ClientRoot,
        string ManifestPath,
        string SignaturePath,
        string PackagePath);
}
