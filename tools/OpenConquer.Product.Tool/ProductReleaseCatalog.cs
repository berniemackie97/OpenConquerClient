using System.Security.Cryptography;
using System.Text.Json;

namespace OpenConquer.Product.Tool;

internal sealed record ProductReleaseCatalogEntry(
    ulong ReleaseSequence,
    string ReleaseVersion,
    int MinimumLauncherVersion,
    string TargetRuntime,
    Uri PackageUri,
    long PackageLength,
    byte[] PackageSha256);

/// <summary>Creates the bounded release index authenticated by the publisher.</summary>
internal static class ProductReleaseCatalog
{
    public const int CurrentSchemaVersion = 1;
    public const string ExpectedProductId = "OpenConquer";
    public const string FileName = "openconquer.catalog.json";
    public const string SignatureFileName = "openconquer.catalog.sig";
    public const int MaximumLength = 256 * 1024;
    public const int MaximumReleaseCount = 128;
    public const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    public static readonly TimeSpan MaximumRemainingLifetime = TimeSpan.FromDays(31);

    private const int MaximumPackageFileNameLength = 128;

    public static void Create(ReleaseCatalogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!ProductReleaseUri.IsValidHttps(options.PackageBaseUri) ||
            !string.IsNullOrEmpty(options.PackageBaseUri.Query) ||
            !options.PackageBaseUri.AbsolutePath.EndsWith('/'))
        {
            throw new ArgumentException("The package base URI is invalid.", nameof(options));
        }

        if (options.ReleasePackagePaths.Count is 0 or > MaximumReleaseCount)
        {
            throw new ArgumentException("The release package count is invalid.", nameof(options));
        }

        string expirationText = options.ExpiresAt.ToUniversalTime().ToString(TimestampFormat,
            System.Globalization.CultureInfo.InvariantCulture);
        DateTimeOffset expiration = DateTimeOffset.ParseExact(expirationText, TimestampFormat,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal |
            System.Globalization.DateTimeStyles.AdjustToUniversal);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (expiration <= now || expiration - now > MaximumRemainingLifetime)
        {
            throw new ArgumentException(
                "The release catalog expiration must be within the next 31 days.",
                nameof(options));
        }

