using System.Runtime.InteropServices;

namespace OpenConquer.Launcher.Installation;

/// <summary>Portable runtime identities accepted by managed OpenConquer releases.</summary>
internal static class ReleaseTargetRuntime
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

            return operatingSystem is null || architecture is null
                ? null
                : $"{operatingSystem}-{architecture}";
        }
    }

    public static bool IsSupported(string value)
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
