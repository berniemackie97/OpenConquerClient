namespace OpenConquer.Product.Tool;

/// <summary>Atomically promotes a verified local-product candidate while preserving rollback safety.</summary>
internal static class LocalProductActivator
{
    public const string LockFileName = ".activation.lock";

    public static void Activate(LocalProductPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        string rootPath = ProductStagingPathGuard.RequireDirectory(
            paths.RootPath,
            nameof(paths.RootPath)
        );
        string workspacePath = ProductStagingPathGuard.RequireDirectory(
            paths.WorkspacePath,
            nameof(paths.WorkspacePath)
        );
        string candidatePath = ProductStagingPathGuard.RequireDirectory(
            paths.CandidateProductPath,
            nameof(paths.CandidateProductPath)
        );
        string productPath = ProductStagingPathGuard.NormalizePath(
            paths.ProductPath,
            nameof(paths.ProductPath)
        );
        string previousProductPath = ProductStagingPathGuard.NormalizePath(
            paths.PreviousProductPath,
            nameof(paths.PreviousProductPath)
        );

        ValidateCanonicalRelationships(
            rootPath,
            workspacePath,
            candidatePath,
            productPath,
            previousProductPath
        );

        using FileStream activationLock = AcquireLock(rootPath);

        ProductReleaseManifest candidateManifest = ValidateCandidate(candidatePath);

        bool currentProductExists = EntryExists(productPath);

        if (currentProductExists)
        {
            string currentProductPath = ProductStagingPathGuard.RequireDirectory(
                productPath,
                "active local product"
            );
            ProductReleaseManifest currentManifest = ProductReleaseManifest.Read(
                Path.Combine(currentProductPath, ProductReleaseManifest.FileName)
            );

            if (candidateManifest.ReleaseSequence <= currentManifest.ReleaseSequence)
            {
                throw new InvalidOperationException(
                    $"Local-product activation would roll back release sequence {currentManifest.ReleaseSequence} to {candidateManifest.ReleaseSequence}."
                );
            }
        }

        DeletePreviousProduct(previousProductPath);

        bool previousProductCreated = false;

        try
        {
            if (currentProductExists)
            {
                Directory.Move(productPath, previousProductPath);
                previousProductCreated = true;
            }

            Directory.Move(candidatePath, productPath);

            _ = ProductStagingPathGuard.RequireDirectory(productPath, "activated local product");
        }
        catch (Exception activationException)
            when (activationException is IOException or UnauthorizedAccessException)
        {
            if (previousProductCreated && !EntryExists(productPath))
            {
                try
                {
                    Directory.Move(previousProductPath, productPath);
                }
                catch (Exception rollbackException)
                    when (rollbackException is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException(
                        "Local-product activation failed and the previous product could not be restored automatically.",
                        new AggregateException(activationException, rollbackException)
                    );
                }
            }

            throw new InvalidOperationException(
                "Local-product activation failed; the previous product remains available when restoration was possible.",
                activationException
            );
        }
    }

    private static ProductReleaseManifest ValidateCandidate(string candidatePath)
    {
        _ = ProductStagingPathGuard.RequireRegularFile(
            Path.Combine(candidatePath, ManagedProductDescriptor.FileName),
            "local-product installation descriptor"
        );

        string manifestPath = ProductStagingPathGuard.RequireRegularFile(
            Path.Combine(candidatePath, ProductReleaseManifest.FileName),
            "local-product release manifest"
        );

        string signaturePath = ProductStagingPathGuard.RequireRegularFile(
            Path.Combine(candidatePath, ProductReleaseSignature.FileName),
            "local-product release signature"
        );

        string clientPath = ProductStagingPathGuard.RequireDirectory(
            Path.Combine(candidatePath, ManagedProductDescriptor.ClientRoot),
            "local-product client component"
        );

        ProductReleaseManifest manifest = ProductReleaseManifest.Read(manifestPath);

        ProductReleaseSignature.ValidateEnvelope(signaturePath);
        ProductReleaseManifest.VerifyClient(clientPath, manifest);

        return manifest;
    }

    private static void ValidateCanonicalRelationships(
        string rootPath,
        string workspacePath,
        string candidatePath,
        string productPath,
        string previousProductPath
    )
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (
            !string.Equals(
                Path.GetDirectoryName(workspacePath),
                Path.Combine(rootPath, "work"),
                comparison
            )
            || !string.Equals(Path.GetDirectoryName(candidatePath), workspacePath, comparison)
            || !string.Equals(Path.GetDirectoryName(productPath), rootPath, comparison)
            || !string.Equals(Path.GetDirectoryName(previousProductPath), rootPath, comparison)
        )
        {
            throw new InvalidOperationException(
                "The local-product activation paths do not match the canonical workspace layout."
            );
        }

        ProductStagingPathGuard.RejectPathWithinRoot(
            candidatePath,
            productPath,
            "The candidate and active local-product paths must not overlap."
        );

        ProductStagingPathGuard.RejectPathWithinRoot(
            candidatePath,
            previousProductPath,
            "The candidate and previous local-product paths must not overlap."
        );

        ProductStagingPathGuard.RejectPathWithinRoot(
            productPath,
            previousProductPath,
            "The active and previous local-product paths must not overlap."
        );
    }

    private static FileStream AcquireLock(string rootPath)
    {
        string lockPath = Path.Combine(rootPath, LockFileName);

        RejectUnsupportedEntry(lockPath, "local-product activation lock");

        try
        {
            FileStream stream = new(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None
            );

            try
            {
                RejectUnsupportedEntry(lockPath, "local-product activation lock");
                return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                "Another local-product activation is already in progress.",
                exception
            );
        }
    }

    private static void DeletePreviousProduct(string previousProductPath)
    {
        if (!EntryExists(previousProductPath))
        {
            return;
        }

        string validatedPreviousProductPath = ProductStagingPathGuard.RequireDirectory(
            previousProductPath,
            "previous local product"
        );

        Directory.Delete(validatedPreviousProductPath, recursive: true);

        if (EntryExists(previousProductPath))
        {
            throw new IOException(
                "The previous local product could not be removed before activation."
            );
        }
    }

    private static void RejectUnsupportedEntry(string path, string description)
    {
        FileInfo file = new(path);
        file.Refresh();

        if (
            file.LinkTarget is not null
            || file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0
        )
        {
            throw new InvalidDataException(
                $"Linked paths are not allowed for the {description}: '{path}'."
            );
        }

        if (file.Exists)
        {
            return;
        }

        DirectoryInfo directory = new(path);
        directory.Refresh();

        if (
            directory.LinkTarget is not null
            || directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0
        )
        {
            throw new InvalidDataException(
                $"Linked paths are not allowed for the {description}: '{path}'."
            );
        }

        if (directory.Exists)
        {
            throw new InvalidDataException(
                $"The {description} path must not be a directory: '{path}'."
            );
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
}
