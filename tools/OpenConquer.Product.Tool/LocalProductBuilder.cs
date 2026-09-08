namespace OpenConquer.Product.Tool;

/// <summary>Composes and activates a complete managed product for local development.</summary>
internal sealed class LocalProductBuilder
{
    private const int LocalDevelopmentMinimumLauncherVersion = 1;
    private const int MaximumReleaseManifestLength = 8 * 1024 * 1024;
    private const string SolutionPath = "OpenConquer.Client.slnx";
    private const string ContentToolProjectPath = "tools/OpenConquer.Content.Tool/OpenConquer.Content.Tool.csproj";
    private const string ClientProjectPath = "src/OpenConquer.Client/OpenConquer.Client.csproj";
    private const string LauncherProjectPath = "src/OpenConquer.Launcher/OpenConquer.Launcher.csproj";

    private readonly IDotNetProcessRunner _dotNetProcessRunner;

    public LocalProductBuilder(IDotNetProcessRunner dotNetProcessRunner)
    {
        ArgumentNullException.ThrowIfNull(dotNetProcessRunner);

        _dotNetProcessRunner = dotNetProcessRunner;
    }

    public LocalProductBuildResult Build(LocalProductOptions options, DevelopmentPublisherIdentityPaths publisherIdentityPaths)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(publisherIdentityPaths);

        string repositoryRoot = ProductRepositoryLocator.Find(options.WorkingDirectoryPath);
        string targetRuntime = ProductTargetRuntime.Current ?? throw new PlatformNotSupportedException("Local-product composition is not supported on the current operating-system and process-architecture combination.");

        LocalProductPaths paths = LocalProductPaths.Create(repositoryRoot, Guid.NewGuid());

        using FileStream buildLock = DevelopmentProductBuildLock.Acquire(publisherIdentityPaths.RootPath);

        PrepareWorkspace(paths);

