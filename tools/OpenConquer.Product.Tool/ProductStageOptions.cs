namespace OpenConquer.Product.Tool;

internal sealed record ProductStageOptions(
    string LauncherPublishPath,
    string ClientPublishPath,
    string ReleaseManifestPath,
    string ReleaseSignaturePath,
    string OutputRootPath) : ProductToolOptions;
