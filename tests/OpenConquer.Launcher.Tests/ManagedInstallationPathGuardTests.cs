using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests;

public sealed class ManagedInstallationPathGuardTests
{
    [Fact]
    public void PathsOverlapRecognizesSamePath()
    {
        string root = CreateTestRoot();

        try
        {
            Directory.CreateDirectory(root);

            Assert.True(ManagedInstallationPathGuard.PathsOverlap(root, root));
        }
        finally
        {
            TryDeleteTree(root);
        }
    }

    [Fact]
    public void PathsOverlapUsesConservativePortableCaseComparison()
    {
        string root = CreateTestRoot();
        string productRoot = Path.Combine(root, "Product");
        string candidateRoot = Path.Combine(root, "product", "candidate");

        try
        {
            Directory.CreateDirectory(productRoot);

            Assert.True(ManagedInstallationPathGuard.PathsOverlap(productRoot, candidateRoot));
        }
        finally
        {
            TryDeleteTree(root);
        }
    }

    [Fact]
    public void PathsOverlapRejectsCandidateThroughAncestorLink()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = CreateTestRoot();
        string productRoot = Path.Combine(root, "product");
        string candidateRoot = Path.Combine(productRoot, "candidate");
        string aliasRoot = Path.Combine(root, "alias");

        try
        {
            Directory.CreateDirectory(candidateRoot);
            Directory.CreateSymbolicLink(aliasRoot, productRoot);

            Assert.True(
                ManagedInstallationPathGuard.PathsOverlap(
                    productRoot,
                    Path.Combine(aliasRoot, "candidate")
                )
            );
        }
        finally
        {
            TryDeleteLink(aliasRoot);
            TryDeleteTree(root);
        }
    }

    [Fact]
    public void PathsOverlapAllowsUnrelatedRoots()
    {
        string root = CreateTestRoot();
        string productRoot = Path.Combine(root, "product");
        string candidateRoot = Path.Combine(root, "candidate");

        try
        {
            Directory.CreateDirectory(productRoot);
            Directory.CreateDirectory(candidateRoot);

            Assert.False(ManagedInstallationPathGuard.PathsOverlap(productRoot, candidateRoot));
        }
        finally
        {
            TryDeleteTree(root);
        }
    }

    private static string CreateTestRoot() =>
        Path.Combine(
            Path.GetTempPath(),
            "OpenConquer.Launcher.Tests",
            Guid.NewGuid().ToString("N")
        );

    private static void TryDeleteLink(string path)
    {
        try
        {
            Directory.Delete(path);
        }
        catch (DirectoryNotFoundException) { }
    }

    private static void TryDeleteTree(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (DirectoryNotFoundException) { }
    }
}