        try
        {
            using DevelopmentPublisherIdentity publisher = DevelopmentPublisherIdentity.LoadOrCreate(publisherIdentityPaths);

            RestoreLockedDependencies(repositoryRoot);
            VerifySourceContent(repositoryRoot);
            PublishClient(repositoryRoot, paths);
            VerifyPublishedClientContent(repositoryRoot, paths);
            LocalProductArtifactValidator.ValidateClientPublish(paths.ClientPublishPath);

            ulong historicalReleaseFloor = ReadHistoricalReleaseFloor(paths);
            ulong releaseSequence = DevelopmentReleaseSequence.Reserve(publisherIdentityPaths.RootPath, historicalReleaseFloor);
            string releaseVersion = $"local-{releaseSequence}";

            WriteNewFile(paths.PublicKeyPath, publisher.ExportPublicKey());

            ProductReleaseManifest.Create(new ReleaseManifestOptions(paths.ClientPublishPath, targetRuntime, releaseVersion, releaseSequence,
                LocalDevelopmentMinimumLauncherVersion, paths.ReleaseManifestPath));

            byte[] releaseManifest = ProductReleaseManifest.ReadRegularFile(paths.ReleaseManifestPath, MaximumReleaseManifestLength);
            byte[] rawSignature = publisher.Sign(releaseManifest);

            WriteNewFile(paths.RawSignaturePath, rawSignature);

            ProductReleaseSignature.Create(new ReleaseSignatureOptions(paths.ReleaseManifestPath, paths.PublicKeyPath, paths.RawSignaturePath, paths.ReleaseSignaturePath));
            ProductReleaseTrust.Create(new ReleaseTrustOptions([paths.PublicKeyPath], paths.ReleaseTrustPath));

            PublishLauncher(repositoryRoot, paths);
            LocalProductArtifactValidator.ValidateLauncherPublish(paths.LauncherPublishPath, targetRuntime);

            ManagedProductStager.Stage(new ProductStageOptions(paths.LauncherPublishPath, paths.ClientPublishPath, paths.ReleaseManifestPath,
                paths.ReleaseSignaturePath, paths.CandidateProductPath));

            LocalProductArtifactValidator.ValidateManagedProduct(paths.CandidateProductPath);
            LocalProductActivator.Activate(paths);

            return new LocalProductBuildResult(paths.ProductPath, releaseSequence, releaseVersion, targetRuntime);
        }
        finally
        {
            TryDeleteWorkspace(paths.WorkspacePath);
        }
    }

    private void RestoreLockedDependencies(string repositoryRoot)
    {
        _dotNetProcessRunner.Run(repositoryRoot, ["restore", SolutionPath, "--locked-mode"]);
    }

    private void VerifySourceContent(string repositoryRoot)
    {
        _dotNetProcessRunner.Run(repositoryRoot,
        [
            "run",
            "--project",
            ContentToolProjectPath,
            "--configuration",
            "Release",
            "--no-restore",
            "--",
            "verify-content-set",
            "--content-set",
            Path.Combine(repositoryRoot, "content", "retail-5517"),
        ]);
    }

    private void PublishClient(string repositoryRoot, LocalProductPaths paths)
    {
        _dotNetProcessRunner.Run(repositoryRoot,
        [
            "publish",
            ClientProjectPath,
            "--configuration",
            "Release",
            "--no-restore",
            "--output",
            paths.ClientPublishPath,
        ]);
    }

    private void VerifyPublishedClientContent(string repositoryRoot, LocalProductPaths paths)
    {
        _dotNetProcessRunner.Run(repositoryRoot,
        [
            "run",
            "--project",
            ContentToolProjectPath,
            "--configuration",
            "Release",
            "--no-build",
            "--no-restore",
            "--",
            "verify-content-set",
            "--content-set",
            Path.Combine(paths.ClientPublishPath, "content", "retail-5517"),
        ]);
    }

    private void PublishLauncher(string repositoryRoot, LocalProductPaths paths)
    {
        _dotNetProcessRunner.Run(repositoryRoot,
        [
            "publish",
            LauncherProjectPath,
            "--configuration",
            "Release",
            "--no-restore",
            "--output",
            paths.LauncherPublishPath,
            $"-p:OpenConquerReleaseTrustPath={paths.ReleaseTrustPath}",
        ]);
    }

    private static void PrepareWorkspace(LocalProductPaths paths)
    {
        Directory.CreateDirectory(paths.RootPath);
        _ = ProductStagingPathGuard.RequireDirectory(paths.RootPath, "local-product root");

        Directory.CreateDirectory(paths.WorkRootPath);
        _ = ProductStagingPathGuard.RequireDirectory(paths.WorkRootPath, "local-product work root");

        ProductStagingPathGuard.PrepareOutputParent(paths.WorkspacePath);
        Directory.CreateDirectory(paths.WorkspacePath);
        _ = ProductStagingPathGuard.RequireDirectory(paths.WorkspacePath, "local-product workspace");
    }

    private static ulong ReadHistoricalReleaseFloor(LocalProductPaths paths)
    {
        return Math.Max(ReadReleaseSequenceIfPresent(paths.ProductPath, "active local product"),
            ReadReleaseSequenceIfPresent(paths.PreviousProductPath, "previous local product"));
    }

    private static ulong ReadReleaseSequenceIfPresent(string productPath, string description)
    {
        DirectoryInfo directory = new(productPath);
        directory.Refresh();

        if (directory.LinkTarget is not null || directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"Linked paths are not allowed for the {description}: '{productPath}'.");
        }

        if (directory.Exists)
        {
            return ProductReleaseManifest.Read(Path.Combine(ProductStagingPathGuard.RequireDirectory(productPath, description),
                ProductReleaseManifest.FileName)).ReleaseSequence;
        }

        FileInfo file = new(productPath);
        file.Refresh();

        if (file.LinkTarget is not null || file.Exists)
        {
            throw new InvalidDataException($"The {description} path must be a directory: '{productPath}'.");
        }

        return 0;
    }

    private static void WriteNewFile(string path, ReadOnlySpan<byte> contents)
    {
        ProductStagingPathGuard.PrepareFileOutput(path);

        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        stream.Write(contents);
        stream.Flush(flushToDisk: true);
    }

    private static void TryDeleteWorkspace(string workspacePath)
    {
        try
        {
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
        catch (IOException)
        {
            // Workspace cleanup must not replace the primary build result.
        }
        catch (UnauthorizedAccessException)
        {
            // Workspace cleanup must not replace the primary build result.
        }
    }
}
