namespace OpenConquer.Product.Tool;

internal sealed record ReleaseCatalogOptions(
    IReadOnlyList<string> ReleasePackagePaths,
    Uri PackageBaseUri,
    IReadOnlyList<string> PublicKeyPaths,
    DateTimeOffset ExpiresAt,
    string OutputPath) : ProductToolOptions;
