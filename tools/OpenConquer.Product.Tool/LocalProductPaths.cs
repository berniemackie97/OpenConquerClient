namespace OpenConquer.Product.Tool;

/// <summary>Canonical repository-local paths used to compose one development product candidate.</summary>
internal sealed record LocalProductPaths(string RootPath, string WorkRootPath, string WorkspacePath, string ClientPublishPath,
    string LauncherPublishPath, string ReleasePath, string CandidateProductPath, string ProductPath, string PreviousProductPath,
    string ReleaseManifestPath, string PublicKeyPath, string RawSignaturePath, string ReleaseSignaturePath, string ReleaseTrustPath)
{
    public static LocalProductPaths Create(string repositoryRoot, Guid workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("The local-product workspace identifier must not be empty.", nameof(workspaceId));
        }

        string normalizedRepositoryRoot = Path.GetFullPath(repositoryRoot);
        string rootPath = Path.Combine(normalizedRepositoryRoot, "artifacts", "local-product");
        string workRootPath = Path.Combine(rootPath, "work");
        string workspacePath = Path.Combine(workRootPath, workspaceId.ToString("N"));
        string releasePath = Path.Combine(workspacePath, "release");

        return new LocalProductPaths(rootPath, workRootPath, workspacePath,
            Path.Combine(workspacePath, "client-publish"), Path.Combine(workspacePath, "launcher-publish"),
            releasePath, Path.Combine(workspacePath, "product"), Path.Combine(rootPath, "product"),
            Path.Combine(rootPath, "product.previous"), Path.Combine(releasePath, ProductReleaseManifest.FileName),
            Path.Combine(releasePath, "publisher-public.der"), Path.Combine(releasePath, "openconquer.release.der"),
            Path.Combine(releasePath, ProductReleaseSignature.FileName), Path.Combine(releasePath, ProductReleaseTrust.FileName));
    }
}
