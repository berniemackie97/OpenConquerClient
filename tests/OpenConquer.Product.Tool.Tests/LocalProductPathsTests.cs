namespace OpenConquer.Product.Tool.Tests;

public sealed class LocalProductPathsTests
{
    [Fact]
    public void CreateUsesCanonicalRepositoryLocalLayout()
    {
        string repositoryRoot = Path.Combine(
            Path.GetTempPath(),
            "openconquer-local-product-layout"
        );
        LocalProductPaths paths = LocalProductPaths.Create(repositoryRoot);

        string expectedRoot = Path.GetFullPath(
            Path.Combine(repositoryRoot, "artifacts", "local-product")
        );
        string expectedRelease = Path.Combine(expectedRoot, "release");

        Assert.Equal(expectedRoot, paths.RootPath);
        Assert.Equal(Path.Combine(expectedRoot, "client-publish"), paths.ClientPublishPath);
        Assert.Equal(Path.Combine(expectedRoot, "launcher-publish"), paths.LauncherPublishPath);
        Assert.Equal(expectedRelease, paths.ReleasePath);
        Assert.Equal(Path.Combine(expectedRoot, "product"), paths.ProductPath);
        Assert.Equal(
            Path.Combine(expectedRelease, ProductReleaseManifest.FileName),
            paths.ReleaseManifestPath
        );
        Assert.Equal(Path.Combine(expectedRelease, "publisher-public.der"), paths.PublicKeyPath);
        Assert.Equal(
            Path.Combine(expectedRelease, "openconquer.release.der"),
            paths.RawSignaturePath
        );
        Assert.Equal(
            Path.Combine(expectedRelease, ProductReleaseSignature.FileName),
            paths.ReleaseSignaturePath
        );
        Assert.Equal(
            Path.Combine(expectedRelease, ProductReleaseTrust.FileName),
            paths.ReleaseTrustPath
        );
    }

    [Fact]
    public void CreateNormalizesRepositoryRoot()
    {
        string repositoryRoot = Path.Combine(
            Path.GetTempPath(),
            "openconquer-local-product-normalization",
            "nested",
            ".."
        );

        LocalProductPaths paths = LocalProductPaths.Create(repositoryRoot);

        Assert.Equal(
            Path.Combine(Path.GetFullPath(repositoryRoot), "artifacts", "local-product"),
            paths.RootPath
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsMissingRepositoryRoot(string? repositoryRoot)
    {
        Assert.ThrowsAny<ArgumentException>(() => LocalProductPaths.Create(repositoryRoot!));
    }
}
