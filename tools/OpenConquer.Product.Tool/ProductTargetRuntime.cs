using System.Runtime.InteropServices;

namespace OpenConquer.Product.Tool;

internal static class ProductTargetRuntime
{
    public static string? Current
    {
        get
        {
            string? operatingSystem = OperatingSystem.IsWindows()
                ? "win"
                : OperatingSystem.IsMacOS()
                    ? "osx"
                    : OperatingSystem.IsLinux()
                        ? "linux"
                        : null;

            string? architecture = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                _ => null,
            };

            return operatingSystem is not null && architecture is not null ? $"{operatingSystem}-{architecture}" : null;
        }
    }

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
