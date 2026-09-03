using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tool;

internal static class Program
{
    private const int InvalidArgumentsExitCode = 2;

    private static readonly string[] s_importedFamilies = ["ini", "data", "ani"];

    private static int Main(string[] args)
    {
        if (TryParseImportArguments(args, out string? sourceRoot, out string? destinationRoot))
        {
            RetailContentImporter.Import(sourceRoot, destinationRoot, s_importedFamilies);
            return 0;
        }

        if (TryParseValidationArguments(args, out string? contentRoot))
        {
            ValidateStartup(contentRoot);
            return 0;
        }

        if (TryParseVerificationArguments(args, out string? contentSetRoot))
        {
            ContentSetVerifier.Verify(contentSetRoot);
            return 0;
        }

        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  OpenConquer.Content.Tool import-retail-5517 --source <retail-root> --destination <content-set-root>");
        Console.Error.WriteLine("  OpenConquer.Content.Tool validate-startup --content-root <content-root>");
        Console.Error.WriteLine("  OpenConquer.Content.Tool verify-content-set --content-set <content-set-root>");
        return InvalidArgumentsExitCode;
    }

    private static bool TryParseImportArguments(string[] args, out string sourceRoot, out string destinationRoot)
    {
        sourceRoot = string.Empty;
        destinationRoot = string.Empty;

        if (args.Length != 5 || !string.Equals(args[0], "import-retail-5517", StringComparison.Ordinal))
        {
            return false;
        }

        for (int index = 1; index < args.Length; index += 2)
        {
            string value = args[index + 1];

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            switch (args[index])
            {
                case "--source" when sourceRoot.Length == 0:
                    sourceRoot = Path.GetFullPath(value);
                    break;

                case "--destination" when destinationRoot.Length == 0:
                    destinationRoot = Path.GetFullPath(value);
                    break;

                default:
                    return false;
            }
        }

        return sourceRoot.Length > 0 && destinationRoot.Length > 0;
    }

    private static bool TryParseValidationArguments(string[] args, out string contentRoot)
    {
        contentRoot = string.Empty;

        if (args.Length != 3
            || !string.Equals(args[0], "validate-startup", StringComparison.Ordinal)
            || !string.Equals(args[1], "--content-root", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(args[2]))
        {
            return false;
        }

        contentRoot = Path.GetFullPath(args[2]);
        return true;
    }

    private static bool TryParseVerificationArguments(string[] args, out string contentSetRoot)
    {
        contentSetRoot = string.Empty;

        if (args.Length != 3
            || !string.Equals(args[0], "verify-content-set", StringComparison.Ordinal)
            || !string.Equals(args[1], "--content-set", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(args[2]))
        {
            return false;
        }

        contentSetRoot = Path.GetFullPath(args[2]);
        return true;
    }

    private static void ValidateStartup(string contentRoot)
    {
        RetailClientContentSource source = RetailClientContentSource.Open(contentRoot);
        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(source);
        StartupLogo firstLogo = StartupLogo.Load(source, monotonicTickMilliseconds: 0);
        StartupLogo secondLogo = StartupLogo.Load(source, monotonicTickMilliseconds: 1);

        Console.WriteLine($"Screen mode: {gameSetup.ScreenMode} ({gameSetup.LogicalWidthPixels}x{gameSetup.LogicalHeightPixels})");
        Console.WriteLine($"Startup logo 1: {firstLogo.ContentPath} ({firstLogo.Image.Width}x{firstLogo.Image.Height})");
        Console.WriteLine($"Startup logo 2: {secondLogo.ContentPath} ({secondLogo.Image.Width}x{secondLogo.Image.Height})");

        if (source.MissingPackageNames.Count > 0)
        {
            Console.WriteLine($"Declared packages not present in this content root: {string.Join(", ", source.MissingPackageNames)}");
        }
    }
}

internal static class RetailContentImporter
{
    private const string ExpectedRetailVersion = "5517";
    private const string ManifestFileName = "manifest.json";
    private const string PayloadDirectoryName = "payload";

    public static void Import(string sourceRoot, string destinationRoot, IReadOnlyList<string> families)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        ArgumentNullException.ThrowIfNull(families);

        ValidateDirectory(sourceRoot, "retail source root");

        if (Directory.Exists(destinationRoot) || File.Exists(destinationRoot))
        {
            throw new IOException($"Content-set destination '{destinationRoot}' already exists.");
        }

        string destinationParent = Path.GetDirectoryName(destinationRoot)
            ?? throw new ArgumentException("Destination must have a parent directory.", nameof(destinationRoot));

        Directory.CreateDirectory(destinationParent);

        string stagingRoot = Path.Combine(
            destinationParent,
            $".{Path.GetFileName(destinationRoot)}.import-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(stagingRoot);

        try
        {
            SourceIdentity sourceIdentity = ReadSourceIdentity(sourceRoot);
            List<ManifestEntry> entries = EnumerateSourceEntries(sourceRoot, families);
            RejectCaseInsensitiveCollisions(entries);

            string payloadRoot = Path.Combine(stagingRoot, PayloadDirectoryName);

            foreach (ManifestEntry entry in entries)
            {
                CopyAndHash(sourceRoot, payloadRoot, entry);
            }

            WriteManifest(
                Path.Combine(stagingRoot, ManifestFileName),
                sourceIdentity,
                families,
                entries
            );

            Directory.Move(stagingRoot, destinationRoot);
        }
        catch
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }

            throw;
        }
    }

