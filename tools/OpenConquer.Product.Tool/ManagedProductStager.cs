namespace OpenConquer.Product.Tool;

internal static class ManagedProductStager
{
    private const string InstallationDescriptorName = "openconquer.installation.json";

    public static void Stage(ProductStageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string launcherPublishPath = RequireDirectory(options.LauncherPublishPath, nameof(options.LauncherPublishPath));
        string clientPublishPath = RequireDirectory(options.ClientPublishPath, nameof(options.ClientPublishPath));
        string outputRootPath = NormalizePath(options.OutputRootPath, nameof(options.OutputRootPath));

        RejectOverlappingRoots(launcherPublishPath, clientPublishPath, outputRootPath);
        RejectLauncherClientComponent(launcherPublishPath);

        EnsureOutputDirectoryDoesNotExist(outputRootPath);

        string? parentPath = Path.GetDirectoryName(outputRootPath);
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new InvalidOperationException("The product output must have a parent directory.");
        }

        Directory.CreateDirectory(parentPath);
        string stagingRootPath = outputRootPath + $".staging-{Guid.NewGuid():N}";

        try
        {
            Directory.CreateDirectory(stagingRootPath);
            CopyTree(launcherPublishPath, stagingRootPath);

            string clientDestinationPath = Path.Combine(stagingRootPath, "client");
            CopyTree(clientPublishPath, clientDestinationPath);

            string descriptorPath = Path.Combine(stagingRootPath, InstallationDescriptorName);
            if (!File.Exists(descriptorPath))
            {
                throw new InvalidDataException($"The launcher publish is missing '{InstallationDescriptorName}'.");
            }

            if (!Directory.EnumerateFileSystemEntries(clientDestinationPath).Any())
            {
                throw new InvalidDataException("The staged client component is empty.");
            }

            Directory.Move(stagingRootPath, outputRootPath);
        }
        finally
        {
            if (Directory.Exists(stagingRootPath))
            {
                Directory.Delete(stagingRootPath, recursive: true);
            }
        }
    }

    private static string RequireDirectory(string path, string parameterName)
    {
        string normalizedPath = NormalizePath(path, parameterName);
        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"The {parameterName} directory '{normalizedPath}' does not exist.");
        }

        FileAttributes attributes = File.GetAttributes(normalizedPath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"Linked paths are not allowed for product staging roots: '{normalizedPath}'.");
        }

        return normalizedPath;
    }

    private static string NormalizePath(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Product staging paths must be absolute.", parameterName);
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Product staging path is invalid.", parameterName, exception);
        }
        catch (NotSupportedException exception)
        {
            throw new ArgumentException("Product staging path is unsupported.", parameterName, exception);
        }
        catch (PathTooLongException exception)
        {
            throw new ArgumentException("Product staging path is too long.", parameterName, exception);
        }
    }

    private static void RejectOverlappingRoots(string launcherRoot, string clientRoot, string outputRoot)
    {
        if (AreRelated(launcherRoot, clientRoot) || AreRelated(launcherRoot, outputRoot) || AreRelated(clientRoot, outputRoot))
        {
            throw new InvalidOperationException("Launcher, client, and output paths must not overlap.");
        }
    }

    private static void RejectLauncherClientComponent(string launcherRoot)
    {
        string reservedClientPath = Path.Combine(launcherRoot, "client");
        if (File.Exists(reservedClientPath) || Directory.Exists(reservedClientPath))
        {
            throw new InvalidDataException("The launcher publish must not contain the reserved 'client' component directory.");
        }
    }

    private static bool AreRelated(string first, string second)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string firstWithSeparator = first.EndsWith(Path.DirectorySeparatorChar) || first.EndsWith(Path.AltDirectorySeparatorChar)
            ? first
            : first + Path.DirectorySeparatorChar;
        string secondWithSeparator = second.EndsWith(Path.DirectorySeparatorChar) || second.EndsWith(Path.AltDirectorySeparatorChar)
            ? second
            : second + Path.DirectorySeparatorChar;

        return string.Equals(first, second, comparison) ||
            first.StartsWith(secondWithSeparator, comparison) ||
            second.StartsWith(firstWithSeparator, comparison);
    }

    private static void EnsureOutputDirectoryDoesNotExist(string outputRoot)
    {
        if (File.Exists(outputRoot) || Directory.Exists(outputRoot))
        {
            throw new InvalidOperationException($"The output path '{outputRoot}' already exists; staging never replaces an existing product.");
        }
    }

    private static void CopyTree(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (FileSystemInfo entry in new DirectoryInfo(sourceRoot).EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"Linked paths are not allowed in product staging: '{entry.FullName}'.");
            }

            string destinationPath = Path.Combine(destinationRoot, entry.Name);
            if (entry is DirectoryInfo)
            {
                CopyTree(entry.FullName, destinationPath);
                continue;
            }

            if (entry is not FileInfo)
            {
                throw new InvalidDataException($"Unsupported filesystem entry in product staging: '{entry.FullName}'.");
            }

            File.Copy(entry.FullName, destinationPath, overwrite: false);
            PreserveUnixMode(entry.FullName, destinationPath);
        }
    }

    private static void PreserveUnixMode(string sourcePath, string destinationPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(destinationPath, File.GetUnixFileMode(sourcePath));
        }
        catch (PlatformNotSupportedException)
        {
            // The runtime reports Unix mode support on the supported desktop targets. Keep the
            // copy usable on a future target that does not expose the optional API.
        }
    }
}
