using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Launcher.Installation;
using OpenConquer.Product.Tool;

namespace OpenConquer.Launcher.Tests;

public sealed class ManagedInstallationResolverTests
{
    [Fact]
    public async Task ResolveAsyncUsesLauncherPackageContextAndTrustedManagedClient()
    {
        using TemporaryDirectory temporary = new();

        TrustedTestRelease release = CreateTrustedInstallation(temporary);

        ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
            temporary.RootPath,
            release.TrustedKeys
        ).ResolveAsync(CancellationToken.None);

        ManagedInstallationResolution.Resolved resolved =
            Assert.IsType<ManagedInstallationResolution.Resolved>(resolution);

        ManagedInstallation installation = resolved.Installation;

        Assert.Equal(Path.GetFullPath(temporary.RootPath), installation.RootPath);

        Assert.Equal(Path.GetFullPath(release.ClientRootPath), installation.ClientRootPath);

        Assert.Equal(
            Path.GetFullPath(
                Path.Combine(temporary.RootPath, ManagedInstallationManifest.FileName)
            ),
            installation.ManifestPath
        );

        Assert.Equal(
            Path.GetFullPath(release.ReleaseManifestPath),
            installation.ReleaseManifestPath
        );

        Assert.Equal(
            Path.GetFullPath(release.ReleaseSignaturePath),
            installation.ReleaseSignaturePath
        );

        Assert.Equal(
            Path.GetFullPath(release.ClientExecutablePath),
            installation.ClientExecutablePath
        );

        Assert.Equal(release.ReleaseSequence, installation.Release.Sequence);

        Assert.Equal(release.ReleaseVersion, installation.Release.Version);

