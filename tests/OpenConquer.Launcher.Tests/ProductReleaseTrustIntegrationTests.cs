using System.Security.Cryptography;
using OpenConquer.Launcher.Installation;
using OpenConquer.Product.Tool;

namespace OpenConquer.Launcher.Tests;

public sealed class ProductReleaseTrustIntegrationTests
{
    [Fact]
    public void ProductToolTrustDocumentAuthenticatesPublisherThroughLauncherContract()
    {
        using TemporaryDirectory temporary = new();
        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] publicKey = publisher.ExportSubjectPublicKeyInfo();
        string publicKeyPath = Path.Combine(temporary.RootPath, "publisher-public.der");
        string trustPath = Path.Combine(temporary.RootPath, ProductReleaseTrust.FileName);

        File.WriteAllBytes(publicKeyPath, publicKey);

        ProductReleaseTrust.Create(new ReleaseTrustOptions([publicKeyPath], trustPath));

        byte[] trustDocument = File.ReadAllBytes(trustPath);

        Assert.True(TrustedReleaseKeys.TryParse(trustDocument, out TrustedReleaseKeys? trustedKeys));
        Assert.NotNull(trustedKeys);
        Assert.True(trustedKeys.IsConfigured);

        byte[] releaseManifest = "openconquer-release-manifest"u8.ToArray();
        byte[] signature = publisher.SignData(releaseManifest, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        ManagedReleaseSignature releaseSignature = new(TrustedReleaseKeys.GetKeyId(publicKey), signature);

        Assert.True(trustedKeys.Verify(releaseSignature, releaseManifest));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"openconquer-release-trust-integration-{Guid.NewGuid():N}");

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