    private static SourceIdentity ReadSourceIdentity(string sourceRoot)
    {
        string versionPath = Path.Combine(sourceRoot, "version.dat");
        FileInfo versionFile = ValidateFile(versionPath, "retail version marker");

        if (versionFile.Length != ExpectedRetailVersion.Length)
        {
            throw new InvalidDataException("The retail version marker is not the expected four-byte 5517 value.");
        }

        byte[] bytes = File.ReadAllBytes(versionPath);
        string version = System.Text.Encoding.ASCII.GetString(bytes);

        if (!string.Equals(version, ExpectedRetailVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Expected retail version {ExpectedRetailVersion}, but found '{version}'.");
        }

        return new SourceIdentity(version, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    private static List<ManifestEntry> EnumerateSourceEntries(
        string sourceRoot,
        IReadOnlyList<string> families)
    {
        List<ManifestEntry> entries = [];

        foreach (string family in families)
        {
            if (family.Length == 0 || family.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException($"Invalid content family '{family}'.", nameof(families));
            }

            string familyRoot = Path.Combine(sourceRoot, family);
            ValidateDirectory(familyRoot, $"'{family}' content family");
            EnumerateDirectory(sourceRoot, familyRoot, family, entries);
        }

        entries.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.SourcePath, right.SourcePath));
        return entries;
    }

    private static void EnumerateDirectory(
        string sourceRoot,
        string directoryPath,
        string family,
        List<ManifestEntry> entries)
    {
        ValidateDirectory(directoryPath, "content directory");

        foreach (string childDirectory in Directory.EnumerateDirectories(directoryPath).Order(StringComparer.Ordinal))
        {
            EnumerateDirectory(sourceRoot, childDirectory, family, entries);
        }

        foreach (string filePath in Directory.EnumerateFiles(directoryPath).Order(StringComparer.Ordinal))
        {
            FileInfo file = ValidateFile(filePath, "content file");
            string sourcePath = Path.GetRelativePath(sourceRoot, file.FullName).Replace('\\', '/');

            entries.Add(new ManifestEntry(
                sourcePath,
                sourcePath.ToLowerInvariant(),
                family,
                file.Length,
                ClassifySignature(file.FullName),
                ClassifyDisposition(sourcePath)
            ));
        }
    }

    private static void RejectCaseInsensitiveCollisions(IEnumerable<ManifestEntry> entries)
    {
        Dictionary<string, string> sourcePathByKey = new(StringComparer.Ordinal);

        foreach (ManifestEntry entry in entries)
        {
            if (sourcePathByKey.TryGetValue(entry.PathKey, out string? existingPath))
            {
                throw new InvalidDataException($"Retail paths '{existingPath}' and '{entry.SourcePath}' collide case-insensitively.");
            }

            sourcePathByKey.Add(entry.PathKey, entry.SourcePath);
        }
    }

    private static void CopyAndHash(string sourceRoot, string payloadRoot, ManifestEntry entry)
    {
        string relativeSystemPath = entry.SourcePath.Replace('/', Path.DirectorySeparatorChar);
        string sourcePath = Path.Combine(sourceRoot, relativeSystemPath);
        string destinationPath = Path.Combine(payloadRoot, relativeSystemPath);
        string? destinationDirectory = Path.GetDirectoryName(destinationPath);

        if (destinationDirectory is not null)
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        using FileStream source = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            FileOptions.SequentialScan
        );

        using FileStream destination = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            FileOptions.SequentialScan
        );

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[1024 * 1024];
        long copiedLength = 0;

        while (true)
        {
            int bytesRead = source.Read(buffer);

            if (bytesRead == 0)
            {
                break;
            }

            destination.Write(buffer, 0, bytesRead);
            hash.AppendData(buffer, 0, bytesRead);
            copiedLength = checked(copiedLength + bytesRead);
        }

        if (copiedLength != entry.Length)
        {
            throw new IOException($"Retail file '{entry.SourcePath}' changed length during import.");
        }

