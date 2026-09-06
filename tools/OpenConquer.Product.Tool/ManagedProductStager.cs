namespace OpenConquer.Product.Tool;

internal static class ManagedProductStager
{
    private const string CopySentinelPrefix = ".openconquer-copy-guard-";

    public static void Stage(ProductStageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string launcherPublishPath = ProductStagingPathGuard.RequireDirectory(
            options.LauncherPublishPath,
            nameof(options.LauncherPublishPath)
        );

        string clientPublishPath = ProductStagingPathGuard.RequireDirectory(
            options.ClientPublishPath,
            nameof(options.ClientPublishPath)
        );

        string outputRootPath = ProductStagingPathGuard.NormalizePath(
            options.OutputRootPath,
            nameof(options.OutputRootPath)
        );

        ProductStagingPathGuard.RejectOverlappingRoots(
            launcherPublishPath,
            clientPublishPath,
            outputRootPath
        );

        ProductStagingPathGuard.PrepareOutputParent(outputRootPath);

        string stagingRootPath = outputRootPath + $".staging-{Guid.NewGuid():N}";

        /*
         * The staging directory is itself a product root candidate and must
         * satisfy the same source-separation policy as the final output.
         */
        ProductStagingPathGuard.RejectOverlappingRoots(
            launcherPublishPath,
            clientPublishPath,
            stagingRootPath
        );

        EnsureStagingRootDoesNotExist(stagingRootPath);

        string copySentinelFileName = CopySentinelPrefix + Guid.NewGuid().ToString("N");

        bool activated = false;

        try
        {
            Directory.CreateDirectory(stagingRootPath);

            _ = ProductStagingPathGuard.RequireDirectory(stagingRootPath, "product staging root");

            CopyTree(launcherPublishPath, stagingRootPath, copySentinelFileName);

            RejectReservedProductEntries(stagingRootPath);

            string clientDestinationPath = Path.Combine(
                stagingRootPath,
                ManagedProductDescriptor.ClientRoot
            );

            CopyTree(clientPublishPath, clientDestinationPath, copySentinelFileName);

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

    private static void RejectReservedProductEntries(string productRoot)
    {
        string descriptorPath = Path.Combine(productRoot, ManagedProductDescriptor.FileName);

        if (File.Exists(descriptorPath) || Directory.Exists(descriptorPath))
        {
            throw new InvalidDataException(
                $"The launcher publish must not contain '{ManagedProductDescriptor.FileName}'; product composition owns that descriptor."
            );
        }

        string clientComponentPath = Path.Combine(productRoot, ManagedProductDescriptor.ClientRoot);

        if (File.Exists(clientComponentPath) || Directory.Exists(clientComponentPath))
        {
            throw new InvalidDataException(
                $"The launcher publish must not contain the reserved '{ManagedProductDescriptor.ClientRoot}' component."
            );
        }
    }

    private static void EnsureStagingRootDoesNotExist(string stagingRootPath)
    {
        if (File.Exists(stagingRootPath) || Directory.Exists(stagingRootPath))
        {
            throw new InvalidOperationException(
                $"The temporary staging path '{stagingRootPath}' already exists."
            );
        }

        /*
         * Exists() follows links on some platforms and can report false for a
         * dangling link. GetAttributes inspects the lexical entry and gives us
         * an additional collision check before we claim the staging pathname.
         */
        try
        {
            _ = File.GetAttributes(stagingRootPath);
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The temporary staging path '{stagingRootPath}' already exists."
        );
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
            /*
             * This second check occurs after the marker exists in the
             * destination. If sourceRoot and destinationRoot are the same
             * physical directory through an alias that portable path analysis
             * could not identify, the source now exposes our marker and the
             * copy fails before processing its contents.
             */
            ThrowIfCopySentinelIsPresent(sourceRoot, sentinelFileName);

            foreach (
                FileSystemInfo entry in new DirectoryInfo(sourceRoot).EnumerateFileSystemInfos()
            )
            {
                if (string.Equals(entry.Name, sentinelFileName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Product staging detected a filesystem alias that would copy the staging tree into itself."
                    );
                }

                if (
                    (entry.Attributes & FileAttributes.ReparsePoint) != 0
                    || entry.LinkTarget is not null
                )
                {
                    throw new InvalidDataException(
                        $"Linked paths are not allowed in product staging: '{entry.FullName}'."
                    );
                }

                string destinationPath = Path.Combine(destinationRoot, entry.Name);

                if (entry is DirectoryInfo)
                {
                    CopyTree(entry.FullName, destinationPath, sentinelFileName);

                    continue;
                }

                if (entry is not FileInfo)
                {
                    throw new InvalidDataException(
                        $"Unsupported filesystem entry in product staging: '{entry.FullName}'."
                    );
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
                /*
                 * A successful copy may not leave internal staging machinery
                 * in the product. Failure to remove the marker therefore makes
                 * the staging operation fail.
                 */
                File.Delete(sentinelPath);
            }
            else
            {
                /*
                 * When another copy failure already exists, marker cleanup is
                 * best-effort so it cannot replace the primary exception.
                 */
                TryDeleteCopySentinel(sentinelPath);
            }
        }
    }

    private static void ThrowIfCopySentinelIsPresent(string sourceRoot, string sentinelFileName)
    {
        string sentinelPath = Path.Combine(sourceRoot, sentinelFileName);

        if (File.Exists(sentinelPath) || Directory.Exists(sentinelPath))
        {
            throw new InvalidOperationException(
                "Product staging detected a filesystem alias that would copy the staging tree into itself."
            );
        }
    }

    private static void CreateCopySentinel(string sentinelPath)
    {
        using FileStream stream = new(
            sentinelPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None
        );
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
            /*
             * Preserve compatibility with a future target where Unix mode APIs
             * are unavailable despite using this staging implementation.
             */
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
