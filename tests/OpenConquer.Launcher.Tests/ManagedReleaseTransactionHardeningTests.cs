using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests;

public sealed partial class ManagedReleaseTransactionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstallGenerationRejectsAuthenticatedSourceLengthChangeBeforeStaging(
        bool growSource
    )
    {
        using TestProduct product = new();
        TestRelease candidate = product.CreateRelease(1, "authenticated");
        Directory.CreateDirectory(product.ProductRoot);

        ManagedReleaseStore store = CreateStore(product);
        StoredReleaseReadResult.Accepted accepted = Assert.IsType<StoredReleaseReadResult.Accepted>(
            await store.ReadCandidateAsync(candidate.RootPath, CancellationToken.None)
        );
        string releasesRoot = store.EnsureReleasesRoot();
        string releaseId = ManagedReleaseId.Create(
            accepted.Release.Manifest.ReleaseSequence,
            accepted.Release.ManifestBytes
        );

        if (growSource)
        {
            File.AppendAllText(candidate.ClientExecutablePath, new string('x', 1024 * 1024));
        }
        else
        {
            File.WriteAllText(candidate.ClientExecutablePath, string.Empty);
        }

        await Assert.ThrowsAsync<IOException>(() =>
            store.InstallGenerationAsync(
                accepted.Release,
                releasesRoot,
                releaseId,
                CancellationToken.None
            )
        );

        Assert.False(Directory.Exists(Path.Combine(releasesRoot, releaseId)));
        Assert.Empty(
            Directory.EnumerateDirectories(
                releasesRoot,
                ".staging-*",
                SearchOption.TopDirectoryOnly
            )
        );
    }

    [Fact]
    public async Task ApplyRejectsCandidateThroughProductAncestorAlias()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);

        string candidateRoot = Path.Combine(product.ProductRoot, "candidate");
        Directory.CreateDirectory(candidateRoot);

        string aliasRoot = Path.Combine(
            Path.GetDirectoryName(product.ProductRoot)!,
            "product-alias"
        );
        Directory.CreateSymbolicLink(aliasRoot, product.ProductRoot);

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                product.Transaction.ApplyAsync(
                    Path.Combine(aliasRoot, "candidate"),
                    ManagedReleaseChangeKind.Update,
                    CancellationToken.None
                )
            );
        }
        finally
        {
            Directory.Delete(aliasRoot);
        }
    }

    [Fact]
    public async Task ApplyClassifiesDanglingCandidateAncestorLink()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TestProduct product = new();
        TestRelease initial = product.CreateRelease(1, "one");
        product.StageInitialProduct(initial);

        string parent = Path.GetDirectoryName(product.ProductRoot)!;
        string aliasRoot = Path.Combine(parent, "dangling-candidate-alias");
        string missingTarget = Path.Combine(parent, "missing-candidate-target");
        Directory.CreateSymbolicLink(aliasRoot, missingTarget);

        try
        {
            ManagedReleaseTransactionResult.Rejected rejected =
                Assert.IsType<ManagedReleaseTransactionResult.Rejected>(
                    await product.Transaction.ApplyAsync(
                        Path.Combine(aliasRoot, "candidate"),
                        ManagedReleaseChangeKind.Update,
                        CancellationToken.None));

            Assert.Equal(ManagedReleaseChangeIssue.LinkedPath, rejected.Issue);
            Assert.Null(rejected.IntegrityIssue);
        }
        finally
        {
            File.Delete(aliasRoot);
        }
    }

    [Fact]
    public void TransactionLockUsesPrivateUnixMode()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TestProduct product = new();
        Directory.CreateDirectory(product.ProductRoot);
        ManagedReleaseStore store = CreateStore(product);

        using FileStream transactionLock = store.AcquireLock();

        UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(
            expected,
            File.GetUnixFileMode(
                Path.Combine(product.ProductRoot, ManagedReleaseTransaction.LockFileName)
            )
        );
    }

    private static ManagedReleaseStore CreateStore(TestProduct product) =>
        new(
            product.ProductRoot,
            new ManagedReleaseVerifier(product.TrustedKeys),
            ManagedReleaseTransaction.LockFileName
        );
}
