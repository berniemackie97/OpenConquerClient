using System.Security.Cryptography;

namespace OpenConquer.Launcher.Installation;

/// <summary>Authenticates release metadata and verifies the complete managed client tree.</summary>
internal sealed class ManagedReleaseVerifier
{
    private const int MaximumDirectoryCount = 16 * 1024;

    private readonly TrustedReleaseKeys _trustedKeys;
    private readonly string? _currentRuntime;

    public ManagedReleaseVerifier(TrustedReleaseKeys trustedKeys, string? currentRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(trustedKeys);

        _trustedKeys = trustedKeys;
        _currentRuntime = currentRuntime ?? ReleaseTargetRuntime.Current;
    }

    public async Task<ManagedReleaseVerification> VerifyAsync(string clientRootPath, ManagedReleaseManifest manifest, ReadOnlyMemory<byte> manifestBytes, ManagedReleaseSignature signature, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientRootPath);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(signature);

        if (!_trustedKeys.IsConfigured)
        {
            return new ManagedReleaseVerification.Rejected(ManagedInstallationIssue.ReleaseAuthorityUnavailable);
        }

        if (!_trustedKeys.Verify(signature, manifestBytes.Span))
        {
            return new ManagedReleaseVerification.Rejected(ManagedInstallationIssue.ReleaseSignatureInvalid);
        }

        if (_currentRuntime is null || !string.Equals(manifest.TargetRuntime, _currentRuntime, StringComparison.Ordinal))
        {
            return new ManagedReleaseVerification.Rejected(ManagedInstallationIssue.ClientPlatformMismatch);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<string, string> actualFiles = EnumerateClientFiles(clientRootPath, cancellationToken);

            if (actualFiles.Count != manifest.Files.Count)
            {
                return new ManagedReleaseVerification.Rejected(ManagedInstallationIssue.ClientIntegrityFailure);
            }

            foreach (ManagedReleaseFile expected in manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!actualFiles.TryGetValue(expected.Path, out string? actualPath) || !await MatchesAsync(actualPath, expected, cancellationToken).ConfigureAwait(false))
                {
                    return new ManagedReleaseVerification.Rejected(ManagedInstallationIssue.ClientIntegrityFailure);
                }
            }

            // Re-enumeration catches ordinary update/repair races that add, remove,
            // link, or rename entries while hashing. A hostile process executing as
            // the same OS user is outside this filesystem-integrity trust boundary.
            Dictionary<string, string> finalFiles = EnumerateClientFiles(clientRootPath, cancellationToken);

            if (finalFiles.Count != actualFiles.Count || actualFiles.Any(pair => !finalFiles.TryGetValue(pair.Key, out string? finalPath)
                    || !string.Equals(pair.Value, finalPath, StringComparison.Ordinal)))
            {
                return new ManagedReleaseVerification.Rejected(ManagedInstallationIssue.ClientIntegrityFailure);
            }

            string executablePath = ReleasePackagePath.Combine(clientRootPath, manifest.ClientExecutable);

            return new ManagedReleaseVerification.Verified(new ManagedReleaseIdentity(manifest.ReleaseSequence, manifest.ReleaseVersion, manifest.TargetRuntime), executablePath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return new ManagedReleaseVerification.Rejected(ManagedInstallationIssue.AccessDenied);
        }
        catch (LinkedInstallationPathException)
        {
            return new ManagedReleaseVerification.Rejected(ManagedInstallationIssue.LinkedPath);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or InvalidDataException)
        {
            return new ManagedReleaseVerification.Rejected(ManagedInstallationIssue.ClientIntegrityFailure);
        }
        catch (IOException)
        {
            return new ManagedReleaseVerification.Rejected(ManagedInstallationIssue.ReadFailure);
        }
    }

    private static Dictionary<string, string> EnumerateClientFiles(string clientRootPath, CancellationToken cancellationToken)
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        HashSet<string> portablePaths = new(StringComparer.Ordinal);
        Stack<DirectoryInfo> pending = new();

        pending.Push(new DirectoryInfo(clientRootPath));

        int directoryCount = 0;

        while (pending.TryPop(out DirectoryInfo? directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (++directoryCount > MaximumDirectoryCount)
            {
                throw new InvalidDataException("The client directory count exceeds the release limit.");
            }

            directory.Refresh();

            if (!directory.Exists || directory.LinkTarget is not null || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new LinkedInstallationPathException();
            }

            foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                entry.Refresh();

                if (entry.LinkTarget is not null || (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new LinkedInstallationPathException();
                }

                if (entry is DirectoryInfo childDirectory)
                {
                    pending.Push(childDirectory);
                    continue;
                }

                if (entry is not FileInfo || (entry.Attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
                {
                    throw new InvalidDataException("The client contains an unsupported filesystem entry.");
                }

                if (files.Count == ManagedReleaseManifest.MaximumFileCount)
                {
                    throw new InvalidDataException("The client file count exceeds the release limit.");
                }

                string relativePath = Path.GetRelativePath(clientRootPath, entry.FullName).Replace(Path.DirectorySeparatorChar, '/');

                if (!ReleasePackagePath.IsValid(relativePath) || !files.TryAdd(relativePath, entry.FullName) || !portablePaths.Add(ReleasePackagePath.PortableIdentity(relativePath)))
                {
                    throw new InvalidDataException("The client contains an invalid or ambiguous path.");
                }
            }
        }

        return files;
    }

    private static async Task<bool> MatchesAsync(string path, ManagedReleaseFile expected, CancellationToken cancellationToken)
    {
        FileAttributes pathAttributes = File.GetAttributes(path);

        if ((pathAttributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0)
        {
            throw new LinkedInstallationPathException();
        }

        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

        FileAttributes openedAttributes = File.GetAttributes(stream.SafeFileHandle);

        if ((openedAttributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0)
        {
            throw new LinkedInstallationPathException();
        }

        if (stream.Length != expected.Length)
        {
            return false;
        }

        byte[] actualHash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);

        if (stream.Length != expected.Length)
        {
            return false;
        }

        FileAttributes finalPathAttributes = File.GetAttributes(path);

        if ((finalPathAttributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(actualHash, expected.Sha256);
    }
}

internal sealed record ManagedReleaseIdentity(ulong Sequence, string Version, string TargetRuntime);

internal abstract record ManagedReleaseVerification
{
    private ManagedReleaseVerification()
    {
    }

    internal sealed record Verified(ManagedReleaseIdentity Release, string ClientExecutablePath)
        : ManagedReleaseVerification;

    internal sealed record Rejected(ManagedInstallationIssue Issue) : ManagedReleaseVerification;
}
