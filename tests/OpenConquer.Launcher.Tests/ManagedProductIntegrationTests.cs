using System.Security.Cryptography;
using OpenConquer.Launcher.Installation;
using OpenConquer.Product.Tool;

namespace OpenConquer.Launcher.Tests;

public sealed class ManagedProductIntegrationTests
{
    [Fact]
    public async Task InstallationCheck_RecoversAfterTrustedProductCompositionWithoutRestart()
    {
        using TemporaryDirectory temporary = new();

        string launcherPublish = temporary.CreateDirectory("launcher");

        string clientPublish = temporary.CreateDirectory("client");

        string productRoot = Path.Combine(temporary.RootPath, "managed");

        File.WriteAllText(Path.Combine(launcherPublish, "OpenConquer.Launcher"), "launcher");

        TestRelease release = CreateTrustedRelease(
            temporary,
            launcherPublish,
            clientPublish,
            productRoot
        );

        await using LauncherApplication application = new(
            new ManagedInstallationResolver(productRoot, release.TrustedKeys)
        );

        await application.StartAsync();

        Assert.Equal(
            ManagedInstallationIssue.ManifestMissing,
            Assert.IsType<LauncherState.InstallationUnavailable>(application.State).Issue
        );

        ManagedProductStager.Stage(release.StageOptions);

        await application.RetryInstallationAsync();

        LauncherState.InstallationResolved resolvedState =
            Assert.IsType<LauncherState.InstallationResolved>(application.State);

        Assert.Equal(Path.GetFullPath(productRoot), resolvedState.Installation.RootPath);

        Assert.Equal(release.ReleaseVersion, resolvedState.Installation.Release.Version);

        Assert.Equal(release.ReleaseSequence, resolvedState.Installation.Release.Sequence);
    }

    [Fact]
    public async Task StagedTrustedManagedProductIsAcceptedByLauncherResolver()
    {
        using TemporaryDirectory temporary = new();

        string launcherPublishPath = temporary.CreateDirectory("launcher");

        string clientPublishPath = temporary.CreateDirectory("client");

        string managedProductPath = Path.Combine(temporary.RootPath, "managed");

        File.WriteAllText(Path.Combine(launcherPublishPath, "OpenConquer.Launcher"), "launcher");

        TestRelease release = CreateTrustedRelease(
            temporary,
            launcherPublishPath,
            clientPublishPath,
            managedProductPath
        );

        ManagedProductStager.Stage(release.StageOptions);

        ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
            managedProductPath,
            release.TrustedKeys
        ).ResolveAsync(CancellationToken.None);

        ManagedInstallationResolution.Resolved resolved =
            Assert.IsType<ManagedInstallationResolution.Resolved>(resolution);

        ManagedInstallation installation = resolved.Installation;