        Assert.Equal(release.TargetRuntime, installation.Release.TargetRuntime);
    }

    [Fact]
    public async Task ResolveAsyncRejectsMissingManagedManifest()
    {
        using TemporaryDirectory temporary = new();

        ManagedInstallationResolution resolution = await CreateResolverWithUnavailableTrust(
                temporary.RootPath
            )
            .ResolveAsync(CancellationToken.None);

        Assert.Equal(
            new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ManifestMissing),
            resolution
        );
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"productId\":\"Other\",\"clientRoot\":\"client\"}")]
    [InlineData("{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"clientRoot\":\"../client\"}")]
    [InlineData(
        "{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"clientRoot\":\"/tmp/client\"}"
    )]
    [InlineData(
        "{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"clientRoot\":\"client\",\"unexpected\":true}"
    )]
    [InlineData(
        "{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"clientRoot\":\"client\",\"clientRoot\":\"other\"}"
    )]
    [InlineData(
        "{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"clientRoot\":\"components/game\"}"
    )]
    public async Task ResolveAsyncRejectsManifestValuesOutsideManagedContract(string manifest)
    {
        using TemporaryDirectory temporary = new();

        File.WriteAllText(
            Path.Combine(temporary.RootPath, ManagedInstallationManifest.FileName),
            manifest
        );

        ManagedInstallationResolution resolution = await CreateResolverWithUnavailableTrust(
                temporary.RootPath
            )
            .ResolveAsync(CancellationToken.None);

        Assert.Equal(
            new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ManifestInvalid),
            resolution
        );
    }

    [Fact]
    public async Task ResolveAsyncRejectsLinkedClientComponent()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string target = temporary.CreateDirectory("real-client");

        string linked = Path.Combine(temporary.RootPath, "client");

        Directory.CreateSymbolicLink(linked, target);

        WriteInstallationManifest(temporary.RootPath);

        ManagedInstallationResolution resolution = await CreateResolverWithUnavailableTrust(
                temporary.RootPath
            )
            .ResolveAsync(CancellationToken.None);

        Assert.Equal(
            new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.LinkedPath),
            resolution
        );
    }

    [Fact]
    public async Task ResolveAsyncReportsUnsupportedFutureManifest()
    {
        using TemporaryDirectory temporary = new();

        WriteInstallationManifest(
            temporary.RootPath,
            ManagedInstallationManifest.CurrentSchemaVersion + 1
        );

        ManagedInstallationResolution resolution = await CreateResolverWithUnavailableTrust(
                temporary.RootPath
            )
            .ResolveAsync(CancellationToken.None);

        Assert.Equal(
            new ManagedInstallationResolution.Rejected(
                ManagedInstallationIssue.UnsupportedManifest
            ),
            resolution
        );
    }

    [Fact]
    public async Task ResolveAsyncReportsMissingClientComponentWithoutSearchingElsewhere()
    {
        using TemporaryDirectory temporary = new();

        WriteInstallationManifest(temporary.RootPath);

        temporary.CreateDirectory("unrelated-client");

        ManagedInstallationResolution resolution = await CreateResolverWithUnavailableTrust(
                temporary.RootPath
            )
            .ResolveAsync(CancellationToken.None);

        Assert.Equal(
            new ManagedInstallationResolution.Rejected(
                ManagedInstallationIssue.ClientComponentMissing
            ),
            resolution
        );
    }

    [Fact]
    public async Task ResolveAsyncRejectsMissingReleaseMetadata()
    {
        using TemporaryDirectory temporary = new();

        WriteInstallationManifest(temporary.RootPath);

        temporary.CreateDirectory(ManagedInstallationManifest.ExpectedClientRoot);

        ManagedInstallationResolution resolution = await CreateResolverWithUnavailableTrust(
                temporary.RootPath
            )
            .ResolveAsync(CancellationToken.None);

        Assert.Equal(
            new ManagedInstallationResolution.Rejected(
                ManagedInstallationIssue.ReleaseMetadataMissing
            ),
            resolution
        );
    }

    [Fact]
    public async Task ResolveAsyncRejectsUnavailableReleaseAuthority()
    {
        using TemporaryDirectory temporary = new();

        _ = CreateTrustedInstallation(temporary);

        ManagedInstallationResolution resolution = await CreateResolverWithUnavailableTrust(
                temporary.RootPath
            )
            .ResolveAsync(CancellationToken.None);

        Assert.Equal(
            new ManagedInstallationResolution.Rejected(
                ManagedInstallationIssue.ReleaseAuthorityUnavailable
            ),
            resolution
        );
    }

    [Fact]
    public async Task ResolveAsyncRejectsClientMutationAfterReleaseWasSigned()
    {
        using TemporaryDirectory temporary = new();

        TrustedTestRelease release = CreateTrustedInstallation(temporary);

        File.AppendAllText(release.ClientExecutablePath, "-tampered");

        ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
            temporary.RootPath,
            release.TrustedKeys
        ).ResolveAsync(CancellationToken.None);

        Assert.Equal(
            new ManagedInstallationResolution.Rejected(
                ManagedInstallationIssue.ClientIntegrityFailure
            ),
            resolution
        );
    }

    [Fact]
    public async Task ResolveAsyncRejectsReleaseSignedByDifferentPublisher()
    {
        using TemporaryDirectory temporary = new();

        _ = CreateTrustedInstallation(temporary);

        using ECDsa unrelatedPublisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        TrustedReleaseKeys unrelatedTrust = TrustedReleaseKeys.Create(
            unrelatedPublisher.ExportSubjectPublicKeyInfo()
        );

        ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
            temporary.RootPath,
            unrelatedTrust
        ).ResolveAsync(CancellationToken.None);

        Assert.Equal(
            new ManagedInstallationResolution.Rejected(
                ManagedInstallationIssue.ReleaseSignatureInvalid
            ),
            resolution
        );
    }

    [Fact]
    public async Task ResolveAsyncHonorsCancellationBeforeReadingManifest()
    {
        using TemporaryDirectory temporary = new();
        using CancellationTokenSource cancellation = new();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateResolverWithUnavailableTrust(temporary.RootPath).ResolveAsync(cancellation.Token)
        );
    }

    private static TrustedTestRelease CreateTrustedInstallation(TemporaryDirectory temporary)
    {
        string targetRuntime =
            ReleaseTargetRuntime.Current
            ?? throw new PlatformNotSupportedException(
                "Launcher resolver tests require a supported OpenConquer target runtime."
            );

        string clientExecutable = ReleaseTargetRuntime.ClientExecutable(targetRuntime);

        string clientRoot = temporary.CreateDirectory(
            ManagedInstallationManifest.ExpectedClientRoot
        );

        string clientExecutablePath = Path.Combine(clientRoot, clientExecutable);

        File.WriteAllText(clientExecutablePath, "client");

        WriteInstallationManifest(temporary.RootPath);

        const ulong releaseSequence = 7;
        const string releaseVersion = "resolver-test";

        string releaseManifestPath = Path.Combine(
            temporary.RootPath,
            ProductReleaseManifest.FileName
        );

        ProductReleaseManifest.Create(
            new ReleaseManifestOptions(
                clientRoot,
                targetRuntime,
                releaseVersion,
                releaseSequence,
                ManagedReleaseManifest.CurrentLauncherVersion,
                releaseManifestPath
            )
        );

        byte[] manifestBytes = File.ReadAllBytes(releaseManifestPath);

        using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] publicKey = publisher.ExportSubjectPublicKeyInfo();

        byte[] rawSignature = publisher.SignData(
            manifestBytes,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence
        );

        string signingInputRoot = temporary.CreateDirectory($"signing-{Guid.NewGuid():N}");

        string publicKeyPath = Path.Combine(signingInputRoot, "publisher-public-key.der");

        string rawSignaturePath = Path.Combine(signingInputRoot, "release-signature.der");

        string releaseSignaturePath = Path.Combine(
            temporary.RootPath,
            ProductReleaseSignature.FileName
        );

        File.WriteAllBytes(publicKeyPath, publicKey);

        File.WriteAllBytes(rawSignaturePath, rawSignature);

        ProductReleaseSignature.Create(
            new ReleaseSignatureOptions(
                releaseManifestPath,
                publicKeyPath,
                rawSignaturePath,
                releaseSignaturePath
            )
        );

        TrustedReleaseKeys trustedKeys = TrustedReleaseKeys.Create(publicKey);

        return new TrustedTestRelease(
            trustedKeys,
            clientRoot,
            clientExecutablePath,
            releaseManifestPath,
            releaseSignaturePath,
            targetRuntime,
            releaseSequence,
            releaseVersion
        );
    }

    private static ManagedInstallationResolver CreateResolverWithUnavailableTrust(string root)
    {
        TrustedReleaseKeys unavailableTrust = TrustedReleaseKeys.LoadEmbedded(
            typeof(ManagedInstallationResolverTests).Assembly
        );

        Assert.False(unavailableTrust.IsConfigured);

        return new ManagedInstallationResolver(root, unavailableTrust);
    }

    private static void WriteInstallationManifest(
        string root,
        int schemaVersion = ManagedInstallationManifest.CurrentSchemaVersion
    )
    {
        string manifest = JsonSerializer.Serialize(
            new
            {
                schemaVersion,
                productId = ManagedInstallationManifest.ExpectedProductId,
                clientRoot = ManagedInstallationManifest.ExpectedClientRoot,
            }
        );

        File.WriteAllText(Path.Combine(root, ManagedInstallationManifest.FileName), manifest);
    }

    private sealed record TrustedTestRelease(
        TrustedReleaseKeys TrustedKeys,
        string ClientRootPath,
        string ClientExecutablePath,
        string ReleaseManifestPath,
        string ReleaseSignaturePath,
        string TargetRuntime,
        ulong ReleaseSequence,
        string ReleaseVersion
    );

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            "OpenConquer.Launcher.Tests",
            Guid.NewGuid().ToString("N")
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