        string outputPath = ProductStagingPathGuard.NormalizePath(
            options.OutputPath, nameof(options.OutputPath));
        List<string> publicKeyPaths = ValidatePublicKeys(
            options.PublicKeyPaths, outputPath);
        List<ProductReleaseCatalogEntry> entries = [];
        HashSet<string> packageNames = new(StringComparer.Ordinal);
        HashSet<(string Runtime, ulong Sequence)> releaseIdentities = [];
        foreach (string packagePathValue in options.ReleasePackagePaths)
        {
            string packagePath = ProductStagingPathGuard.RequireRegularFile(
                packagePathValue, "release package");
            if (string.Equals(packagePath, outputPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The release catalog output must not replace a package.");
            }

            string fileName = Path.GetFileName(packagePath);
            if (!IsValidPackageFileName(fileName) ||
                !packageNames.Add(fileName.ToUpperInvariant()))
            {
                throw new InvalidDataException(
                    "Release package file names must be unique and portable.");
            }

            ProductReleaseManifest manifest = ProductReleasePackage.Validate(packagePath);
            ProductReleasePackage.VerifyPublisher(packagePath, publicKeyPaths);
            (long length, byte[] digest) = ProductReleasePackage.ReadIdentity(packagePath);
            ProductReleaseManifest confirmedManifest = ProductReleasePackage.Validate(packagePath);
            ProductReleasePackage.VerifyPublisher(packagePath, publicKeyPaths);
            (long confirmedLength, byte[] confirmedDigest) =
                ProductReleasePackage.ReadIdentity(packagePath);
            if (!ManifestEquals(manifest, confirmedManifest) || length != confirmedLength ||
                !CryptographicOperations.FixedTimeEquals(digest, confirmedDigest))
            {
                throw new IOException("A release package changed while creating the catalog.");
            }

            if (!releaseIdentities.Add((manifest.TargetRuntime, manifest.ReleaseSequence)))
            {
                throw new InvalidDataException(
                    "The release catalog contains a duplicate runtime and sequence.");
            }

            Uri packageUri = new(options.PackageBaseUri, Uri.EscapeDataString(fileName));
            if (!ProductReleaseUri.IsValidHttps(packageUri) ||
                packageUri.AbsoluteUri.Length > ProductReleaseUri.MaximumLength)
            {
                throw new InvalidDataException("A release package URI is invalid.");
            }

            entries.Add(new ProductReleaseCatalogEntry(manifest.ReleaseSequence,
                manifest.ReleaseVersion, manifest.MinimumLauncherVersion,
                manifest.TargetRuntime, packageUri, length, digest));
        }

        entries.Sort(static (left, right) =>
        {
            int runtime = string.CompareOrdinal(left.TargetRuntime, right.TargetRuntime);
            return runtime != 0 ? runtime : left.ReleaseSequence.CompareTo(right.ReleaseSequence);
        });

        ProductStagingPathGuard.PrepareFileOutput(outputPath);
        string temporaryPath = outputPath + $".tmp-{Guid.NewGuid():N}";
        bool completed = false;
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None))
            using (Utf8JsonWriter writer = new(stream,
                       new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
                writer.WriteString("productId", ExpectedProductId);
                writer.WriteString("expiresUtc", expirationText);
                writer.WriteStartArray("releases");
                foreach (ProductReleaseCatalogEntry entry in entries)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("releaseSequence", entry.ReleaseSequence);
                    writer.WriteString("releaseVersion", entry.ReleaseVersion);
                    writer.WriteNumber("minimumLauncherVersion",
                        entry.MinimumLauncherVersion);
                    writer.WriteString("targetRuntime", entry.TargetRuntime);
                    writer.WriteString("packageUri", entry.PackageUri.AbsoluteUri);
                    writer.WriteNumber("packageLength", entry.PackageLength);
                    writer.WriteString("packageSha256",
                        Convert.ToHexStringLower(entry.PackageSha256));
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            byte[] bytes = ProductReleaseManifest.ReadRegularFile(temporaryPath, MaximumLength);
            _ = Read(bytes);
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

    public static IReadOnlyList<ProductReleaseCatalogEntry> Read(
        ReadOnlyMemory<byte> catalogBytes)
    {
        if (catalogBytes.IsEmpty || catalogBytes.Length > MaximumLength)
        {
            throw new InvalidDataException("The release catalog length is invalid.");
        }

        using JsonDocument document = JsonDocument.Parse(catalogBytes,
            new JsonDocumentOptions { MaxDepth = 8 });
        JsonElement root = document.RootElement;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (root.ValueKind != JsonValueKind.Object ||
            !HasExactProperties(root, "schemaVersion", "productId", "expiresUtc", "releases") ||
            !root.TryGetProperty("schemaVersion", out JsonElement schema) ||
            !schema.TryGetInt32(out int schemaVersion) ||
            schemaVersion != CurrentSchemaVersion ||
            !root.TryGetProperty("productId", out JsonElement productId) ||
            productId.ValueKind != JsonValueKind.String ||
            !string.Equals(productId.GetString(), ExpectedProductId,
                StringComparison.Ordinal) ||
            !root.TryGetProperty("expiresUtc", out JsonElement expiresElement) ||
            expiresElement.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParseExact(expiresElement.GetString(), TimestampFormat,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal |
                System.Globalization.DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset expiresAt) || expiresAt <= now ||
            expiresAt - now > MaximumRemainingLifetime ||
            !root.TryGetProperty("releases", out JsonElement releasesElement) ||
            releasesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The release catalog is invalid.");
        }

        List<ProductReleaseCatalogEntry> releases = [];
        HashSet<(string Runtime, ulong Sequence)> identities = [];
        foreach (JsonElement entry in releasesElement.EnumerateArray())
        {
            if (releases.Count == MaximumReleaseCount ||
                !TryReadEntry(entry, out ProductReleaseCatalogEntry? release) ||
                release is null ||
                !identities.Add((release.TargetRuntime, release.ReleaseSequence)))
            {
                throw new InvalidDataException("The release catalog is invalid.");
            }

            releases.Add(release);
        }

        if (releases.Count == 0)
        {
            throw new InvalidDataException("The release catalog is empty.");
        }

        return releases;
    }

    private static bool TryReadEntry(
        JsonElement entry,
        out ProductReleaseCatalogEntry? release)
    {
        release = null;
        if (entry.ValueKind != JsonValueKind.Object || !HasExactProperties(entry,
                "releaseSequence", "releaseVersion", "minimumLauncherVersion",
                "targetRuntime", "packageUri", "packageLength", "packageSha256") ||
            !entry.TryGetProperty("releaseSequence", out JsonElement sequenceElement) ||
            !sequenceElement.TryGetUInt64(out ulong sequence) || sequence == 0 ||
            !entry.TryGetProperty("releaseVersion", out JsonElement versionElement) ||
            versionElement.ValueKind != JsonValueKind.String ||
            versionElement.GetString() is not { } version ||
            !IsValidVersion(version) ||
            !entry.TryGetProperty("minimumLauncherVersion", out JsonElement launcherElement) ||
            !launcherElement.TryGetInt32(out int minimumLauncherVersion) ||
            minimumLauncherVersion <= 0 ||
            !entry.TryGetProperty("targetRuntime", out JsonElement runtimeElement) ||
            runtimeElement.ValueKind != JsonValueKind.String ||
            runtimeElement.GetString() is not { } runtime ||
            !ProductTargetRuntime.IsSupported(runtime) ||
            !entry.TryGetProperty("packageUri", out JsonElement uriElement) ||
            uriElement.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(uriElement.GetString(), UriKind.Absolute, out Uri? packageUri) ||
            !ProductReleaseUri.IsValidHttps(packageUri) ||
            !entry.TryGetProperty("packageLength", out JsonElement lengthElement) ||
            !lengthElement.TryGetInt64(out long packageLength) ||
            packageLength is <= 0 or > ProductReleasePackage.MaximumPackageLength ||
            !entry.TryGetProperty("packageSha256", out JsonElement digestElement) ||
            digestElement.ValueKind != JsonValueKind.String ||
            !TryReadDigest(digestElement.GetString(), out byte[]? digest))
        {
            return false;
        }

        release = new ProductReleaseCatalogEntry(sequence, version, minimumLauncherVersion,
            runtime, packageUri, packageLength, digest!);
        return true;
    }

    private static bool HasExactProperties(JsonElement element, params string[] names)
    {
        HashSet<string> properties = [];
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!properties.Add(property.Name))
            {
                return false;
            }
        }

        return properties.SetEquals(names);
    }

    private static bool TryReadDigest(string? value, out byte[]? digest)
    {
        digest = null;
        if (value is not { Length: 64 } || value.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            return false;
        }

        digest = Convert.FromHexString(value);
        return true;
    }

    private static bool IsValidPackageFileName(string name)
    {
        return name.Length is > 0 and <= MaximumPackageFileNameLength &&
            name.All(character => char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '-' or '_') &&
            name[0] != '.' && name[^1] != '.';
    }

    private static List<string> ValidatePublicKeys(
        IReadOnlyList<string> publicKeyPaths,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(publicKeyPaths);
        if (publicKeyPaths.Count is 0 or > ProductReleaseTrust.MaximumKeyCount)
        {
            throw new ArgumentException("The release publisher-key count is invalid.",
                nameof(publicKeyPaths));
        }

        List<string> normalizedPaths = [];
        HashSet<string> keyIds = new(StringComparer.Ordinal);
        foreach (string path in publicKeyPaths)
        {
            string normalized = ProductStagingPathGuard.RequireRegularFile(
                path, "release public key");
            if (string.Equals(normalized, outputPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The release catalog output must not replace a public key.");
            }

            byte[] publicKey = ProductReleaseSignature.ExportSubjectPublicKeyInfo(normalized);
            if (!keyIds.Add(ProductReleaseSignature.GetKeyId(publicKey)))
            {
                throw new InvalidDataException(
                    "The release catalog publisher keys contain a duplicate.");
            }

            normalizedPaths.Add(normalized);
        }

        return normalizedPaths;
    }

    private static bool IsValidVersion(string version)
    {
        return version.Length is > 0 and <= 64 && char.IsAsciiLetterOrDigit(version[0]) &&
            char.IsAsciiLetterOrDigit(version[^1]) &&
            version.All(character => char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '-');
    }

    private static bool ManifestEquals(
        ProductReleaseManifest left,
        ProductReleaseManifest right)
    {
        if (left.SchemaVersion != right.SchemaVersion || left.ProductId != right.ProductId ||
            left.ReleaseSequence != right.ReleaseSequence ||
            left.ReleaseVersion != right.ReleaseVersion ||
            left.MinimumLauncherVersion != right.MinimumLauncherVersion ||
            left.TargetRuntime != right.TargetRuntime ||
            left.ClientExecutable != right.ClientExecutable ||
            left.Files.Count != right.Files.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Files.Count; index++)
        {
            ProductReleaseFile leftFile = left.Files[index];
            ProductReleaseFile rightFile = right.Files[index];
            if (leftFile.Path != rightFile.Path || leftFile.Length != rightFile.Length ||
                !CryptographicOperations.FixedTimeEquals(leftFile.Sha256, rightFile.Sha256))
            {
                return false;
            }
        }

        return true;
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
