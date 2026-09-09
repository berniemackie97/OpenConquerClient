using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Updates;

internal enum ReleasePackageIssue
{
    InvalidArchive,
    MetadataMismatch,
    IntegrityFailure,
    FileSystemFailure,
}

internal abstract record ReleasePackageExtractionResult
{
    private ReleasePackageExtractionResult()
    {
    }

    internal sealed record Extracted : ReleasePackageExtractionResult;

    internal sealed record Rejected(ReleasePackageIssue Issue) : ReleasePackageExtractionResult;
}

/// <summary>Expands one catalog-bound release archive into an unselected transaction candidate.</summary>
internal static class ReleasePackageExtractor
{
    public const long MaximumExpandedLength = 64L * 1024 * 1024 * 1024;

    private const int CopyBufferSize = 128 * 1024;
    private const int MaximumManifestLength = 8 * 1024 * 1024;
    private const int MaximumSignatureLength = 4 * 1024;
    private const int UnixFileTypeMask = 0xf000;
    private const int UnixRegularFileType = 0x8000;
    private static readonly UnixFileMode s_privateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private static readonly UnixFileMode s_dataFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    private static readonly UnixFileMode s_executableFileMode = s_dataFileMode |
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    public static async Task<ReleasePackageExtractionResult> ExtractAsync(
        string packagePath,
        string candidateRoot,
        ReleaseCatalogEntry catalogEntry,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateRoot);
        ArgumentNullException.ThrowIfNull(catalogEntry);

