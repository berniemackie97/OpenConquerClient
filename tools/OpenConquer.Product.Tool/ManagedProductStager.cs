namespace OpenConquer.Product.Tool;

internal static class ManagedProductStager
{
    public static void Stage(ProductStageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string launcherPublishPath = RequireDirectory(options.LauncherPublishPath, nameof(options.LauncherPublishPath));
        string clientPublishPath = RequireDirectory(options.ClientPublishPath, nameof(options.ClientPublishPath));
        string outputRootPath = NormalizePath(options.OutputRootPath, nameof(options.OutputRootPath));

        RejectOverlappingRoots(launcherPublishPath, clientPublishPath, outputRootPath);

        EnsureOutputDirectoryDoesNotExist(outputRootPath);

        string? parentPath = Path.GetDirectoryName(outputRootPath);

        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new InvalidOperationException("The product output must have a parent directory.");
        }

        Directory.CreateDirectory(parentPath);

        string stagingRootPath = outputRootPath + $".staging-{Guid.NewGuid():N}";

        bool activated = false;

        try
        {
            Directory.CreateDirectory(stagingRootPath);

            CopyTree(launcherPublishPath, stagingRootPath);

            RejectReservedProductEntries(stagingRootPath);

            string clientDestinationPath = Path.Combine(stagingRootPath, ManagedProductDescriptor.ClientRoot);

            CopyTree(clientPublishPath, clientDestinationPath);

            if (!Directory.EnumerateFileSystemEntries(clientDestinationPath).Any())
            {
                throw new InvalidDataException("The staged client component is empty.");
            }

            ManagedProductDescriptor.Write(stagingRootPath);

            Directory.Move(stagingRootPath, outputRootPath);

            activated = true;
        }
        finally
        {
            if (!activated && Directory.Exists(stagingRootPath))
            {
                TryDeleteFailedStagingRoot(stagingRootPath);
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

    private static void RejectReservedProductEntries(string productRoot)
    {
        string descriptorPath = Path.Combine(productRoot, ManagedProductDescriptor.FileName);

        if (File.Exists(descriptorPath) || Directory.Exists(descriptorPath))
        {
            throw new InvalidDataException($"The launcher publish must not contain '{ManagedProductDescriptor.FileName}'; product composition owns that descriptor.");
        }

        string clientComponentPath = Path.Combine(productRoot, ManagedProductDescriptor.ClientRoot);

        if (File.Exists(clientComponentPath) || Directory.Exists(clientComponentPath))
        {
            throw new InvalidDataException($"The launcher publish must not contain the reserved '{ManagedProductDescriptor.ClientRoot}' component.");
        }
    }

    private static bool AreRelated(string first, string second)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        string firstWithSeparator = first.EndsWith(Path.DirectorySeparatorChar) || first.EndsWith(Path.AltDirectorySeparatorChar)
            ? first
            : first + Path.DirectorySeparatorChar;

        string secondWithSeparator = second.EndsWith(Path.DirectorySeparatorChar) || second.EndsWith(Path.AltDirectorySeparatorChar)
            ? second
            : second + Path.DirectorySeparatorChar;

        return string.Equals(first, second, comparison) || first.StartsWith(secondWithSeparator, comparison) || second.StartsWith(firstWithSeparator, comparison);
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
            // Preserve compatibility with a future target where Unix mode APIs
            // are unavailable despite using this staging implementation.
        }
    }

    private static void TryDeleteFailedStagingRoot(string stagingRootPath)
    {
        try
        {
            Directory.Delete(stagingRootPath, recursive: true);
        }
        catch (IOException)
        {
            // Cleanup must not replace the primary staging failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup must not replace the primary staging failure.
        }
    }
}
