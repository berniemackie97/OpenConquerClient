namespace OpenConquer.Product.Tool;

internal sealed record CatalogSignatureOptions(
    string ReleaseCatalogPath,
    string PublicKeyPath,
    string SignaturePath,
    string OutputPath) : ProductToolOptions;