        entry.Sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void WriteManifest(
        string manifestPath,
        SourceIdentity sourceIdentity,
        IReadOnlyList<string> families,
        IReadOnlyList<ManifestEntry> entries)
    {
        using FileStream stream = new(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteString("sourceSet", "retail-5517");
        writer.WriteString("retailVersion", sourceIdentity.Version);
        writer.WriteString("versionMarkerSha256", sourceIdentity.VersionMarkerSha256);

        writer.WriteStartArray("families");

        foreach (string family in families)
        {
            ManifestEntry[] familyEntries = entries.Where(entry => entry.Family == family).ToArray();

            writer.WriteStartObject();
            writer.WriteString("name", family);
            writer.WriteNumber("fileCount", familyEntries.Length);
            writer.WriteNumber("length", familyEntries.Sum(entry => entry.Length));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteNumber("fileCount", entries.Count);
        writer.WriteNumber("length", entries.Sum(entry => entry.Length));
        writer.WriteStartArray("entries");

        foreach (ManifestEntry entry in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("sourcePath", entry.SourcePath);
            writer.WriteString("pathKey", entry.PathKey);
            writer.WriteString("family", entry.Family);
            writer.WriteNumber("length", entry.Length);
            writer.WriteString("sha256", entry.Sha256);
            writer.WriteString("signature", entry.Signature);
            writer.WriteString("disposition", entry.Disposition);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        stream.WriteByte((byte)'\n');
    }

    private static string ClassifySignature(string filePath)
    {
        Span<byte> header = stackalloc byte[12];

        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        int length = stream.Read(header);
        ReadOnlySpan<byte> bytes = header[..length];

        if (bytes.StartsWith("BM"u8))
        {
            return "bmp";
        }

        if (bytes.StartsWith("DDS "u8))
        {
            return "dds";
        }

        if (bytes.StartsWith("RIFF"u8))
        {
            return "riff";
        }

        if (bytes.StartsWith("FWS"u8) || bytes.StartsWith("CWS"u8) || bytes.StartsWith("ZWS"u8))
        {
            return "swf";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "jpeg";
        }

        return "unknown";
    }

    private static string ClassifyDisposition(string sourcePath)
    {
        if (sourcePath.EndsWith("/Thumbs.db", StringComparison.OrdinalIgnoreCase)
            || sourcePath.EndsWith("/.DS_Store", StringComparison.OrdinalIgnoreCase))
        {
            return "excluded-host-artifact";
        }

        if (sourcePath.StartsWith("data/AutoPatch_pic/", StringComparison.OrdinalIgnoreCase))
        {
            return "source-only-launcher";
        }

        if (sourcePath.EndsWith(".swf", StringComparison.OrdinalIgnoreCase))
        {
            return "source-only-legacy-flash";
        }

        return "retained";
    }

    private static void ValidateDirectory(string path, string description)
    {
        DirectoryInfo directory = new(path);

        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"The {description} '{path}' does not exist.");
        }

        if (directory.LinkTarget is not null || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"The {description} '{path}' is a symbolic link or reparse point.");
        }
    }

    private static FileInfo ValidateFile(string path, string description)
    {
        FileInfo file = new(path);

        if (!file.Exists)
        {
            throw new FileNotFoundException($"The {description} '{path}' does not exist.", path);
        }

        if (file.LinkTarget is not null || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"The {description} '{path}' is a symbolic link or reparse point.");
        }

        return file;
    }

    private sealed class ManifestEntry(
        string sourcePath,
        string pathKey,
        string family,
        long length,
        string signature,
        string disposition)
    {
        public string SourcePath { get; } = sourcePath;
        public string PathKey { get; } = pathKey;
        public string Family { get; } = family;
        public long Length { get; } = length;
        public string Signature { get; } = signature;
        public string Disposition { get; } = disposition;
        public string Sha256 { get; set; } = string.Empty;
    }

    private readonly record struct SourceIdentity(string Version, string VersionMarkerSha256);
}

internal static class ContentSetVerifier
{
    private const int MaximumManifestLength = 64 * 1024 * 1024;

