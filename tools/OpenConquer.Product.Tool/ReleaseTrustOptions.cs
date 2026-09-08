namespace OpenConquer.Product.Tool;

internal sealed record ReleaseTrustOptions(IReadOnlyList<string> PublicKeyPaths, string OutputPath) : ProductToolOptions;
