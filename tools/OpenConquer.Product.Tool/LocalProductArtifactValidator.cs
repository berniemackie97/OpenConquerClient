namespace OpenConquer.Product.Tool;

/// <summary>Validates local-development publish and managed-product artifact boundaries.</summary>
internal static class LocalProductArtifactValidator
{
    private const int MaximumValidationEntryCount = 32 * 1024;
    private const string LegacyServerFileName = "Server.dat";
    private const string RetailContentPath = "content/retail-5517";

    private static readonly HashSet<string> s_prohibitedLauncherRootEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        ManagedProductDescriptor.FileName,
        ProductReleaseManifest.FileName,
        ProductReleaseSignature.FileName,
        ProductReleaseTrust.FileName,
        ManagedProductDescriptor.ReleasesRoot,
        ManagedProductDescriptor.ClientRoot,
        LocalProductPaths.PublicKeyFileName,
        LocalProductPaths.RawSignatureFileName,
        DevelopmentPublisherIdentityPaths.PrivateKeyFileName,
        DevelopmentReleaseSequence.FileName,
        DevelopmentReleaseSequence.LockFileName,
        DevelopmentProductBuildLock.FileName,
        LocalProductActivator.LockFileName,
    };

    private static readonly HashSet<string> s_forbiddenManagedProductFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ProductReleaseTrust.FileName,
        LocalProductPaths.PublicKeyFileName,
        LocalProductPaths.RawSignatureFileName,
        DevelopmentPublisherIdentityPaths.PrivateKeyFileName,
        DevelopmentReleaseSequence.FileName,
        DevelopmentReleaseSequence.LockFileName,
        DevelopmentProductBuildLock.FileName,
        LocalProductActivator.LockFileName,
    };

    public static void ValidateClientPublish(string clientPublishPath)
    {
        string clientRoot = ProductStagingPathGuard.RequireDirectory(clientPublishPath, "local-development client publish");

        foreach (FileSystemInfo entry in EnumerateTreeEntries(clientRoot, "published client"))
        {
            if (entry is FileInfo && string.Equals(entry.Name, LegacyServerFileName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Legacy Server.dat must not be present in the published client: '{entry.FullName}'.");
            }
        }
    }

    public static void ValidateLauncherPublish(string launcherPublishPath, string targetRuntime)
    {
        if (!ProductTargetRuntime.IsSupported(targetRuntime))
        {
            throw new ArgumentException("The target runtime is not supported.", nameof(targetRuntime));
        }

        string launcherRoot = ProductStagingPathGuard.RequireDirectory(launcherPublishPath, "local-development launcher publish");
        string launcherExecutable = targetRuntime.StartsWith("win-", StringComparison.Ordinal) ? "OpenConquer.Launcher.exe" : "OpenConquer.Launcher";

        _ = ProductStagingPathGuard.RequireRegularFile(Path.Combine(launcherRoot, launcherExecutable), "local-development launcher executable");

        foreach (FileSystemInfo entry in EnumerateTreeEntries(launcherRoot, "published launcher"))
        {
            string relativePath = Path.GetRelativePath(launcherRoot, entry.FullName).Replace(Path.DirectorySeparatorChar, '/');

            if (s_prohibitedLauncherRootEntries.Contains(relativePath) || IsWithinRetailContent(relativePath))
            {
                throw new InvalidDataException($"The raw launcher publish contains prohibited product entry '{relativePath}'.");
            }
        }
    }

    public static void ValidateManagedProduct(string productRootPath)
    {
        string productRoot = ProductStagingPathGuard.RequireDirectory(productRootPath, "managed local product");

        foreach (FileSystemInfo entry in EnumerateTreeEntries(productRoot, "managed local product"))
        {
            if (s_forbiddenManagedProductFileNames.Contains(entry.Name))
            {
                throw new InvalidDataException($"The managed local product contains forbidden loose development or publishing metadata: '{entry.FullName}'.");
            }
        }
    }

    private static IEnumerable<FileSystemInfo> EnumerateTreeEntries(string rootPath, string description)
    {
        Stack<DirectoryInfo> pending = new();
        int entryCount = 0;

        pending.Push(new DirectoryInfo(rootPath));

        while (pending.TryPop(out DirectoryInfo? directory))
        {
            directory.Refresh();

            if (!directory.Exists || directory.LinkTarget is not null || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"Linked or unavailable directories are not allowed while validating the {description}: '{directory.FullName}'.");
            }

            foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
            {
                if (++entryCount > MaximumValidationEntryCount)
                {
                    throw new InvalidDataException($"The {description} exceeds the supported validation entry limit.");
                }

                entry.Refresh();

                if (entry.LinkTarget is not null || (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException($"Linked paths are not allowed while validating the {description}: '{entry.FullName}'.");
                }

                if (entry is DirectoryInfo childDirectory)
                {
                    pending.Push(childDirectory);
                }
                else if (entry is not FileInfo || (entry.Attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
                {
                    throw new InvalidDataException($"The {description} contains an unsupported filesystem entry: '{entry.FullName}'.");
                }

                yield return entry;
            }
        }
    }

    private static bool IsWithinRetailContent(string relativePath)
    {
        return string.Equals(relativePath, RetailContentPath, StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith(RetailContentPath + "/", StringComparison.OrdinalIgnoreCase);
    }
}