    public static void Verify(string contentSetRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSetRoot);

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentSetRoot));
        ValidateDirectory(root);

        string manifestPath = Path.Combine(root, "manifest.json");
        FileInfo manifestFile = new(manifestPath);

        if (!manifestFile.Exists || manifestFile.Length > MaximumManifestLength)
        {
            throw new InvalidDataException("The content-set manifest is missing or exceeds its size limit.");
        }

        using FileStream manifestStream = File.OpenRead(manifestPath);
        using JsonDocument document = JsonDocument.Parse(manifestStream);
        JsonElement manifest = document.RootElement;

        if (manifest.GetProperty("schemaVersion").GetInt32() != 1
            || !string.Equals(manifest.GetProperty("sourceSet").GetString(), "retail-5517", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The content-set manifest has an unsupported identity or schema version.");
        }

        string payloadRoot = Path.Combine(root, "payload");
        ValidateDirectory(payloadRoot);
        Dictionary<string, ManifestIdentity> expectedByPath = new(StringComparer.Ordinal);

        foreach (JsonElement encodedEntry in manifest.GetProperty("entries").EnumerateArray())
        {
            string sourcePath = encodedEntry.GetProperty("sourcePath").GetString()
                ?? throw new InvalidDataException("A manifest entry has no source path.");
            string pathKey = encodedEntry.GetProperty("pathKey").GetString()
                ?? throw new InvalidDataException($"Manifest entry '{sourcePath}' has no path key.");
            long length = encodedEntry.GetProperty("length").GetInt64();
            string sha256 = encodedEntry.GetProperty("sha256").GetString()
                ?? throw new InvalidDataException($"Manifest entry '{sourcePath}' has no SHA-256 value.");

            ValidateRelativePath(sourcePath);

            if (!string.Equals(pathKey, sourcePath.ToLowerInvariant(), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Manifest entry '{sourcePath}' has an invalid path key.");
            }

            if (!expectedByPath.TryAdd(sourcePath, new ManifestIdentity(length, sha256)))
            {
                throw new InvalidDataException($"Manifest contains duplicate source path '{sourcePath}'.");
            }
        }

        int declaredFileCount = manifest.GetProperty("fileCount").GetInt32();
        long declaredLength = manifest.GetProperty("length").GetInt64();

        if (declaredFileCount != expectedByPath.Count
            || declaredLength != expectedByPath.Values.Sum(identity => identity.Length))
        {
            throw new InvalidDataException("The content-set manifest summary does not match its entries.");
        }

        HashSet<string> actualPaths = [];
        VerifyDirectory(payloadRoot, payloadRoot, expectedByPath, actualPaths);

        string[] missingPaths = expectedByPath.Keys.Except(actualPaths, StringComparer.Ordinal).Take(4).ToArray();

        if (missingPaths.Length > 0)
        {
            throw new InvalidDataException($"Content set is missing manifest payload(s): {string.Join(", ", missingPaths)}.");
        }

        Console.WriteLine($"Verified {expectedByPath.Count} files ({declaredLength} bytes) for retail-5517.");
    }

    private static void VerifyDirectory(
        string payloadRoot,
        string directoryPath,
        IReadOnlyDictionary<string, ManifestIdentity> expectedByPath,
        ISet<string> actualPaths)
    {
        ValidateDirectory(directoryPath);

        foreach (string childDirectory in Directory.EnumerateDirectories(directoryPath).Order(StringComparer.Ordinal))
        {
            VerifyDirectory(payloadRoot, childDirectory, expectedByPath, actualPaths);
        }

        foreach (string filePath in Directory.EnumerateFiles(directoryPath).Order(StringComparer.Ordinal))
        {
            FileInfo file = new(filePath);

            if (file.LinkTarget is not null || (file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Content-set payload '{filePath}' is a symbolic link or reparse point.");
            }

            string sourcePath = Path.GetRelativePath(payloadRoot, filePath).Replace('\\', '/');

            if (!expectedByPath.TryGetValue(sourcePath, out ManifestIdentity expected))
            {
                throw new InvalidDataException($"Content-set payload '{sourcePath}' is not declared in the manifest.");
            }

            if (file.Length != expected.Length)
            {
                throw new InvalidDataException($"Content-set payload '{sourcePath}' has an unexpected length.");
            }

            using FileStream stream = File.OpenRead(filePath);
            string actualHash = Convert.ToHexStringLower(SHA256.HashData(stream));

            if (!string.Equals(actualHash, expected.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Content-set payload '{sourcePath}' failed SHA-256 verification.");
            }

            actualPaths.Add(sourcePath);
        }
    }

    private static void ValidateRelativePath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)
            || sourcePath[0] is '/' or '\\'
            || sourcePath.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Manifest source path '{sourcePath}' is invalid.");
        }

        string[] segments = sourcePath.Split('/');

        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Contains(':', StringComparison.Ordinal)))
        {
            throw new InvalidDataException($"Manifest source path '{sourcePath}' is invalid.");
        }
    }

    private static void ValidateDirectory(string path)
    {
        DirectoryInfo directory = new(path);

        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Content-set directory '{path}' does not exist.");
        }

        if (directory.LinkTarget is not null || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Content-set directory '{path}' is a symbolic link or reparse point.");
        }
    }

    private readonly record struct ManifestIdentity(long Length, string Sha256);
}
