using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Launcher.Installation;
using OpenConquer.Product.Tool;

namespace OpenConquer.Launcher.Tests;

public sealed class ManagedReleaseTransactionTests
{
    [Fact]
    public async Task UpdateInstallsNewGenerationAndPreservesVerifiedFallback()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        string initialReleaseId = ReadDescriptor(product.ProductRoot).ActiveRelease!;
        TestRelease update = product.CreateRelease(2, "two");

        ManagedReleaseTransactionResult result = await product.Transaction.ApplyAsync(
            update.RootPath, ManagedReleaseChangeKind.Update, CancellationToken.None);

        ManagedReleaseTransactionResult.Applied applied =
            Assert.IsType<ManagedReleaseTransactionResult.Applied>(result);
        Assert.Equal(2UL, applied.Installation.Release.Sequence);
        Assert.Equal(initialReleaseId, applied.Installation.FallbackReleaseId);
        Assert.NotEqual(initialReleaseId, applied.Installation.ActiveReleaseId);
        Assert.True(Directory.Exists(Path.Combine(product.ProductRoot,
            ManagedInstallationManifest.ReleasesRoot, initialReleaseId)));
        Assert.True(Directory.Exists(update.RootPath));

        ManagedInstallation resolved = await product.ResolveAsync();
        Assert.Equal(2UL, resolved.Release.Sequence);
        Assert.Equal(applied.Installation.ActiveReleaseId, resolved.ActiveReleaseId);
        Assert.Equal(initialReleaseId, resolved.FallbackReleaseId);
    }

    [Fact]
    public async Task RollbackAtomicallySelectsVerifiedFallbackAndRetainsCurrentRelease()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        TestRelease update = product.CreateRelease(2, "two");
        ManagedReleaseTransactionResult.Applied updated =
            Assert.IsType<ManagedReleaseTransactionResult.Applied>(await product.Transaction.ApplyAsync(
                update.RootPath, ManagedReleaseChangeKind.Update, CancellationToken.None));

        ManagedReleaseTransactionResult result = await product.Transaction.RollbackAsync(
            CancellationToken.None);

        ManagedReleaseTransactionResult.Applied rolledBack =
            Assert.IsType<ManagedReleaseTransactionResult.Applied>(result);
        Assert.Equal(1UL, rolledBack.Installation.Release.Sequence);
        Assert.Equal(updated.Installation.ActiveReleaseId, rolledBack.Installation.FallbackReleaseId);
        Assert.Equal(1UL, (await product.ResolveAsync()).Release.Sequence);
    }

    [Fact]
    public async Task RepairReplacesDamagedActiveReleaseWithoutMutatingItInPlace()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(7, "healthy", "release-seven");
        product.StageInitialProduct(initial);
        ManagedInstallation installed = await product.ResolveAsync();
        string originalReleaseId = installed.ActiveReleaseId!;
        File.AppendAllText(installed.ClientExecutablePath, "-damaged");
        TestRelease repair = product.CreateRelease(7, "healthy", "release-seven");

        ManagedReleaseTransactionResult result = await product.Transaction.ApplyAsync(
            repair.RootPath, ManagedReleaseChangeKind.Repair, CancellationToken.None);

        ManagedReleaseTransactionResult.Applied applied =
            Assert.IsType<ManagedReleaseTransactionResult.Applied>(result);
        Assert.NotEqual(originalReleaseId, applied.Installation.ActiveReleaseId);
        Assert.Null(applied.Installation.FallbackReleaseId);
        Assert.EndsWith("-damaged", File.ReadAllText(installed.ClientExecutablePath),
            StringComparison.Ordinal);
        Assert.Equal("healthy", File.ReadAllText(applied.Installation.ClientExecutablePath));
        Assert.Equal(7UL, (await product.ResolveAsync()).Release.Sequence);
    }

    [Fact]
    public async Task UpdateRecoversDamagedActiveReleaseWithoutRecordingItAsFallback()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(3, "three");
        product.StageInitialProduct(initial);
        File.AppendAllText((await product.ResolveAsync()).ClientExecutablePath, "-damaged");
        TestRelease update = product.CreateRelease(4, "four");

        ManagedReleaseTransactionResult.Applied applied =
            Assert.IsType<ManagedReleaseTransactionResult.Applied>(await product.Transaction.ApplyAsync(
                update.RootPath, ManagedReleaseChangeKind.Update, CancellationToken.None));

        Assert.Null(applied.Installation.FallbackReleaseId);
        Assert.Equal(4UL, (await product.ResolveAsync()).Release.Sequence);
    }

    [Fact]
    public async Task UpdateMigratesLegacyLayoutAndMakesItTheVerifiedFallback()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(10, "legacy");
        product.StageLegacyProduct(initial);
        TestRelease update = product.CreateRelease(11, "current");

        ManagedReleaseTransactionResult.Applied applied =
            Assert.IsType<ManagedReleaseTransactionResult.Applied>(await product.Transaction.ApplyAsync(
                update.RootPath, ManagedReleaseChangeKind.Update, CancellationToken.None));

        Assert.NotNull(applied.Installation.FallbackReleaseId);
        Assert.Equal(11UL, (await product.ResolveAsync()).Release.Sequence);
        Assert.Equal(10UL, Assert.IsType<ManagedReleaseTransactionResult.Applied>(
            await product.Transaction.RollbackAsync(CancellationToken.None)).Installation.Release.Sequence);
    }

    [Theory]
    [InlineData(ManagedReleaseChangeKind.Update, 2UL, "same", ManagedReleaseChangeIssue.ReleaseNotNewer)]
    [InlineData(ManagedReleaseChangeKind.Update, 1UL, "older", ManagedReleaseChangeIssue.ReleaseRollbackBlocked)]
    [InlineData(ManagedReleaseChangeKind.Repair, 2UL, "different", ManagedReleaseChangeIssue.RepairReleaseMismatch)]
    [InlineData(ManagedReleaseChangeKind.Repair, 3UL, "newer", ManagedReleaseChangeIssue.RepairReleaseMismatch)]
    public async Task ApplyEnforcesUpdateAndRepairSequencePolicy(
        int kindValue,
        ulong candidateSequence,
        string candidateContents,
        int expectedIssueValue)
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(2, "same");
        product.StageInitialProduct(initial);
        string originalReleaseId = ReadDescriptor(product.ProductRoot).ActiveRelease!;
        TestRelease candidate = product.CreateRelease(candidateSequence, candidateContents);

        ManagedReleaseTransactionResult.Rejected rejected =
            Assert.IsType<ManagedReleaseTransactionResult.Rejected>(await product.Transaction.ApplyAsync(
                candidate.RootPath, (ManagedReleaseChangeKind)kindValue, CancellationToken.None));

        Assert.Equal((ManagedReleaseChangeIssue)expectedIssueValue, rejected.Issue);
        Assert.Equal(originalReleaseId, ReadDescriptor(product.ProductRoot).ActiveRelease);
    }

    [Fact]
    public async Task ApplyRejectsUntrustedCandidateBeforeChangingInstallation()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        string originalReleaseId = ReadDescriptor(product.ProductRoot).ActiveRelease!;
        TestRelease candidate = product.CreateRelease(2, "two");
        File.WriteAllText(candidate.SignaturePath, "{}");

        ManagedReleaseTransactionResult.Rejected rejected =
            Assert.IsType<ManagedReleaseTransactionResult.Rejected>(await product.Transaction.ApplyAsync(
                candidate.RootPath, ManagedReleaseChangeKind.Update, CancellationToken.None));

        Assert.Equal(ManagedReleaseChangeIssue.CandidateRejected, rejected.Issue);
        Assert.Equal(ManagedInstallationIssue.ReleaseSignatureInvalid, rejected.IntegrityIssue);
        Assert.Equal(originalReleaseId, ReadDescriptor(product.ProductRoot).ActiveRelease);
    }

    [Fact]
    public async Task ApplyRejectsCandidateWithUnexpectedReleaseRootEntry()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        TestRelease candidate = product.CreateRelease(2, "two");
        File.WriteAllText(Path.Combine(candidate.RootPath, "unexpected"), "data");

        ManagedReleaseTransactionResult.Rejected rejected =
            Assert.IsType<ManagedReleaseTransactionResult.Rejected>(await product.Transaction.ApplyAsync(
                candidate.RootPath, ManagedReleaseChangeKind.Update, CancellationToken.None));

        Assert.Equal(ManagedReleaseChangeIssue.CandidateRejected, rejected.Issue);
        Assert.Equal(ManagedInstallationIssue.ClientIntegrityFailure, rejected.IntegrityIssue);
    }

    [Fact]
    public async Task ApplyRejectsLinkedCandidateClient()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        TestRelease candidate = product.CreateRelease(2, "two");
        string clientPath = candidate.ClientRootPath;
        string movedClientPath = candidate.RootPath + "-client-target";
        Directory.Move(clientPath, movedClientPath);
        Directory.CreateSymbolicLink(clientPath, movedClientPath);

        ManagedReleaseTransactionResult.Rejected rejected =
            Assert.IsType<ManagedReleaseTransactionResult.Rejected>(await product.Transaction.ApplyAsync(
                candidate.RootPath, ManagedReleaseChangeKind.Update, CancellationToken.None));

        Assert.Equal(ManagedReleaseChangeIssue.CandidateRejected, rejected.Issue);
        Assert.Equal(ManagedInstallationIssue.LinkedPath, rejected.IntegrityIssue);
    }

    [Fact]
    public async Task ApplyReturnsBusyWhileAnotherTransactionOwnsLock()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        TestRelease candidate = product.CreateRelease(2, "two");
        using FileStream heldLock = new(Path.Combine(product.ProductRoot,
            ManagedReleaseTransaction.LockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite,
            FileShare.None);

        ManagedReleaseTransactionResult.Rejected rejected =
            Assert.IsType<ManagedReleaseTransactionResult.Rejected>(await product.Transaction.ApplyAsync(
                candidate.RootPath, ManagedReleaseChangeKind.Update, CancellationToken.None));

        Assert.Equal(ManagedReleaseChangeIssue.OperationAlreadyInProgress, rejected.Issue);
    }

    [Fact]
    public async Task ApplyHonorsCancellationWithoutChangingDescriptor()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        string originalDescriptor = File.ReadAllText(Path.Combine(product.ProductRoot,
            ManagedInstallationManifest.FileName));
        TestRelease candidate = product.CreateRelease(2, "two");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => product.Transaction.ApplyAsync(
            candidate.RootPath, ManagedReleaseChangeKind.Update, cancellation.Token));

        Assert.Equal(originalDescriptor, File.ReadAllText(Path.Combine(product.ProductRoot,
            ManagedInstallationManifest.FileName)));
    }

    [Fact]
    public async Task RollbackRejectsDamagedFallbackWithoutChangingDescriptor()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        TestRelease update = product.CreateRelease(2, "two");
        ManagedReleaseTransactionResult.Applied applied =
            Assert.IsType<ManagedReleaseTransactionResult.Applied>(await product.Transaction.ApplyAsync(
                update.RootPath, ManagedReleaseChangeKind.Update, CancellationToken.None));
        string descriptorBefore = File.ReadAllText(Path.Combine(product.ProductRoot,
            ManagedInstallationManifest.FileName));
        string fallbackExecutable = Path.Combine(product.ProductRoot,
            ManagedInstallationManifest.ReleasesRoot, applied.Installation.FallbackReleaseId!,
            ManagedInstallationManifest.ExpectedClientRoot, product.ClientExecutable);
        File.AppendAllText(fallbackExecutable, "-damaged");

        ManagedReleaseTransactionResult.Rejected rejected =
            Assert.IsType<ManagedReleaseTransactionResult.Rejected>(
                await product.Transaction.RollbackAsync(CancellationToken.None));

        Assert.Equal(ManagedReleaseChangeIssue.FallbackUnavailable, rejected.Issue);
        Assert.Equal(ManagedInstallationIssue.ClientIntegrityFailure, rejected.IntegrityIssue);
        Assert.Equal(descriptorBefore, File.ReadAllText(Path.Combine(product.ProductRoot,
            ManagedInstallationManifest.FileName)));
    }

    [Fact]
    public async Task ResolverRejectsGenerationNameThatDoesNotMatchSignedManifest()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        ManagedInstallationManifest descriptor = ReadDescriptor(product.ProductRoot);
        string wrongId = ManagedReleaseId.Create(2, "wrong-manifest"u8);
        string releasesRoot = Path.Combine(product.ProductRoot,
            ManagedInstallationManifest.ReleasesRoot);
        Directory.Move(Path.Combine(releasesRoot, descriptor.ActiveRelease!),
            Path.Combine(releasesRoot, wrongId));
        await ManagedInstallationManifest.WriteCurrentAsync(Path.Combine(product.ProductRoot,
            ManagedInstallationManifest.FileName), wrongId, fallbackRelease: null,
            CancellationToken.None);

        ManagedInstallationResolution.Rejected rejected =
            Assert.IsType<ManagedInstallationResolution.Rejected>(await new ManagedInstallationResolver(
                product.ProductRoot, product.TrustedKeys).ResolveAsync(CancellationToken.None));

        Assert.Equal(ManagedInstallationIssue.ReleaseIdentityMismatch, rejected.Issue);
    }

    [Fact]
    public async Task InstalledUnixClientModeIsSanitizedAndExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        TestRelease update = product.CreateRelease(2, "two");
        File.SetUnixFileMode(update.ClientExecutablePath, (UnixFileMode)0x1ff);

        ManagedReleaseTransactionResult.Applied applied =
            Assert.IsType<ManagedReleaseTransactionResult.Applied>(await product.Transaction.ApplyAsync(
                update.RootPath, ManagedReleaseChangeKind.Update, CancellationToken.None));

        Assert.Equal((UnixFileMode)0x1ed,
            File.GetUnixFileMode(applied.Installation.ClientExecutablePath));
    }

    [Fact]
    public async Task ResolverRejectsMissingSelectedGeneration()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        ManagedInstallationManifest descriptor = ReadDescriptor(product.ProductRoot);
        string activeRoot = Path.Combine(product.ProductRoot,
            ManagedInstallationManifest.ReleasesRoot, descriptor.ActiveRelease!);
        Directory.Move(activeRoot, activeRoot + "-moved");

        ManagedInstallationResolution.Rejected rejected =
            Assert.IsType<ManagedInstallationResolution.Rejected>(await new ManagedInstallationResolver(
                product.ProductRoot, product.TrustedKeys).ResolveAsync(CancellationToken.None));

        Assert.Equal(ManagedInstallationIssue.ActiveReleaseMissing, rejected.Issue);
    }

    [Fact]
    public async Task ResolverRejectsUnexpectedGenerationRootEntry()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        ManagedInstallation installed = await product.ResolveAsync();
        File.WriteAllText(Path.Combine(installed.ReleaseRootPath, "unexpected"), "data");

        ManagedInstallationResolution.Rejected rejected =
            Assert.IsType<ManagedInstallationResolution.Rejected>(await new ManagedInstallationResolver(
                product.ProductRoot, product.TrustedKeys).ResolveAsync(CancellationToken.None));

        Assert.Equal(ManagedInstallationIssue.ClientIntegrityFailure, rejected.Issue);
    }

    [Fact]
    public async Task ResolverRejectsUnixClientWithoutPortableExecutePermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        ManagedInstallation installed = await product.ResolveAsync();
        File.SetUnixFileMode(installed.ClientExecutablePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);

        ManagedInstallationResolution.Rejected rejected =
            Assert.IsType<ManagedInstallationResolution.Rejected>(await new ManagedInstallationResolver(
                product.ProductRoot, product.TrustedKeys).ResolveAsync(CancellationToken.None));

        Assert.Equal(ManagedInstallationIssue.ClientIntegrityFailure, rejected.Issue);
    }

    [Fact]
    public async Task RollbackWithoutRecordedFallbackIsRejected()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);

        ManagedReleaseTransactionResult.Rejected rejected =
            Assert.IsType<ManagedReleaseTransactionResult.Rejected>(
                await product.Transaction.RollbackAsync(CancellationToken.None));

        Assert.Equal(ManagedReleaseChangeIssue.FallbackUnavailable, rejected.Issue);
        Assert.Null(rejected.IntegrityIssue);
    }

    [Fact]
    public async Task UpdateRejectsCorruptedCurrentReleaseMetadata()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        ManagedInstallation installed = await product.ResolveAsync();
        File.WriteAllText(installed.ReleaseManifestPath, "{}");
        TestRelease update = product.CreateRelease(2, "two");

        ManagedReleaseTransactionResult.Rejected rejected =
            Assert.IsType<ManagedReleaseTransactionResult.Rejected>(await product.Transaction.ApplyAsync(
                update.RootPath, ManagedReleaseChangeKind.Update, CancellationToken.None));

        Assert.Equal(ManagedReleaseChangeIssue.InstallationUnavailable, rejected.Issue);
        Assert.Equal(ManagedInstallationIssue.ReleaseManifestInvalid, rejected.IntegrityIssue);
    }

    [Fact]
    public async Task ApplyRejectsUndefinedChangeKind()
    {
        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);
        TestRelease update = product.CreateRelease(2, "two");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => product.Transaction.ApplyAsync(
            update.RootPath, (ManagedReleaseChangeKind)99, CancellationToken.None));
    }

    [Fact]
    public async Task VerifierRejectsMetadataThatChangedAfterAuthentication()
    {
        using TestProduct product = new();
        TestRelease release = product.CreateRelease(1, "one");
        ReleaseManifestReadResult.Accepted manifest = Assert.IsType<ReleaseManifestReadResult.Accepted>(
            await ManagedReleaseManifest.ReadAsync(release.ManifestPath, CancellationToken.None));
        ReleaseSignatureReadResult.Accepted signature = Assert.IsType<ReleaseSignatureReadResult.Accepted>(
            await ManagedReleaseSignature.ReadAsync(release.SignaturePath, CancellationToken.None));
        File.AppendAllText(release.SignaturePath, Environment.NewLine);

        ManagedReleaseVerification result = await new ManagedReleaseVerifier(product.TrustedKeys)
            .VerifyAsync(release.ClientRootPath, release.ManifestPath, release.SignaturePath,
                manifest.Manifest, manifest.Bytes, signature.Signature, signature.Bytes,
                CancellationToken.None);

        ManagedReleaseVerification.Rejected rejected =
            Assert.IsType<ManagedReleaseVerification.Rejected>(result);
        Assert.Equal(ManagedInstallationIssue.ReadFailure, rejected.Issue);
    }

    private static ManagedInstallationManifest ReadDescriptor(string productRoot)
    {
        ManifestReadResult result = ManagedInstallationManifest.ReadAsync(Path.Combine(productRoot,
            ManagedInstallationManifest.FileName), CancellationToken.None).GetAwaiter().GetResult();
        return Assert.IsType<ManifestReadResult.Accepted>(result).Manifest;
    }

    private sealed class TestProduct : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "OpenConquer.ReleaseTransaction.Tests", Guid.NewGuid().ToString("N"));
        private readonly ECDsa _publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private int _releaseNumber;

        public TestProduct()
        {
            Directory.CreateDirectory(_root);
            ProductRoot = Path.Combine(_root, "product");
            TrustedKeys = TrustedReleaseKeys.Create(_publisher.ExportSubjectPublicKeyInfo());
            Transaction = new ManagedReleaseTransaction(ProductRoot, TrustedKeys);
            TargetRuntime = ReleaseTargetRuntime.Current ?? throw new PlatformNotSupportedException(
                "Release transaction tests require a supported runtime.");
            ClientExecutable = ReleaseTargetRuntime.ClientExecutable(TargetRuntime);
        }

        public string ProductRoot
        {
            get;
        }

        public string TargetRuntime
        {
            get;
        }

        public string ClientExecutable
        {
            get;
        }

        public TrustedReleaseKeys TrustedKeys
        {
            get;
        }

        public ManagedReleaseTransaction Transaction
        {
            get;
        }

        public TestRelease CreateRelease(
            ulong sequence,
            string executableContents,
            string? version = null)
        {
            string releaseRoot = Path.Combine(_root, $"candidate-{++_releaseNumber}");
            string clientRoot = Path.Combine(releaseRoot,
                ManagedInstallationManifest.ExpectedClientRoot);
            Directory.CreateDirectory(clientRoot);
            string executablePath = Path.Combine(clientRoot, ClientExecutable);
            File.WriteAllText(executablePath, executableContents);
            EnsureExecutable(executablePath);

            string manifestPath = Path.Combine(releaseRoot, ManagedReleaseManifest.FileName);
            ProductReleaseManifest.Create(new ReleaseManifestOptions(clientRoot, TargetRuntime,
                version ?? $"release-{sequence}", sequence,
                ManagedReleaseManifest.CurrentLauncherVersion, manifestPath));

            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            byte[] rawSignature = _publisher.SignData(manifestBytes, HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
            string signingRoot = Path.Combine(_root, $"signing-{_releaseNumber}");
            Directory.CreateDirectory(signingRoot);
            string publicKeyPath = Path.Combine(signingRoot, "public.der");
            string rawSignaturePath = Path.Combine(signingRoot, "signature.der");
            File.WriteAllBytes(publicKeyPath, _publisher.ExportSubjectPublicKeyInfo());
            File.WriteAllBytes(rawSignaturePath, rawSignature);
            string signaturePath = Path.Combine(releaseRoot, ManagedReleaseSignature.FileName);
            ProductReleaseSignature.Create(new ReleaseSignatureOptions(manifestPath, publicKeyPath,
                rawSignaturePath, signaturePath));

            return new TestRelease(releaseRoot, clientRoot, executablePath, manifestPath,
                signaturePath);
        }

        public void StageInitialProduct(TestRelease release)
        {
            string launcherRoot = Path.Combine(_root, "launcher");
            Directory.CreateDirectory(launcherRoot);
            File.WriteAllText(Path.Combine(launcherRoot, "OpenConquer.Launcher"), "launcher");
            ManagedProductStager.Stage(new ProductStageOptions(launcherRoot,
                release.ClientRootPath, release.ManifestPath, release.SignaturePath, ProductRoot));
        }

        public void StageLegacyProduct(TestRelease release)
        {
            Directory.CreateDirectory(ProductRoot);
            File.WriteAllText(Path.Combine(ProductRoot, "OpenConquer.Launcher"), "launcher");
            string clientRoot = Path.Combine(ProductRoot,
                ManagedInstallationManifest.ExpectedClientRoot);
            Directory.CreateDirectory(clientRoot);
            File.Copy(release.ClientExecutablePath, Path.Combine(clientRoot, ClientExecutable));
            EnsureExecutable(Path.Combine(clientRoot, ClientExecutable));
            File.Copy(release.ManifestPath,
                Path.Combine(ProductRoot, ManagedReleaseManifest.FileName));
            File.Copy(release.SignaturePath,
                Path.Combine(ProductRoot, ManagedReleaseSignature.FileName));
            File.WriteAllText(Path.Combine(ProductRoot, ManagedInstallationManifest.FileName),
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    productId = ManagedInstallationManifest.ExpectedProductId,
                    clientRoot = ManagedInstallationManifest.ExpectedClientRoot,
                }));
        }

        public async Task<ManagedInstallation> ResolveAsync()
        {
            ManagedInstallationResolution result = await new ManagedInstallationResolver(
                ProductRoot, TrustedKeys).ResolveAsync(CancellationToken.None);
            return Assert.IsType<ManagedInstallationResolution.Resolved>(result).Installation;
        }

        public void Dispose()
        {
            _publisher.Dispose();
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void EnsureExecutable(string path)
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, (UnixFileMode)0x1ed);
            }
        }
    }

    private sealed record TestRelease(
        string RootPath,
        string ClientRootPath,
        string ClientExecutablePath,
        string ManifestPath,
        string SignaturePath);
}
