namespace OpenConquer.Product.Tool.Tests;

public sealed class LocalProductPathsTests
{
    private static readonly Guid s_workspaceId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Fact]
    public void CreateUsesCanonicalPerRunWorkspaceLayout()
    {
        string repositoryRoot = Path.Combine(Path.GetTempPath(), "openconquer-local-product-layout");

        LocalProductPaths paths = LocalProductPaths.Create(repositoryRoot, s_workspaceId);

        string expectedRoot = Path.GetFullPath(Path.Combine(repositoryRoot, "artifacts", "local-product"));
        string expectedWorkRoot = Path.Combine(expectedRoot, "work");
        string expectedWorkspace = Path.Combine(expectedWorkRoot, "00112233445566778899aabbccddeeff");
        string expectedRelease = Path.Combine(expectedWorkspace, "release");

        Assert.Equal(expectedRoot, paths.RootPath);
        Assert.Equal(expectedWorkRoot, paths.WorkRootPath);
        Assert.Equal(expectedWorkspace, paths.WorkspacePath);
        Assert.Equal(Path.Combine(expectedWorkspace, "client-publish"), paths.ClientPublishPath);
        Assert.Equal(Path.Combine(expectedWorkspace, "launcher-publish"), paths.LauncherPublishPath);
        Assert.Equal(expectedRelease, paths.ReleasePath);
        Assert.Equal(Path.Combine(expectedWorkspace, "product"), paths.CandidateProductPath);
        Assert.Equal(Path.Combine(expectedRoot, "product"), paths.ProductPath);
        Assert.Equal(Path.Combine(expectedRoot, "product.previous"), paths.PreviousProductPath);
        Assert.Equal(Path.Combine(expectedRelease, ProductReleaseManifest.FileName), paths.ReleaseManifestPath);
        Assert.Equal(Path.Combine(expectedRelease, "publisher-public.der"), paths.PublicKeyPath);
        Assert.Equal(Path.Combine(expectedRelease, "openconquer.release.der"), paths.RawSignaturePath);
        Assert.Equal(Path.Combine(expectedRelease, ProductReleaseSignature.FileName), paths.ReleaseSignaturePath);
        Assert.Equal(Path.Combine(expectedRelease, ProductReleaseTrust.FileName), paths.ReleaseTrustPath);
    }

    [Fact]
    public void CreateUsesIsolatedWorkspaceForEachRun()
    {
        string repositoryRoot = Path.Combine(Path.GetTempPath(), "openconquer-local-product-isolation");

        LocalProductPaths first = LocalProductPaths.Create(repositoryRoot, Guid.Parse("11111111-1111-1111-1111-111111111111"));
        LocalProductPaths second = LocalProductPaths.Create(repositoryRoot, Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.NotEqual(first.WorkspacePath, second.WorkspacePath);
        Assert.NotEqual(first.ClientPublishPath, second.ClientPublishPath);
        Assert.NotEqual(first.LauncherPublishPath, second.LauncherPublishPath);
        Assert.NotEqual(first.CandidateProductPath, second.CandidateProductPath);

        Assert.Equal(first.RootPath, second.RootPath);
        Assert.Equal(first.WorkRootPath, second.WorkRootPath);
        Assert.Equal(first.ProductPath, second.ProductPath);
        Assert.Equal(first.PreviousProductPath, second.PreviousProductPath);
    }

    [Fact]
    public void CreateNormalizesRepositoryRoot()
    {
        string repositoryRoot = Path.Combine(Path.GetTempPath(), "openconquer-local-product-normalization", "nested", "..");

        LocalProductPaths paths = LocalProductPaths.Create(repositoryRoot, s_workspaceId);

        Assert.Equal(Path.Combine(Path.GetFullPath(repositoryRoot), "artifacts", "local-product"), paths.RootPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsMissingRepositoryRoot(string? repositoryRoot)
    {
        Assert.ThrowsAny<ArgumentException>(() => LocalProductPaths.Create(repositoryRoot!, s_workspaceId));
    }

    [Fact]
    public void CreateRejectsEmptyWorkspaceIdentifier()
    {
        Assert.Throws<ArgumentException>(() => LocalProductPaths.Create(Path.GetTempPath(), Guid.Empty));
    }
}
