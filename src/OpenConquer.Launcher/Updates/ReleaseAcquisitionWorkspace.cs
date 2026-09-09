using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Updates;

internal sealed class ReleaseCandidateLease : IDisposable
{
    private readonly FileStream _workspaceLock;
    private readonly string _attemptRoot;
    private int _disposed;

    public ReleaseCandidateLease(
        string candidateRoot,
        string attemptRoot,
        FileStream workspaceLock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(attemptRoot);
        ArgumentNullException.ThrowIfNull(workspaceLock);
        if (!Path.IsPathFullyQualified(candidateRoot) ||
            !Path.IsPathFullyQualified(attemptRoot))
        {
            throw new ArgumentException("Release candidate lease paths must be fully qualified.");
        }

        string normalizedAttempt = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(attemptRoot));
        string normalizedCandidate = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(candidateRoot));
        if (!ReleaseAcquisitionWorkspace.IsOwnedAttemptName(
                Path.GetFileName(normalizedAttempt)) ||
            !string.Equals(normalizedCandidate,
                Path.Combine(normalizedAttempt,
                    ReleaseAcquisitionWorkspace.CandidateDirectoryName),
                StringComparison.Ordinal))
        {
            throw new ArgumentException("Release candidate lease paths are not launcher-owned.");
        }

        CandidateRoot = normalizedCandidate;
        _attemptRoot = normalizedAttempt;
        _workspaceLock = workspaceLock;
    }

    public string CandidateRoot
    {
        get;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            Directory.Delete(_attemptRoot, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
            // Authenticated update payloads contain no secrets; stale attempts are inert.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the completed maintenance outcome if best-effort cleanup fails.
        }
        finally
        {
            TryDispose(_workspaceLock);
        }
    }

    private static void TryDispose(FileStream stream)
    {
        try
        {
            stream.Dispose();
        }
        catch (IOException)
        {
            // Lock cleanup must not replace the completed maintenance result.
        }
    }
}

internal enum ReleaseAcquisitionIssue
{
    OperationAlreadyInProgress,
    UnsafeWorkspace,
    WorkspaceUnavailable,
    TransferRejected,
    PackageRejected,
}

internal abstract record ReleaseAcquisitionResult
{
    private ReleaseAcquisitionResult()
    {
    }

    internal sealed record Acquired(ReleaseCandidateLease Candidate) : ReleaseAcquisitionResult;

    internal sealed record Rejected(
        ReleaseAcquisitionIssue Issue,
        ReleaseTransferIssue? TransferIssue,
        ReleasePackageIssue? PackageIssue) : ReleaseAcquisitionResult;
}

/// <summary>Owns serialized, private download staging outside the managed product root.</summary>
internal sealed class ReleaseAcquisitionWorkspace
{
    public const string LockFileName = ".openconquer.acquire.lock";
    internal const string CandidateDirectoryName = "candidate";

    private const string AttemptPrefix = ".openconquer-acquire-";
    private static readonly UnixFileMode s_privateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private static readonly UnixFileMode s_lockFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string _workspaceRoot;
    private readonly string _productRoot;
    private readonly ReleaseHttpTransport _transport;

