namespace OpenConquer.Product.Tool;

internal sealed record ReleaseManifestOptions(
    string ClientPublishPath,
    string TargetRuntime,
    string ReleaseVersion,
    ulong ReleaseSequence,
    int MinimumLauncherVersion,
    string OutputPath) : ProductToolOptions;
