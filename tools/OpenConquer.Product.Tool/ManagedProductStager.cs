namespace OpenConquer.Product.Tool;

internal static class ManagedProductStager
{
    private const string CopySentinelPrefix = ".openconquer-copy-guard-";

    public static void Stage(ProductStageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string launcherPublishPath = ProductStagingPathGuard.RequireDirectory(options.LauncherPublishPath, nameof(options.LauncherPublishPath));
        string clientPublishPath = ProductStagingPathGuard.RequireDirectory(options.ClientPublishPath, nameof(options.ClientPublishPath));
        string releaseManifestPath = ProductStagingPathGuard.RequireRegularFile(options.ReleaseManifestPath, nameof(options.ReleaseManifestPath));
        string releaseSignaturePath = ProductStagingPathGuard.RequireRegularFile(options.ReleaseSignaturePath, nameof(options.ReleaseSignaturePath));

        RejectReleaseMetadataWithinPublishRoots(launcherPublishPath, clientPublishPath, releaseManifestPath, releaseSignaturePath);

        _ = ProductReleaseManifest.Read(releaseManifestPath);
        ProductReleaseSignature.ValidateEnvelope(releaseSignaturePath);

        string outputRootPath = ProductStagingPathGuard.NormalizePath(options.OutputRootPath, nameof(options.OutputRootPath));

        ProductStagingPathGuard.RejectOverlappingRoots(launcherPublishPath, clientPublishPath, outputRootPath);
        ProductStagingPathGuard.PrepareOutputParent(outputRootPath);

        string stagingRootPath = outputRootPath + $".staging-{Guid.NewGuid():N}";

        ProductStagingPathGuard.RejectOverlappingRoots(launcherPublishPath, clientPublishPath, stagingRootPath);

        EnsureStagingRootDoesNotExist(stagingRootPath);

        string copySentinelFileName = CopySentinelPrefix + Guid.NewGuid().ToString("N");

        bool activated = false;

        try
        {
            Directory.CreateDirectory(stagingRootPath);

            _ = ProductStagingPathGuard.RequireDirectory(stagingRootPath, "product staging root");

            CopyTree(launcherPublishPath, stagingRootPath, copySentinelFileName);

            RejectReservedProductEntries(stagingRootPath);

            string clientDestinationPath = Path.Combine(stagingRootPath, ManagedProductDescriptor.ClientRoot);

            CopyTree(clientPublishPath, clientDestinationPath, copySentinelFileName);

            if (!Directory.EnumerateFileSystemEntries(clientDestinationPath).Any())
            {
                throw new InvalidDataException("The staged client component is empty.");
            }

            string stagedReleaseManifestPath = Path.Combine(stagingRootPath, ProductReleaseManifest.FileName);
            string stagedReleaseSignaturePath = Path.Combine(stagingRootPath, ProductReleaseSignature.FileName);

            CopyReleaseMetadata(releaseManifestPath, stagedReleaseManifestPath, "release manifest");
            CopyReleaseMetadata(releaseSignaturePath, stagedReleaseSignaturePath, "release signature");

            ProductReleaseManifest stagedManifest = ProductReleaseManifest.Read(stagedReleaseManifestPath);

            ProductReleaseSignature.ValidateEnvelope(stagedReleaseSignaturePath);

            ProductReleaseManifest.VerifyClient(clientDestinationPath, stagedManifest);

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

    private static void RejectReleaseMetadataWithinPublishRoots(string launcherPublishPath, string clientPublishPath, string releaseManifestPath, string releaseSignaturePath)
    {
        ProductStagingPathGuard.RejectPathWithinRoot(launcherPublishPath, releaseManifestPath, "The release manifest must remain outside the launcher publish.");
        ProductStagingPathGuard.RejectPathWithinRoot(clientPublishPath, releaseManifestPath, "The release manifest must remain outside the client publish.");
        ProductStagingPathGuard.RejectPathWithinRoot(launcherPublishPath, releaseSignaturePath, "The release signature must remain outside the launcher publish.");
        ProductStagingPathGuard.RejectPathWithinRoot(clientPublishPath, releaseSignaturePath, "The release signature must remain outside the client publish.");
    }

    private static void RejectReservedProductEntries(string productRoot)
    {
        RejectReservedProductEntry(productRoot, ManagedProductDescriptor.FileName, "product composition owns that descriptor");
        RejectReservedProductEntry(productRoot, ManagedProductDescriptor.ClientRoot, "product composition owns the managed client component");
        RejectReservedProductEntry(productRoot, ProductReleaseManifest.FileName, "product composition owns the release manifest");
        RejectReservedProductEntry(productRoot, ProductReleaseSignature.FileName, "product composition owns the release signature");
    }

    private static void RejectReservedProductEntry(string productRoot, string entryName, string reason)
    {
        string entryPath = Path.Combine(productRoot, entryName);

        if (File.Exists(entryPath) || Directory.Exists(entryPath) || EntryExists(entryPath))
        {
            throw new InvalidDataException($"The launcher publish must not contain reserved entry '{entryName}'; {reason}.");
        }
    }

    private static void CopyReleaseMetadata(string sourcePath, string destinationPath, string description)
    {
        string validatedSourcePath = ProductStagingPathGuard.RequireRegularFile(sourcePath, description);

        if (File.Exists(destinationPath) || Directory.Exists(destinationPath) || EntryExists(destinationPath))
        {
            throw new InvalidOperationException($"The staged {description} destination already exists.");
        }

        File.Copy(validatedSourcePath, destinationPath, overwrite: false);

        _ = ProductStagingPathGuard.RequireRegularFile(destinationPath, $"staged {description}");
    }

    private static void EnsureStagingRootDoesNotExist(string stagingRootPath)
    {
        if (EntryExists(stagingRootPath))
        {
            throw new InvalidOperationException($"The temporary staging path '{stagingRootPath}' already exists.");
        }
    }

    private static bool EntryExists(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            return true;
        }

        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static void CopyTree(string sourceRoot, string destinationRoot, string sentinelFileName)
    {
        ThrowIfCopySentinelIsPresent(sourceRoot, sentinelFileName);

        Directory.CreateDirectory(destinationRoot);

        string sentinelPath = Path.Combine(destinationRoot, sentinelFileName);

        CreateCopySentinel(sentinelPath);

        bool copyCompleted = false;

        try
        {
            ThrowIfCopySentinelIsPresent(sourceRoot, sentinelFileName);

            foreach (FileSystemInfo entry in new DirectoryInfo(sourceRoot).EnumerateFileSystemInfos())
            {
                entry.Refresh();

                if (string.Equals(entry.Name, sentinelFileName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Product staging detected a filesystem alias that would copy the staging tree into itself.");
                }

                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0 || entry.LinkTarget is not null)
                {
                    throw new InvalidDataException($"Linked paths are not allowed in product staging: '{entry.FullName}'.");
                }

                string destinationPath = Path.Combine(destinationRoot, entry.Name);

                if (entry is DirectoryInfo)
                {
                    CopyTree(entry.FullName, destinationPath, sentinelFileName);

                    continue;
                }

                if (entry is not FileInfo || (entry.Attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
                {
                    throw new InvalidDataException($"Unsupported filesystem entry in product staging: '{entry.FullName}'.");
                }

                File.Copy(entry.FullName, destinationPath, overwrite: false);

                PreserveUnixMode(entry.FullName, destinationPath);
            }

            copyCompleted = true;
        }
        finally
        {
            if (copyCompleted)
            {
                File.Delete(sentinelPath);
            }
            else
            {
                TryDeleteCopySentinel(sentinelPath);
            }
        }
    }

    private static void ThrowIfCopySentinelIsPresent(string sourceRoot, string sentinelFileName)
    {
        string sentinelPath = Path.Combine(sourceRoot, sentinelFileName);

        if (EntryExists(sentinelPath))
        {
            throw new InvalidOperationException("Product staging detected a filesystem alias that would copy the staging tree into itself.");
        }
    }

    private static void CreateCopySentinel(string sentinelPath)
    {
        using FileStream stream = new(sentinelPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
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
        }
    }

    private static void TryDeleteCopySentinel(string sentinelPath)
    {
        try
        {
            File.Delete(sentinelPath);
        }
        catch (IOException)
        {
            // Preserve the copy failure that initiated cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the copy failure that initiated cleanup.
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
