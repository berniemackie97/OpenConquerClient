using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;

namespace OpenConquer.Product.Tool;

/// <summary>Creates and validates the deterministic archive consumed by launcher acquisition.</summary>
internal static class ProductReleasePackage
{
    public const long MaximumPackageLength = 16L * 1024 * 1024 * 1024;
    public const long MaximumExpandedLength = 64L * 1024 * 1024 * 1024;

    private const int MaximumManifestLength = 8 * 1024 * 1024;
    private const int MaximumSignatureLength = 4 * 1024;
    private const int UnixRegularFileType = 0x8000;
    private const int UnixDataMode = 0x01a4;
    private const int UnixExecutableMode = 0x01ed;
    private static readonly DateTimeOffset s_deterministicTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Create(ReleasePackageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string clientRoot = ProductStagingPathGuard.RequireDirectory(
            options.ClientPublishPath, nameof(options.ClientPublishPath));
        string manifestPath = ProductStagingPathGuard.RequireRegularFile(
            options.ReleaseManifestPath, nameof(options.ReleaseManifestPath));
        string signaturePath = ProductStagingPathGuard.RequireRegularFile(
            options.ReleaseSignaturePath, nameof(options.ReleaseSignaturePath));
        string publicKeyPath = ProductStagingPathGuard.RequireRegularFile(
            options.PublicKeyPath, nameof(options.PublicKeyPath));
        string outputPath = ProductStagingPathGuard.NormalizePath(
            options.OutputPath, nameof(options.OutputPath));

        ProductStagingPathGuard.RejectPathWithinRoot(clientRoot, manifestPath,
            "The release manifest must remain outside the client publish.");
        ProductStagingPathGuard.RejectPathWithinRoot(clientRoot, signaturePath,
            "The release signature must remain outside the client publish.");
        ProductStagingPathGuard.RejectPathWithinRoot(clientRoot, publicKeyPath,
            "The release public key must remain outside the client publish.");
        ProductStagingPathGuard.RejectPathWithinRoot(clientRoot, outputPath,
            "The release package output must remain outside the client publish.");
        RejectOutputMatchesInput(outputPath, manifestPath, signaturePath, publicKeyPath);
        ProductStagingPathGuard.PrepareFileOutput(outputPath);

        byte[] manifestBytes = ProductReleaseManifest.ReadRegularFile(
            manifestPath, MaximumManifestLength);
        byte[] signatureBytes = ProductReleaseManifest.ReadRegularFile(
            signaturePath, MaximumSignatureLength);
        ProductReleaseManifest manifest = ProductReleaseManifest.Read(manifestBytes);
        ProductReleaseSignature.VerifyEnvelope(
            manifestBytes, signatureBytes, publicKeyPath);
        ProductReleaseManifest.VerifyClient(clientRoot, manifest);

        string temporaryPath = outputPath + $".tmp-{Guid.NewGuid():N}";
        bool completed = false;
        try
        {
            using (FileStream package = new(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite,
                       FileShare.None))
            {
                using (ZipArchive archive = new(package, ZipArchiveMode.Create, leaveOpen: true))
                {
                    WriteBytes(archive, ProductReleaseManifest.FileName, manifestBytes,
                        executable: false);
                    WriteBytes(archive, ProductReleaseSignature.FileName, signatureBytes,
                        executable: false);

                    foreach (ProductReleaseFile file in manifest.Files)
                    {
                        WriteClientFile(archive, clientRoot, file,
                            string.Equals(file.Path, manifest.ClientExecutable,
                                StringComparison.Ordinal));
                    }
                }

                package.Flush(flushToDisk: true);
                if (package.Length is <= 0 or > MaximumPackageLength)
                {
                    throw new InvalidDataException("The release package length is invalid.");
                }
            }

            _ = Validate(temporaryPath);

            if (!manifestBytes.AsSpan().SequenceEqual(ProductReleaseManifest.ReadRegularFile(
                    manifestPath, MaximumManifestLength)) ||
                !signatureBytes.AsSpan().SequenceEqual(ProductReleaseManifest.ReadRegularFile(
                    signaturePath, MaximumSignatureLength)))
            {
                throw new IOException("Release metadata changed while packaging.");
            }

            File.Move(temporaryPath, outputPath);
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                TryDelete(temporaryPath);
            }
        }
    }

    public static ProductReleaseManifest Validate(string packagePath)
    {
        string normalized = ProductStagingPathGuard.RequireRegularFile(
            packagePath, "release package");
        using FileStream package = new(normalized, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 128 * 1024, FileOptions.SequentialScan);
        if (package.Length is <= 0 or > MaximumPackageLength)
        {
            throw new InvalidDataException("The release package length is invalid.");
        }

        using ZipArchive archive = new(package, ZipArchiveMode.Read, leaveOpen: false);
        if (archive.Entries.Count is < 3 or > ProductReleaseManifest.MaximumFileCount + 2)
        {
            throw new InvalidDataException("The release package entry count is invalid.");
        }

        Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.Ordinal);
        HashSet<string> portablePaths = new(StringComparer.Ordinal);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!IsRegularEntry(entry) || !IsValidEntryName(entry.FullName) ||
                !entries.TryAdd(entry.FullName, entry) ||
                !portablePaths.Add(PortableEntryIdentity(entry.FullName)))
            {
                throw new InvalidDataException("The release package contains an invalid entry.");
            }
        }

        if (!entries.TryGetValue(ProductReleaseManifest.FileName,
                out ZipArchiveEntry? manifestEntry) ||
            !entries.TryGetValue(ProductReleaseSignature.FileName,
                out ZipArchiveEntry? signatureEntry))
        {
            throw new InvalidDataException("The release package metadata is missing.");
        }

        byte[] manifestBytes = ReadEntry(manifestEntry, MaximumManifestLength);
        byte[] signatureBytes = ReadEntry(signatureEntry, MaximumSignatureLength);
        ProductReleaseManifest manifest = ProductReleaseManifest.Read(manifestBytes);
        ProductReleaseSignature.ValidateEnvelope(signatureBytes);

        long expandedLength = checked(manifestEntry.Length + signatureEntry.Length);
        HashSet<string> expectedEntries = new(StringComparer.Ordinal)
        {
            ProductReleaseManifest.FileName,
            ProductReleaseSignature.FileName,
        };
        foreach (ProductReleaseFile file in manifest.Files)
        {
            expandedLength = checked(expandedLength + file.Length);
            if (expandedLength > MaximumExpandedLength)
            {
                throw new InvalidDataException("The expanded release package is too large.");
            }

            string entryName = "client/" + file.Path;
            if (!expectedEntries.Add(entryName) || !entries.TryGetValue(entryName,
                    out ZipArchiveEntry? entry) || entry.Length != file.Length)
            {
                throw new InvalidDataException("The release package does not match its manifest.");
            }

            using Stream source = entry.Open();
            byte[] hash = SHA256.HashData(source);
            if (!CryptographicOperations.FixedTimeEquals(hash, file.Sha256))
            {
                throw new InvalidDataException("The release package does not match its manifest.");
            }
        }

        if (expectedEntries.Count != entries.Count)
        {
            throw new InvalidDataException("The release package contains unexpected entries.");
        }

        return manifest;
    }

    public static void VerifyPublisher(
        string packagePath,
        IReadOnlyList<string> publicKeyPaths)
    {
        ArgumentNullException.ThrowIfNull(publicKeyPaths);
        if (publicKeyPaths.Count is 0 or > ProductReleaseTrust.MaximumKeyCount)
        {
            throw new ArgumentException("The release publisher-key count is invalid.",
                nameof(publicKeyPaths));
        }

        string normalized = ProductStagingPathGuard.RequireRegularFile(
            packagePath, "release package");
        using FileStream package = new(normalized, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 128 * 1024, FileOptions.SequentialScan);
        using ZipArchive archive = new(package, ZipArchiveMode.Read, leaveOpen: false);
        ZipArchiveEntry manifestEntry = archive.GetEntry(ProductReleaseManifest.FileName) ??
            throw new InvalidDataException("The release package metadata is missing.");
        ZipArchiveEntry signatureEntry = archive.GetEntry(ProductReleaseSignature.FileName) ??
            throw new InvalidDataException("The release package metadata is missing.");
        byte[] manifestBytes = ReadEntry(manifestEntry, MaximumManifestLength);
        byte[] signatureBytes = ReadEntry(signatureEntry, MaximumSignatureLength);
        foreach (string publicKeyPath in publicKeyPaths)
        {
            try
            {
                ProductReleaseSignature.VerifyEnvelope(manifestBytes, signatureBytes,
                    publicKeyPath);
                return;
            }
            catch (InvalidDataException)
            {
            }
        }

        throw new InvalidDataException(
            "The release package is not authenticated by an accepted publisher key.");
    }

    public static (long Length, byte[] Sha256) ReadIdentity(string packagePath)
    {
        string normalized = ProductStagingPathGuard.RequireRegularFile(
            packagePath, "release package");
        using FileStream package = new(normalized, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 128 * 1024, FileOptions.SequentialScan);
        long length = package.Length;
        if (length is <= 0 or > MaximumPackageLength)
        {
            throw new InvalidDataException("The release package length is invalid.");
        }

        byte[] digest = SHA256.HashData(package);
        if (package.Length != length)
        {
            throw new IOException("The release package changed while it was being read.");
        }

        return (length, digest);
    }

    private static void WriteBytes(
        ZipArchive archive,
        string name,
        ReadOnlySpan<byte> bytes,
        bool executable)
    {
        ZipArchiveEntry entry = CreateEntry(archive, name, executable);
        using Stream destination = entry.Open();
        destination.Write(bytes);
    }

    private static void WriteClientFile(
        ZipArchive archive,
        string clientRoot,
        ProductReleaseFile expected,
        bool executable)
    {
        string sourcePath = ProductStagingPathGuard.RequireRegularFile(
            ProductReleasePath.Combine(clientRoot, expected.Path), "client release file");
        ZipArchiveEntry entry = CreateEntry(archive, "client/" + expected.Path, executable);
        using FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 128 * 1024, FileOptions.SequentialScan);
        if (source.Length != expected.Length)
        {
            throw new IOException($"Client file changed while packaging: '{expected.Path}'.");
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using Stream destination = entry.Open();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
        try
        {
            long remaining = expected.Length;
            while (remaining > 0)
            {
                int read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read == 0)
                {
                    throw new IOException(
                        $"Client file changed while packaging: '{expected.Path}'.");
                }

                hash.AppendData(buffer, 0, read);
                destination.Write(buffer, 0, read);
                remaining -= read;
            }

            if (source.ReadByte() != -1 || !CryptographicOperations.FixedTimeEquals(
                    hash.GetHashAndReset(), expected.Sha256))
            {
                throw new IOException(
                    $"Client file changed while packaging: '{expected.Path}'.");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ZipArchiveEntry CreateEntry(
        ZipArchive archive,
        string name,
        bool executable)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = s_deterministicTimestamp;
        entry.ExternalAttributes = (UnixRegularFileType |
            (executable ? UnixExecutableMode : UnixDataMode)) << 16;
        return entry;
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, int maximumLength)
    {
        if (entry.Length is <= 0 || entry.Length > maximumLength)
        {
            throw new InvalidDataException("The release package metadata length is invalid.");
        }

        byte[] bytes = new byte[(int)entry.Length];
        using Stream source = entry.Open();
        source.ReadExactly(bytes);
        if (source.ReadByte() != -1)
        {
            throw new InvalidDataException("The release package metadata length changed.");
        }

        return bytes;
    }

    private static bool IsValidEntryName(string name)
    {
        return name is ProductReleaseManifest.FileName or ProductReleaseSignature.FileName ||
            name.StartsWith("client/", StringComparison.Ordinal) &&
            ProductReleasePath.IsValid(name["client/".Length..]);
    }

    private static string PortableEntryIdentity(string name)
    {
        return name.StartsWith("client/", StringComparison.Ordinal)
            ? "CLIENT/" + ProductReleasePath.PortableIdentity(name["client/".Length..])
            : name.ToUpperInvariant();
    }

    private static bool IsRegularEntry(ZipArchiveEntry entry)
    {
        int unixType = (entry.ExternalAttributes >> 16) & 0xf000;
        return !string.IsNullOrEmpty(entry.Name) && !entry.FullName.EndsWith('/') &&
            (unixType is 0 or UnixRegularFileType) &&
            (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) == 0;
    }

    private static void RejectOutputMatchesInput(string outputPath, params string[] inputPaths)
    {
        foreach (string inputPath in inputPaths)
        {
            if (string.Equals(outputPath, inputPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The release package output must not replace an input file.");
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
