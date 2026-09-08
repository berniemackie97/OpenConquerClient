namespace OpenConquer.Product.Tool;

internal sealed record LocalProductBuildResult(
    string ProductPath,
    ulong ReleaseSequence,
    string ReleaseVersion,
    string TargetRuntime
);
