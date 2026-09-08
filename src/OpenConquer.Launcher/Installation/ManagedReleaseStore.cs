using System.Buffers;

namespace OpenConquer.Launcher.Installation;

/// <summary>Owns safe release-tree reads, copies, generation paths, and the mutation lock.</summary>
internal sealed class ManagedReleaseStore
{
    private const string StagingPrefix = ".staging-";
    private const int CopyBufferSize = 128 * 1024;
    private static readonly UnixFileMode s_dataFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    private static readonly UnixFileMode s_executableFileMode = s_dataFileMode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
    private static readonly UnixFileMode s_directoryMode = s_executableFileMode;
    private static readonly UnixFileMode s_privateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private static readonly UnixFileMode s_lockFileMode = s_privateFileMode;

    private readonly ManagedReleaseVerifier _releaseVerifier;
    private readonly string _lockFileName;

    public ManagedReleaseStore(string productRoot, ManagedReleaseVerifier releaseVerifier, string lockFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productRoot);
        ArgumentNullException.ThrowIfNull(releaseVerifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFileName);

        if (!Path.IsPathFullyQualified(productRoot))
        {
            throw new ArgumentException("The product root must be fully qualified.", nameof(productRoot));
        }

        ProductRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(productRoot));
        _releaseVerifier = releaseVerifier;
        _lockFileName = lockFileName;
    }

    public string ProductRoot
    {
        get;
    }

    public string DescriptorPath => Path.Combine(ProductRoot, ManagedInstallationManifest.FileName);

    public Task<StoredReleaseReadResult> ReadCandidateAsync(string candidateRoot, CancellationToken cancellationToken) => ReadReleaseAsync(candidateRoot, verifyClient: true, cancellationToken);
    public Task<StoredReleaseReadResult> ReadActiveMetadataAsync(ActiveReleaseLocation location, CancellationToken cancellationToken) => ReadReleaseAsync(location.RootPath, verifyClient: false, cancellationToken);

    public async Task<StoredReleaseReadResult> ReadGenerationAsync(string releaseId, CancellationToken cancellationToken)
    {
        StoredReleaseReadResult result = await ReadReleaseAsync(GetGenerationRoot(releaseId), verifyClient: true, cancellationToken).ConfigureAwait(false);
        if (result is StoredReleaseReadResult.Accepted accepted && !ManagedReleaseId.Matches(releaseId, accepted.Release.Manifest.ReleaseSequence, accepted.Release.ManifestBytes))
        {
            return new StoredReleaseReadResult.Rejected(ManagedInstallationIssue.ReleaseIdentityMismatch);
        }

        return result;
    }

    public async Task<bool> IsHealthyGenerationAsync(string releaseId, CancellationToken cancellationToken)
    {
        StoredReleaseReadResult result = await ReadGenerationAsync(releaseId, cancellationToken).ConfigureAwait(false);
        return result is StoredReleaseReadResult.Accepted;
    }

    public async Task<bool> VerifyClientAsync(AuthenticatedRelease release, CancellationToken cancellationToken)
    {
        ManagedReleaseVerification verification = await _releaseVerifier.VerifyAsync(release.ClientRootPath, release.ManifestPath, release.SignaturePath, release.Manifest, release.ManifestBytes, release.Signature, release.SignatureBytes, cancellationToken).ConfigureAwait(false);
        return verification is ManagedReleaseVerification.Verified;
    }

    public async Task<ManagedReleaseGeneration> InstallGenerationAsync(AuthenticatedRelease source, string releasesRoot, string preferredReleaseId, CancellationToken cancellationToken)
    {
        string releaseId = preferredReleaseId;
        string finalRoot = Path.Combine(releasesRoot, releaseId);
        if (EntryExists(finalRoot))
        {
            StoredReleaseReadResult existing = await ReadGenerationAsync(releaseId, cancellationToken).ConfigureAwait(false);
            if (existing is StoredReleaseReadResult.Accepted accepted)
            {
                return new ManagedReleaseGeneration(releaseId, finalRoot, accepted.Release, Created: false);
            }

            do
            {
                releaseId = ManagedReleaseId.Create(source.Manifest.ReleaseSequence, source.ManifestBytes, Guid.NewGuid().ToString("N"));
                finalRoot = Path.Combine(releasesRoot, releaseId);
            }
            while (EntryExists(finalRoot));
        }

        string stagingRoot = Path.Combine(releasesRoot, StagingPrefix + Guid.NewGuid().ToString("N"));
        bool moved = false;
        try
        {
            CreateDirectory(stagingRoot);
            RequireDirectory(stagingRoot);
            SetDirectoryMode(stagingRoot);

            string stagedClientRoot = Path.Combine(stagingRoot, ManagedInstallationManifest.ExpectedClientRoot);
            CreateDirectory(stagedClientRoot);
            RequireDirectory(stagedClientRoot);
            SetDirectoryMode(stagedClientRoot);

            await CopyFileAsync(source.ManifestPath, Path.Combine(stagingRoot, ManagedReleaseManifest.FileName), source.ManifestBytes.LongLength, executable: false, cancellationToken).ConfigureAwait(false);
            await CopyFileAsync(source.SignaturePath, Path.Combine(stagingRoot, ManagedReleaseSignature.FileName), source.SignatureBytes.LongLength, executable: false, cancellationToken).ConfigureAwait(false);

            foreach (ManagedReleaseFile file in source.Manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string sourcePath = ReleasePackagePath.Combine(source.ClientRootPath, file.Path);
                string destinationPath = ReleasePackagePath.Combine(stagedClientRoot, file.Path);
                EnsureDestinationParent(stagedClientRoot, destinationPath);
                await CopyFileAsync(sourcePath, destinationPath, file.Length, string.Equals(file.Path, source.Manifest.ClientExecutable, StringComparison.Ordinal), cancellationToken).ConfigureAwait(false);
            }

            StoredReleaseReadResult stagedResult = await ReadReleaseAsync(stagingRoot, verifyClient: true, cancellationToken).ConfigureAwait(false);
            if (stagedResult is not StoredReleaseReadResult.Accepted staged || !ManagedReleaseId.Matches(releaseId, staged.Release.Manifest.ReleaseSequence, staged.Release.ManifestBytes))
            {
                ManagedInstallationIssue detail = stagedResult is StoredReleaseReadResult.Rejected rejected
                        ? rejected.Issue
                        : ManagedInstallationIssue.ReleaseIdentityMismatch;
                throw new StagedReleaseRejectedException(detail);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(stagingRoot, finalRoot);
            moved = true;
            return new ManagedReleaseGeneration(releaseId, finalRoot, staged.Release, Created: true);
        }
        finally
        {
            if (!moved)
            {
                TryDeleteTree(stagingRoot);
            }
        }
    }

    public ManagedInstallation CreateInstallation(string activeReleaseId, string? fallbackReleaseId, string releaseRoot, AuthenticatedRelease release)
    {
        string clientRoot = Path.Combine(releaseRoot, ManagedInstallationManifest.ExpectedClientRoot);
        return ManagedInstallation.Create(ProductRoot, releaseRoot, clientRoot, DescriptorPath, Path.Combine(releaseRoot, ManagedReleaseManifest.FileName),
            Path.Combine(releaseRoot, ManagedReleaseSignature.FileName), ReleasePackagePath.Combine(clientRoot, release.Manifest.ClientExecutable),
            release.Identity, activeReleaseId, fallbackReleaseId);
    }

    public ActiveReleaseLocation GetActiveReleaseLocation(ManagedInstallationManifest descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.SchemaVersion == 1)
        {
            return new ActiveReleaseLocation(ProductRoot, ReleaseId: null);
        }

        string releaseId = descriptor.ActiveRelease!;
        return new ActiveReleaseLocation(GetGenerationRoot(releaseId), releaseId);
    }

    public string GetGenerationRoot(string releaseId) => Path.Combine(ProductRoot, ManagedInstallationManifest.ReleasesRoot, releaseId);

    public string EnsureReleasesRoot()
    {
        RequireDirectory(ProductRoot);
        string releasesRoot = Path.Combine(ProductRoot, ManagedInstallationManifest.ReleasesRoot);
        if (!EntryExists(releasesRoot))
        {
            CreateDirectory(releasesRoot);
        }

        RequireDirectory(releasesRoot);
        SetDirectoryMode(releasesRoot);
        return releasesRoot;
    }

    public FileStream AcquireLock()
    {
        RequireDirectory(ProductRoot);
        string lockPath = Path.Combine(ProductRoot, _lockFileName);
        if (EntryExists(lockPath))
        {
            RequireRegularFile(lockPath);
        }

        FileStream stream;
        try
        {
            FileStreamOptions options = new()
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
            };

            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = s_lockFileMode;
            }

            stream = new FileStream(lockPath, options);
        }
        catch (IOException exception)
        {
            throw new ManagedReleaseLockUnavailableException(exception);
        }

        try
        {
            RequireRegularFile(lockPath);
            SetFileMode(lockPath, s_lockFileMode);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public static void TryDeleteTree(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
            // Best-effort cleanup must not replace the transaction result.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup must not replace the transaction result.
        }
    }

    private async Task<StoredReleaseReadResult> ReadReleaseAsync(string releaseRoot, bool verifyClient, CancellationToken cancellationToken)
    {
        try
        {
            RequireDirectory(releaseRoot);
            if (verifyClient && !ManagedReleaseDirectory.HasExpectedShape(releaseRoot))
            {
                return new StoredReleaseReadResult.Rejected(ManagedInstallationIssue.ClientIntegrityFailure);
            }

            string clientRoot = Path.Combine(releaseRoot, ManagedInstallationManifest.ExpectedClientRoot);
            if (verifyClient)
            {
                RequireDirectory(clientRoot);
            }

            string manifestPath = Path.Combine(releaseRoot, ManagedReleaseManifest.FileName);
            ReleaseManifestReadResult manifestResult = await ManagedReleaseManifest.ReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            if (manifestResult is ReleaseManifestReadResult.Rejected manifestRejected)
            {
                return new StoredReleaseReadResult.Rejected(manifestRejected.Issue);
            }

            ReleaseManifestReadResult.Accepted acceptedManifest = (ReleaseManifestReadResult.Accepted)manifestResult;
            string signaturePath = Path.Combine(releaseRoot, ManagedReleaseSignature.FileName);
            ReleaseSignatureReadResult signatureResult = await ManagedReleaseSignature.ReadAsync(signaturePath, cancellationToken).ConfigureAwait(false);
            if (signatureResult is ReleaseSignatureReadResult.Rejected signatureRejected)
            {
                return new StoredReleaseReadResult.Rejected(signatureRejected.Issue);
            }

            ReleaseSignatureReadResult.Accepted acceptedSignature = (ReleaseSignatureReadResult.Accepted)signatureResult;
            ManagedReleaseSignature signature = acceptedSignature.Signature;
            ManagedReleaseMetadataVerification metadata = _releaseVerifier.VerifyMetadata(acceptedManifest.Manifest, acceptedManifest.Bytes, signature);
            if (metadata is ManagedReleaseMetadataVerification.Rejected metadataRejected)
            {
                return new StoredReleaseReadResult.Rejected(metadataRejected.Issue);
            }

            ManagedReleaseVerification.Verified? verifiedClient = null;
            if (verifyClient)
            {
                ManagedReleaseVerification clientVerification = await _releaseVerifier.VerifyAsync(clientRoot, manifestPath, signaturePath, acceptedManifest.Manifest, acceptedManifest.Bytes, signature, acceptedSignature.Bytes, cancellationToken).ConfigureAwait(false);
                if (clientVerification is ManagedReleaseVerification.Rejected clientRejected)
                {
                    return new StoredReleaseReadResult.Rejected(clientRejected.Issue);
                }

                verifiedClient = (ManagedReleaseVerification.Verified)clientVerification;

                if (!ManagedReleaseDirectory.HasExpectedShape(releaseRoot))
                {
                    return new StoredReleaseReadResult.Rejected(ManagedInstallationIssue.ClientIntegrityFailure);
                }
            }

            ManagedReleaseIdentity identity = verifiedClient?.Release ?? ((ManagedReleaseMetadataVerification.Verified)metadata).Release;
            return new StoredReleaseReadResult.Accepted(new AuthenticatedRelease(releaseRoot, clientRoot, manifestPath, signaturePath, acceptedManifest.Manifest, acceptedManifest.Bytes, signature, acceptedSignature.Bytes, identity));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            return new StoredReleaseReadResult.Rejected(ManagedInstallationIssue.ClientComponentMissing);
        }
        catch (DirectoryNotFoundException)
        {
            return new StoredReleaseReadResult.Rejected(ManagedInstallationIssue.ClientComponentMissing);
        }
        catch (UnauthorizedAccessException)
        {
            return new StoredReleaseReadResult.Rejected(ManagedInstallationIssue.AccessDenied);
        }
        catch (LinkedInstallationPathException)
        {
            return new StoredReleaseReadResult.Rejected(ManagedInstallationIssue.LinkedPath);
        }
        catch (IOException)
        {
            return new StoredReleaseReadResult.Rejected(ManagedInstallationIssue.ReadFailure);
        }
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, long expectedLength, bool executable, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedLength);

        RequireRegularFile(sourcePath);
        await using FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        FileAttributes openedAttributes = File.GetAttributes(source.SafeFileHandle);
        if ((openedAttributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0)
        {
            throw new LinkedInstallationPathException();
        }

        if (source.Length != expectedLength)
        {
            throw new IOException("A release source file length does not match authenticated metadata.");
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            FileStreamOptions destinationOptions = new()
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = CopyBufferSize,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            };

            if (!OperatingSystem.IsWindows())
            {
                destinationOptions.UnixCreateMode = s_privateFileMode;
            }

            await using (FileStream destination = new(destinationPath, destinationOptions))
            {
                long remaining = expectedLength;
                while (remaining > 0)
                {
                    int read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        throw new IOException("A release source file changed while it was copied.");
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    remaining -= read;
                }

                if (await source.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) != 0)
                {
                    throw new IOException("A release source file changed while it was copied.");
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                destination.Flush(flushToDisk: true);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (source.Length != expectedLength)
        {
            throw new IOException("A release source file changed while it was copied.");
        }

        RequireRegularFile(sourcePath);
        SetFileMode(destinationPath, executable ? s_executableFileMode : s_dataFileMode);
    }

    private static void EnsureDestinationParent(string clientRoot, string destinationPath)
    {
        string? parent = Path.GetDirectoryName(destinationPath);
        if (parent is null)
        {
            throw new IOException("The release destination has no parent directory.");
        }

        CreateDirectory(parent);

        string relativeParent = Path.GetRelativePath(clientRoot, parent);
        string current = clientRoot;
        if (!string.Equals(relativeParent, ".", StringComparison.Ordinal))
        {
            foreach (string segment in relativeParent.Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, segment);
                RequireDirectory(current);
                SetDirectoryMode(current);
            }
        }
    }

    private static void CreateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(path, s_directoryMode);
        }
    }

    private static void RequireDirectory(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new LinkedInstallationPathException();
        }

        if ((attributes & (FileAttributes.Directory | FileAttributes.Device)) != FileAttributes.Directory)
        {
            throw new IOException("The installation path is not a directory.");
        }
    }

    private static void RequireRegularFile(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new LinkedInstallationPathException();
        }

        if ((attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
        {
            throw new IOException("The installation path is not a regular file.");
        }
    }

    private static bool EntryExists(string path)
    {
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

    private static void SetDirectoryMode(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, s_directoryMode);
        }
    }

    private static void SetFileMode(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }
}

internal sealed record ActiveReleaseLocation(string RootPath, string? ReleaseId);
internal sealed record AuthenticatedRelease(string RootPath, string ClientRootPath, string ManifestPath, string SignaturePath, ManagedReleaseManifest Manifest, byte[] ManifestBytes, ManagedReleaseSignature Signature, byte[] SignatureBytes, ManagedReleaseIdentity Identity);
internal sealed record ManagedReleaseGeneration(string ReleaseId, string RootPath, AuthenticatedRelease Release, bool Created);
internal abstract record StoredReleaseReadResult
{
    private StoredReleaseReadResult()
    {
    }

    internal sealed record Accepted(AuthenticatedRelease Release) : StoredReleaseReadResult;
    internal sealed record Rejected(ManagedInstallationIssue Issue) : StoredReleaseReadResult;
}

internal sealed class ManagedReleaseLockUnavailableException(Exception innerException) : IOException("The managed release transaction lock is unavailable.", innerException);

internal sealed class StagedReleaseRejectedException(ManagedInstallationIssue issue) : Exception($"The staged release was rejected: {issue}.")
{
    public ManagedInstallationIssue Issue { get; } = issue;
}