        Assert.Equal(Path.GetFullPath(managedProductPath), installation.RootPath);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(managedProductPath, ManagedProductDescriptor.ClientRoot)),
            installation.ClientRootPath
        );

        Assert.Equal(
            Path.GetFullPath(Path.Combine(managedProductPath, ManagedProductDescriptor.FileName)),
            installation.ManifestPath
        );

        Assert.Equal(
            Path.GetFullPath(Path.Combine(managedProductPath, ProductReleaseManifest.FileName)),
            installation.ReleaseManifestPath
        );

        Assert.Equal(
            Path.GetFullPath(Path.Combine(managedProductPath, ProductReleaseSignature.FileName)),
            installation.ReleaseSignaturePath
        );

        Assert.Equal(
            Path.GetFullPath(
                Path.Combine(
                    managedProductPath,
                    ManagedProductDescriptor.ClientRoot,
                    release.ClientExecutable
                )
            ),
            installation.ClientExecutablePath
        );

        Assert.Equal(release.ReleaseSequence, installation.Release.Sequence);

        Assert.Equal(release.ReleaseVersion, installation.Release.Version);

        Assert.Equal(release.TargetRuntime, installation.Release.TargetRuntime);
    }

    [Fact]
    public async Task StagedProductSignedByUntrustedPublisherIsRejected()
    {
        using TemporaryDirectory temporary = new();

        string launcherPublishPath = temporary.CreateDirectory("launcher");

        string clientPublishPath = temporary.CreateDirectory("client");

        string managedProductPath = Path.Combine(temporary.RootPath, "managed");

        File.WriteAllText(Path.Combine(launcherPublishPath, "OpenConquer.Launcher"), "launcher");

        TestRelease release = CreateTrustedRelease(
            temporary,
            launcherPublishPath,
            clientPublishPath,
            managedProductPath
        );

        ManagedProductStager.Stage(release.StageOptions);

        using ECDsa unrelatedPublisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        TrustedReleaseKeys unrelatedTrust = TrustedReleaseKeys.Create(
            unrelatedPublisher.ExportSubjectPublicKeyInfo()
        );

        ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
            managedProductPath,
            unrelatedTrust
        ).ResolveAsync(CancellationToken.None);

        ManagedInstallationResolution.Rejected rejected =
            Assert.IsType<ManagedInstallationResolution.Rejected>(resolution);

        Assert.Equal(ManagedInstallationIssue.ReleaseSignatureInvalid, rejected.Issue);
    }

    private static TestRelease CreateTrustedRelease(
        TemporaryDirectory temporary,
        string launcherPublishPath,
        string clientPublishPath,
        string outputRootPath
    )
    {
        string targetRuntime =
            ReleaseTargetRuntime.Current
            ?? throw new PlatformNotSupportedException(
                "Launcher integration tests require a supported OpenConquer target runtime."
            );

        string clientExecutable = ReleaseTargetRuntime.ClientExecutable(targetRuntime);

        File.WriteAllText(Path.Combine(clientPublishPath, clientExecutable), "client");

        const ulong releaseSequence = 42;
        const string releaseVersion = "integration-test";

        string releaseMetadataRoot = temporary.CreateDirectory($"release-{Guid.NewGuid():N}");

        string releaseManifestPath = Path.Combine(
            releaseMetadataRoot,
            ProductReleaseManifest.FileName
        );

        string publicKeyPath = Path.Combine(releaseMetadataRoot, "publisher-public-key.der");

        string rawSignaturePath = Path.Combine(releaseMetadataRoot, "release-signature.der");

        string releaseSignaturePath = Path.Combine(
            releaseMetadataRoot,
            ProductReleaseSignature.FileName
        );

        ProductReleaseManifest.Create(
            new ReleaseManifestOptions(
                clientPublishPath,
                targetRuntime,
                releaseVersion,
                releaseSequence,
                ManagedReleaseManifest.CurrentLauncherVersion,
                releaseManifestPath
            )
        );

        byte[] releaseManifest = File.ReadAllBytes(releaseManifestPath);

        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] publicKey = publisher.ExportSubjectPublicKeyInfo();

        byte[] signature = publisher.SignData(
            releaseManifest,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence
        );

        File.WriteAllBytes(publicKeyPath, publicKey);

        File.WriteAllBytes(rawSignaturePath, signature);

        ProductReleaseSignature.Create(
            new ReleaseSignatureOptions(
                releaseManifestPath,
                publicKeyPath,
                rawSignaturePath,
                releaseSignaturePath
            )
        );

        TrustedReleaseKeys trustedKeys = TrustedReleaseKeys.Create(publicKey);

        ProductStageOptions stageOptions = new(
            launcherPublishPath,
            clientPublishPath,
            releaseManifestPath,
            releaseSignaturePath,
            outputRootPath
        );

        return new TestRelease(
            stageOptions,
            trustedKeys,
            clientExecutable,
            targetRuntime,
            releaseSequence,
            releaseVersion
        );
    }

    private sealed record TestRelease(
        ProductStageOptions StageOptions,
        TrustedReleaseKeys TrustedKeys,
        string ClientExecutable,
        string TargetRuntime,
        ulong ReleaseSequence,
        string ReleaseVersion
    );

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"openconquer-managed-product-{Guid.NewGuid():N}"
        );

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(_path);
        }

        public string RootPath => _path;

        public string CreateDirectory(string name)
        {
            string path = Path.Combine(_path, name);

            Directory.CreateDirectory(path);

            return path;
        }

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