    public ReleaseAcquisitionWorkspace(
        string workspaceRoot,
        string productRoot,
        ReleaseHttpTransport transport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(productRoot);
        ArgumentNullException.ThrowIfNull(transport);
        if (!Path.IsPathFullyQualified(workspaceRoot) || !Path.IsPathFullyQualified(productRoot))
        {
            throw new ArgumentException("Release workspace paths must be fully qualified.");
        }

        _workspaceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot));
        _productRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(productRoot));
        _transport = transport;
    }

    public async Task<ReleaseAcquisitionResult> AcquireAsync(
        ReleaseCatalogEntry release,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        FileStream? workspaceLock = null;
        string? attemptRoot = null;
        bool leased = false;
        try
        {
            if (ManagedInstallationPathGuard.PathsOverlap(_workspaceRoot, _productRoot))
            {
                throw new InvalidDataException(
                    "The release workspace must remain outside the managed product.");
            }

            CreateDirectory(_workspaceRoot);
            RequireDirectory(_workspaceRoot);
            workspaceLock = AcquireLock();
            cancellationToken.ThrowIfCancellationRequested();
            RemoveStaleAttempts();

            attemptRoot = Path.Combine(_workspaceRoot,
                AttemptPrefix + Guid.NewGuid().ToString("N"));
            if (EntryExists(attemptRoot))
            {
                throw new IOException("A release acquisition attempt path already exists.");
            }

            CreateDirectory(attemptRoot);
            RequireDirectory(attemptRoot);
            string packagePath = Path.Combine(attemptRoot, "release.zip");
            ReleaseTransferResult<string> transfer = await _transport.DownloadFileAsync(
                release.PackageUri, packagePath, release.PackageLength, release.PackageSha256,
                cancellationToken).ConfigureAwait(false);
            if (transfer is ReleaseTransferResult<string>.Rejected transferRejected)
            {
                return new ReleaseAcquisitionResult.Rejected(
                    ReleaseAcquisitionIssue.TransferRejected, transferRejected.Issue,
                    PackageIssue: null);
            }

            string candidateRoot = Path.Combine(attemptRoot, CandidateDirectoryName);
            ReleasePackageExtractionResult extraction = await ReleasePackageExtractor.ExtractAsync(
                packagePath, candidateRoot, release, cancellationToken).ConfigureAwait(false);
            if (extraction is ReleasePackageExtractionResult.Rejected extractionRejected)
            {
                return new ReleaseAcquisitionResult.Rejected(
                    ReleaseAcquisitionIssue.PackageRejected, TransferIssue: null,
                    extractionRejected.Issue);
            }

            ReleaseCandidateLease lease = new(candidateRoot, attemptRoot, workspaceLock);
            leased = true;
            return new ReleaseAcquisitionResult.Acquired(lease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ReleaseAcquisitionLockUnavailableException)
        {
            return new ReleaseAcquisitionResult.Rejected(
                ReleaseAcquisitionIssue.OperationAlreadyInProgress, TransferIssue: null,
                PackageIssue: null);
        }
        catch (InvalidDataException)
        {
            return new ReleaseAcquisitionResult.Rejected(
                ReleaseAcquisitionIssue.UnsafeWorkspace, TransferIssue: null,
                PackageIssue: null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ReleaseAcquisitionResult.Rejected(
                ReleaseAcquisitionIssue.WorkspaceUnavailable, TransferIssue: null,
                PackageIssue: null);
        }
        finally
        {
            if (!leased)
            {
                if (attemptRoot is not null)
                {
                    TryDeleteTree(attemptRoot);
                }

                if (workspaceLock is not null)
                {
                    TryDispose(workspaceLock);
                }
            }
        }
    }

    private FileStream AcquireLock()
    {
        string path = Path.Combine(_workspaceRoot, LockFileName);
        if (EntryExists(path))
        {
            RequireRegularFile(path);
        }

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

        FileStream stream;
        try
        {
            stream = new FileStream(path, options);
        }
        catch (IOException exception) when (EntryExists(path))
        {
            throw new ReleaseAcquisitionLockUnavailableException(exception);
        }

        try
        {
            FileAttributes attributes = File.GetAttributes(stream.SafeFileHandle);
            if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory |
                    FileAttributes.Device)) != 0)
            {
                throw new InvalidDataException("The release workspace lock is unsafe.");
            }

            RequireRegularFile(path);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(stream.SafeFileHandle, s_lockFileMode);
            }

            return stream;
        }
        catch
        {
            TryDispose(stream);
            throw;
        }
    }

    private void RemoveStaleAttempts()
    {
        foreach (FileSystemInfo entry in new DirectoryInfo(_workspaceRoot)
                     .EnumerateFileSystemInfos(AttemptPrefix + "*"))
        {
            entry.Refresh();
            if (!IsOwnedAttemptName(entry.Name))
            {
                continue;
            }

            if (entry is not DirectoryInfo || entry.LinkTarget is not null ||
                (entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    "The release workspace contains an unsafe acquisition attempt.");
            }

            Directory.Delete(entry.FullName, recursive: true);
        }
    }

    internal static bool IsOwnedAttemptName(string name)
    {
        if (!name.StartsWith(AttemptPrefix, StringComparison.Ordinal) ||
            name.Length != AttemptPrefix.Length + 32)
        {
            return false;
        }

        ReadOnlySpan<char> suffix = name.AsSpan(AttemptPrefix.Length);
        foreach (char character in suffix)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    private static void CreateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(path, s_privateDirectoryMode);
        }
    }

    private static void RequireDirectory(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            (attributes & (FileAttributes.Directory | FileAttributes.Device)) !=
            FileAttributes.Directory)
        {
            throw new InvalidDataException("The release workspace is unsafe.");
        }

        if (!OperatingSystem.IsWindows() &&
            File.GetUnixFileMode(path) != s_privateDirectoryMode)
        {
            throw new InvalidDataException(
                "The release workspace directory must grant access only to its owner.");
        }
    }

    private static void RequireRegularFile(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory |
                FileAttributes.Device)) != 0)
        {
            throw new InvalidDataException("The release workspace lock is unsafe.");
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

    private static void TryDeleteTree(string path)
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDispose(FileStream stream)
    {
        try
        {
            stream.Dispose();
        }
        catch (IOException)
        {
            // Lock cleanup must not replace cancellation or an acquisition result.
        }
    }
}

internal sealed class ReleaseAcquisitionLockUnavailableException : IOException
{
    public ReleaseAcquisitionLockUnavailableException(Exception innerException)
        : base("Another release acquisition is already in progress.", innerException)
    {
    }
}
