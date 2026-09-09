namespace OpenConquer.Product.Tool;

internal sealed record ReleasePackageOptions(
    string ClientPublishPath,
    string ReleaseManifestPath,
    string ReleaseSignaturePath,
    string PublicKeyPath,
    string OutputPath) : ProductToolOptions;
