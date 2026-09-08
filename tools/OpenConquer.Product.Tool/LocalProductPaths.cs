namespace OpenConquer.Product.Tool;

/// <summary>Canonical repository-local paths used to compose a development product.</summary>
internal sealed record LocalProductPaths(
    string RootPath,
    string ClientPublishPath,
    string LauncherPublishPath,
    string ReleasePath,
    string ProductPath,
    string ReleaseManifestPath,
    string PublicKeyPath,
    string RawSignaturePath,
    string ReleaseSignaturePath,
    string ReleaseTrustPath
)
{
    public static LocalProductPaths Create(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        string normalizedRepositoryRoot = Path.GetFullPath(repositoryRoot);
        string rootPath = Path.Combine(normalizedRepositoryRoot, "artifacts", "local-product");
        string releasePath = Path.Combine(rootPath, "release");

        return new LocalProductPaths(
            rootPath,
            Path.Combine(rootPath, "client-publish"),
            Path.Combine(rootPath, "launcher-publish"),
            releasePath,
            Path.Combine(rootPath, "product"),
            Path.Combine(releasePath, ProductReleaseManifest.FileName),
            Path.Combine(releasePath, "publisher-public.der"),
            Path.Combine(releasePath, "openconquer.release.der"),
            Path.Combine(releasePath, ProductReleaseSignature.FileName),
            Path.Combine(releasePath, ProductReleaseTrust.FileName)
        );
    }
}
