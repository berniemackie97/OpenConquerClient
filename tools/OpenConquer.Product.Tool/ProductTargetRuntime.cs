namespace OpenConquer.Product.Tool;

internal static class ProductTargetRuntime
{
    public static bool IsSupported(string? value)
    {
        return value is "win-x64" or "win-arm64" or "osx-x64" or "osx-arm64" or "linux-x64" or "linux-arm64";
    }

    public static string ClientExecutable(string targetRuntime)
    {
        if (!IsSupported(targetRuntime))
        {
            throw new ArgumentException("The target runtime is not supported.", nameof(targetRuntime));
        }

        return targetRuntime.StartsWith("win-", StringComparison.Ordinal)
            ? "OpenConquer.Client.exe"
            : "OpenConquer.Client";
    }
}
