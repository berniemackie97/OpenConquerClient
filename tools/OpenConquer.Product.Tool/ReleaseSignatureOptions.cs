namespace OpenConquer.Product.Tool;

internal sealed record ReleaseSignatureOptions(
    string ReleaseManifestPath,
    string PublicKeyPath,
    string SignaturePath,
    string OutputPath) : ProductToolOptions;