        if (!Path.IsPathFullyQualified(packagePath) || !Path.IsPathFullyQualified(candidateRoot))
        {
            throw new ArgumentException("Release package paths must be fully qualified.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireRegularFile(packagePath);
            if (EntryExists(candidateRoot))
            {
                throw new IOException("The release candidate destination already exists.");
            }

            CreateDirectory(candidateRoot);
            RequireDirectory(candidateRoot);

            await using FileStream package = new(packagePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.RandomAccess);
            using ZipArchive archive = new(package, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count is < 3 or > ManagedReleaseManifest.MaximumFileCount + 2)
            {
                return Rejected(ReleasePackageIssue.InvalidArchive);
            }

            Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.Ordinal);
            HashSet<string> portablePaths = new(StringComparer.Ordinal);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsRegularEntry(entry) || !IsValidEntryName(entry.FullName) ||
                    !entries.TryAdd(entry.FullName, entry) ||
                    !portablePaths.Add(entry.FullName.ToUpperInvariant()))
                {
                    return Rejected(ReleasePackageIssue.InvalidArchive);
                }
            }

            if (!entries.TryGetValue(ManagedReleaseManifest.FileName,
                    out ZipArchiveEntry? manifestEntry) ||
                manifestEntry.Length is <= 0 or > MaximumManifestLength ||
                !entries.TryGetValue(ManagedReleaseSignature.FileName,
                    out ZipArchiveEntry? signatureEntry) ||
                signatureEntry.Length is <= 0 or > MaximumSignatureLength)
            {
                return Rejected(ReleasePackageIssue.InvalidArchive);
            }

            string manifestPath = Path.Combine(candidateRoot, ManagedReleaseManifest.FileName);
            string signaturePath = Path.Combine(candidateRoot, ManagedReleaseSignature.FileName);
            if (!await ExtractEntryAsync(manifestEntry, manifestPath, manifestEntry.Length,
                    expectedSha256: null, executable: false, cancellationToken)
                    .ConfigureAwait(false) ||
                !await ExtractEntryAsync(signatureEntry, signaturePath, signatureEntry.Length,
                    expectedSha256: null, executable: false, cancellationToken)
                    .ConfigureAwait(false))
            {
                return Rejected(ReleasePackageIssue.IntegrityFailure);
            }

            ReleaseManifestReadResult manifestResult = await ManagedReleaseManifest.ReadAsync(
                manifestPath, cancellationToken).ConfigureAwait(false);
            ReleaseSignatureReadResult signatureResult = await ManagedReleaseSignature.ReadAsync(
                signaturePath, cancellationToken).ConfigureAwait(false);
            if (manifestResult is not ReleaseManifestReadResult.Accepted acceptedManifest ||
                signatureResult is not ReleaseSignatureReadResult.Accepted)
            {
                return Rejected(ReleasePackageIssue.InvalidArchive);
            }

            ManagedReleaseManifest manifest = acceptedManifest.Manifest;
            if (manifest.ReleaseSequence != catalogEntry.ReleaseSequence ||
                !string.Equals(manifest.ReleaseVersion, catalogEntry.ReleaseVersion,
                    StringComparison.Ordinal) ||
                manifest.MinimumLauncherVersion != catalogEntry.MinimumLauncherVersion ||
                !string.Equals(manifest.TargetRuntime, catalogEntry.TargetRuntime,
                    StringComparison.Ordinal))
            {
                return Rejected(ReleasePackageIssue.MetadataMismatch);
            }

            long expandedLength = checked(manifestEntry.Length + signatureEntry.Length);
            HashSet<string> expectedEntries = new(StringComparer.Ordinal)
            {
                ManagedReleaseManifest.FileName,
                ManagedReleaseSignature.FileName,
            };
            foreach (ManagedReleaseFile file in manifest.Files)
            {
                expandedLength = checked(expandedLength + file.Length);
                if (expandedLength > MaximumExpandedLength)
                {
                    return Rejected(ReleasePackageIssue.InvalidArchive);
                }

                expectedEntries.Add("client/" + file.Path);
            }

            if (expectedEntries.Count != entries.Count ||
                expectedEntries.Any(expected => !entries.ContainsKey(expected)))
            {
                return Rejected(ReleasePackageIssue.InvalidArchive);
            }

            string clientRoot = Path.Combine(candidateRoot,
                ManagedInstallationManifest.ExpectedClientRoot);
            CreateDirectory(clientRoot);
            foreach (ManagedReleaseFile expected in manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ZipArchiveEntry entry = entries["client/" + expected.Path];
                if (entry.Length != expected.Length)
                {
                    return Rejected(ReleasePackageIssue.IntegrityFailure);
                }

                string destinationPath = ReleasePackagePath.Combine(clientRoot, expected.Path);
                EnsureParentDirectories(clientRoot, destinationPath);
                bool executable = string.Equals(expected.Path, manifest.ClientExecutable,
                    StringComparison.Ordinal);
                if (!await ExtractEntryAsync(entry, destinationPath, expected.Length,
                        expected.Sha256, executable, cancellationToken).ConfigureAwait(false))
                {
                    return Rejected(ReleasePackageIssue.IntegrityFailure);
                }
            }

            if (!ManagedReleaseDirectory.HasExpectedShape(candidateRoot))
            {
                return Rejected(ReleasePackageIssue.InvalidArchive);
            }

            return new ReleasePackageExtractionResult.Extracted();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or
            NotSupportedException or OverflowException)
        {
            return Rejected(ReleasePackageIssue.InvalidArchive);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Rejected(ReleasePackageIssue.FileSystemFailure);
        }
    }

    private static async Task<bool> ExtractEntryAsync(
        ZipArchiveEntry entry,
        string destinationPath,
        long expectedLength,
        byte[]? expectedSha256,
        bool executable,
        CancellationToken cancellationToken)
    {
        await using Stream source = entry.Open();
        FileStreamOptions options = new()
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = CopyBufferSize,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = s_dataFileMode;
        }

        await using FileStream destination = new(destinationPath, options);
        using IncrementalHash? hash = expectedSha256 is null
            ? null
            : IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            long remaining = expectedLength;
            while (remaining > 0)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0,
                    (int)Math.Min(buffer.Length, remaining)), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return false;
                }

                hash?.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
                remaining -= read;
            }

            if (await source.ReadAsync(buffer.AsMemory(0, 1), cancellationToken)
                    .ConfigureAwait(false) != 0)
            {
                return false;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (expectedSha256 is not null && !CryptographicOperations.FixedTimeEquals(
                hash!.GetHashAndReset(), expectedSha256))
        {
            return false;
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        destination.Flush(flushToDisk: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(destination.SafeFileHandle,
                executable ? s_executableFileMode : s_dataFileMode);
        }

        return true;
    }

    private static bool IsValidEntryName(string name)
    {
        if (name is ManagedReleaseManifest.FileName or ManagedReleaseSignature.FileName)
        {
            return true;
        }

        const string clientPrefix = "client/";
        return name.StartsWith(clientPrefix, StringComparison.Ordinal) &&
            ReleasePackagePath.IsValid(name[clientPrefix.Length..]);
    }

    private static bool IsRegularEntry(ZipArchiveEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/'))
        {
            return false;
        }

        int unixType = (entry.ExternalAttributes >> 16) & UnixFileTypeMask;
        return (unixType is 0 or UnixRegularFileType) &&
            (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) == 0;
    }

    private static void EnsureParentDirectories(string root, string destinationPath)
    {
        string? parent = Path.GetDirectoryName(destinationPath);
        if (parent is null)
        {
            throw new InvalidDataException("A release entry has no parent directory.");
        }

        string relative = Path.GetRelativePath(root, parent);
        string current = root;
        if (!string.Equals(relative, ".", StringComparison.Ordinal))
        {
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, segment);
                CreateDirectory(current);
                RequireDirectory(current);
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
            throw new InvalidDataException("A release destination directory is unsafe.");
        }
    }

    private static void RequireRegularFile(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory |
                FileAttributes.Device)) != 0)
        {
            throw new InvalidDataException("The release package is not a regular file.");
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

    private static ReleasePackageExtractionResult.Rejected Rejected(
        ReleasePackageIssue issue) => new(issue);
}
