namespace OpenConquer.Product.Tool;

internal enum ProductHostPlatform
{
    Windows,
    MacOS,
    Linux,
}

/// <summary>Canonical user-scoped location of the local-development publisher identity.</summary>
internal sealed record DevelopmentPublisherIdentityPaths(string RootPath, string PrivateKeyPath)
{
    public const string PrivateKeyFileName = "publisher-private.pk8";

    public static DevelopmentPublisherIdentityPaths CreateCurrent()
    {
        ProductHostPlatform platform = OperatingSystem.IsWindows()
            ? ProductHostPlatform.Windows
            : OperatingSystem.IsMacOS()
                ? ProductHostPlatform.MacOS
                : OperatingSystem.IsLinux()
                    ? ProductHostPlatform.Linux
                    : throw new PlatformNotSupportedException("Local-development publishing is supported only on Windows, macOS, and Linux.");

        return Create(platform, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"));
    }

    internal static DevelopmentPublisherIdentityPaths Create(ProductHostPlatform platform, string userProfilePath, string? localApplicationDataPath, string? xdgConfigHome)
    {
        string basePath = platform switch
        {
            ProductHostPlatform.Windows => RequireAbsolutePath(localApplicationDataPath, "The Windows local application-data directory is unavailable."),
            ProductHostPlatform.MacOS => Path.Combine(RequireAbsolutePath(userProfilePath, "The macOS user-profile directory is unavailable."), "Library", "Application Support"),
            ProductHostPlatform.Linux => LinuxConfigRoot(userProfilePath, xdgConfigHome),
            _ => throw new ArgumentOutOfRangeException(nameof(platform)),
        };

        string rootPath = Path.GetFullPath(Path.Combine(basePath, "OpenConquer", "Development"));

        return new DevelopmentPublisherIdentityPaths(rootPath, Path.Combine(rootPath, PrivateKeyFileName));
    }

    private static string LinuxConfigRoot(string userProfilePath, string? xdgConfigHome)
    {
        if (!string.IsNullOrWhiteSpace(xdgConfigHome) && Path.IsPathFullyQualified(xdgConfigHome))
        {
            return Path.GetFullPath(xdgConfigHome);
        }

        return Path.Combine(RequireAbsolutePath(userProfilePath, "The Linux user-profile directory is unavailable."), ".config");
    }

    private static string RequireAbsolutePath(string? path, string message)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException(message);
        }

        return Path.GetFullPath(path);
    }
}
