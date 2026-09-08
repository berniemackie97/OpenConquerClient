using System.Security.Cryptography;
using System.Text.Json;

namespace OpenConquer.Product.Tool;

internal sealed record ProductReleaseFile(string Path, long Length, byte[] Sha256);

/// <summary>Creates and validates deterministic signed-release payloads.</summary>
internal sealed record ProductReleaseManifest(int SchemaVersion, string ProductId, ulong ReleaseSequence, string ReleaseVersion, int MinimumLauncherVersion, string TargetRuntime, string ClientExecutable, IReadOnlyList<ProductReleaseFile> Files)
{
    public const int CurrentSchemaVersion = 1;
    public const string ExpectedProductId = "OpenConquer";
    public const string FileName = "openconquer.release.json";
    public const int MaximumFileCount = 16 * 1024;

    private const int MaximumLength = 8 * 1024 * 1024;
    private const int MaximumVersionLength = 64;
    private const int MaximumDirectoryCount = 16 * 1024;

    public static void Create(ReleaseManifestOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string clientRoot = ProductStagingPathGuard.RequireDirectory(options.ClientPublishPath, nameof(options.ClientPublishPath));

        if (!ProductTargetRuntime.IsSupported(options.TargetRuntime) || !IsValidVersion(options.ReleaseVersion) || options.ReleaseSequence == 0 || options.MinimumLauncherVersion <= 0)
        {
            throw new ArgumentException("Release identity and compatibility values are invalid.", nameof(options));
        }

        string outputPath = ProductStagingPathGuard.NormalizePath(options.OutputPath, nameof(options.OutputPath));

        ProductStagingPathGuard.RejectPathWithinRoot(clientRoot, outputPath, "Release manifest output must remain outside the client publish.");

        ProductStagingPathGuard.PrepareFileOutput(outputPath);

        string executable = ProductTargetRuntime.ClientExecutable(options.TargetRuntime);

        List<ProductReleaseFile> files = ReadClientFiles(clientRoot);

        if (!files.Any(file => string.Equals(file.Path, executable, StringComparison.Ordinal)))
        {
            throw new InvalidDataException($"The client publish does not contain required executable '{executable}'.");
        }

        string temporaryPath = outputPath + $".tmp-{Guid.NewGuid():N}";
        bool completed = false;

        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
                writer.WriteString("productId", ExpectedProductId);
                writer.WriteNumber("releaseSequence", options.ReleaseSequence);
                writer.WriteString("releaseVersion", options.ReleaseVersion);
                writer.WriteNumber("minimumLauncherVersion", options.MinimumLauncherVersion);
                writer.WriteString("targetRuntime", options.TargetRuntime);
                writer.WriteString("clientExecutable", executable);

                writer.WriteStartArray("files");

                foreach (ProductReleaseFile file in files)
                {
                    writer.WriteStartObject();
                    writer.WriteString("path", file.Path);
                    writer.WriteNumber("length", file.Length);
                    writer.WriteString("sha256", Convert.ToHexStringLower(file.Sha256));
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.Flush();

                stream.Flush(flushToDisk: true);
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

    public static ProductReleaseManifest Read(string path)
    {
        byte[] bytes = ReadRegularFile(path, MaximumLength);

        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });

        if (!TryRead(document.RootElement, out ProductReleaseManifest? manifest) || manifest is null)
        {
            throw new InvalidDataException("The release manifest is invalid.");
        }

        return manifest;
    }

    public static void VerifyClient(string clientRoot, ProductReleaseManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientRoot);
        ArgumentNullException.ThrowIfNull(manifest);

        List<ProductReleaseFile> actual = ReadClientFiles(clientRoot);

        if (actual.Count != manifest.Files.Count)
        {
            throw new InvalidDataException("The client publish does not match the release manifest.");
        }

        for (int index = 0; index < actual.Count; index++)
        {
            ProductReleaseFile expectedFile = manifest.Files[index];
            ProductReleaseFile actualFile = actual[index];

            if (!string.Equals(expectedFile.Path, actualFile.Path, StringComparison.Ordinal) || expectedFile.Length != actualFile.Length || !CryptographicOperations.FixedTimeEquals(expectedFile.Sha256, actualFile.Sha256))
            {
                throw new InvalidDataException("The client publish does not match the release manifest.");
            }
        }
    }

    internal static byte[] ReadRegularFile(string path, int maximumLength)
    {
        string normalized = ProductStagingPathGuard.RequireRegularFile(path, "release metadata");

        using FileStream stream = new(normalized, FileMode.Open, FileAccess.Read, FileShare.Read);

        if (stream.Length is <= 0 || stream.Length > maximumLength)
        {
            throw new InvalidDataException("The release metadata length is invalid.");
        }

        byte[] bytes = new byte[(int)stream.Length];

        stream.ReadExactly(bytes);

        if (stream.Length != bytes.Length)
        {
            throw new IOException("Release metadata changed while it was being read.");
        }

        return bytes;
    }

    private static List<ProductReleaseFile> ReadClientFiles(string clientRoot)
    {
        List<ProductReleaseFile> files = [];
        HashSet<string> portablePaths = new(StringComparer.Ordinal);
        Stack<DirectoryInfo> pending = new();

        DirectoryInfo root = new(clientRoot);
        root.Refresh();

        if (!root.Exists || root.LinkTarget is not null || (root.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"Linked or unavailable directories are not allowed in a release: '{root.FullName}'.");
        }

        if ((root.Attributes & FileAttributes.Device) != 0)
        {
            throw new InvalidDataException("The client publish contains unsupported or excessive entries.");
        }

        pending.Push(root);

        int directoryCount = 1;

        while (pending.TryPop(out DirectoryInfo? directory))
        {
            directory.Refresh();

            if (!directory.Exists || directory.LinkTarget is not null || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"Linked or unavailable directories are not allowed in a release: '{directory.FullName}'.");
            }

            if ((directory.Attributes & FileAttributes.Device) != 0)
            {
                throw new InvalidDataException("The client publish contains unsupported or excessive entries.");
            }

            foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
            {
                entry.Refresh();

                if (entry.LinkTarget is not null || (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException($"Linked paths are not allowed in a release: '{entry.FullName}'.");
                }

                if ((entry.Attributes & FileAttributes.Device) != 0)
                {
                    throw new InvalidDataException("The client publish contains unsupported or excessive entries.");
                }

                if (entry is DirectoryInfo child)
                {
                    if (directoryCount == MaximumDirectoryCount)
                    {
                        throw new InvalidDataException("The client directory count exceeds the release limit.");
                    }

                    directoryCount++;
                    pending.Push(child);
                    continue;
                }

                if (entry is not FileInfo file || (entry.Attributes & FileAttributes.Directory) != 0 || files.Count == MaximumFileCount)
                {
                    throw new InvalidDataException("The client publish contains unsupported or excessive entries.");
                }

                string relativePath = Path.GetRelativePath(clientRoot, file.FullName).Replace(Path.DirectorySeparatorChar, '/');

                if (!ProductReleasePath.IsValid(relativePath) || !portablePaths.Add(ProductReleasePath.PortableIdentity(relativePath)))
                {
                    throw new InvalidDataException($"The client publish contains an invalid or ambiguous path: '{relativePath}'.");
                }

                using FileStream stream = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 128 * 1024, FileOptions.SequentialScan);

                FileAttributes openedAttributes = File.GetAttributes(stream.SafeFileHandle);

                if ((openedAttributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0)
                {
                    throw new InvalidDataException($"The client publish contains an unsupported file: '{relativePath}'.");
                }

                long length = stream.Length;
                byte[] hash = SHA256.HashData(stream);

                if (stream.Length != length)
                {
                    throw new IOException($"Client file changed while reading: '{relativePath}'.");
                }

                FileAttributes finalAttributes = File.GetAttributes(file.FullName);

                if ((finalAttributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0)
                {
                    throw new IOException($"Client file changed while reading: '{relativePath}'.");
                }

                files.Add(new ProductReleaseFile(relativePath, length, hash));
            }
        }

        files.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));

        if (files.Count == 0)
        {
            throw new InvalidDataException("The client publish is empty.");
        }

        return files;
    }

    private static bool TryRead(JsonElement root, out ProductReleaseManifest? manifest)
    {
        manifest = null;

        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 8)
        {
            return false;
        }

        int? schemaVersion = null;
        string? productId = null;
        ulong? releaseSequence = null;
        string? releaseVersion = null;
        int? minimumLauncherVersion = null;
        string? targetRuntime = null;
        string? clientExecutable = null;
        List<ProductReleaseFile>? files = null;

        HashSet<string> properties = new(StringComparer.Ordinal);

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!properties.Add(property.Name))
            {
                return false;
            }

            switch (property.Name)
            {
                case "schemaVersion" when property.Value.TryGetInt32(out int value):
                    schemaVersion = value;
                    break;

                case "productId" when property.Value.ValueKind == JsonValueKind.String:
                    productId = property.Value.GetString();
                    break;

                case "releaseSequence" when property.Value.TryGetUInt64(out ulong value):
                    releaseSequence = value;
                    break;

                case "releaseVersion" when property.Value.ValueKind == JsonValueKind.String:
                    releaseVersion = property.Value.GetString();
                    break;

                case "minimumLauncherVersion" when property.Value.TryGetInt32(out int value):
                    minimumLauncherVersion = value;
                    break;

                case "targetRuntime" when property.Value.ValueKind == JsonValueKind.String:
                    targetRuntime = property.Value.GetString();
                    break;

                case "clientExecutable" when property.Value.ValueKind == JsonValueKind.String:
                    clientExecutable = property.Value.GetString();
                    break;

                case "files" when TryReadFiles(property.Value, out files):
                    break;

                default:
                    return false;
            }
        }

        if (schemaVersion != CurrentSchemaVersion || productId is null || !string.Equals(productId, ExpectedProductId, StringComparison.Ordinal)
            || releaseSequence is null or 0 || releaseVersion is null || !IsValidVersion(releaseVersion) || minimumLauncherVersion is null or <= 0
            || targetRuntime is null || !ProductTargetRuntime.IsSupported(targetRuntime) || clientExecutable is null || !ProductReleasePath.IsValid(clientExecutable)
            || files is not { Count: > 0 } || !files.Any(file => string.Equals(file.Path, clientExecutable, StringComparison.Ordinal)))
        {
            return false;
        }

        manifest = new ProductReleaseManifest(schemaVersion.Value, productId, releaseSequence.Value, releaseVersion, minimumLauncherVersion.Value, targetRuntime, clientExecutable, files);

        return true;
    }

    private static bool TryReadFiles(JsonElement element, out List<ProductReleaseFile>? files)
    {
        files = null;

        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        List<ProductReleaseFile> parsed = [];
        HashSet<string> portablePaths = new(StringComparer.Ordinal);

        string? previous = null;

        foreach (JsonElement item in element.EnumerateArray())
        {
            if (parsed.Count == MaximumFileCount || !TryReadFile(item, out ProductReleaseFile? file) || file is null
                || !portablePaths.Add(ProductReleasePath.PortableIdentity(file.Path)) || previous is not null && string.CompareOrdinal(previous, file.Path) >= 0)
            {
                return false;
            }

            parsed.Add(file);
            previous = file.Path;
        }

        files = parsed;

        return true;
    }

    private static bool TryReadFile(JsonElement item, out ProductReleaseFile? file)
    {
        file = null;

        if (item.ValueKind != JsonValueKind.Object || item.EnumerateObject().Count() != 3)
        {
            return false;
        }

        string? path = null;
        long? length = null;
        string? hash = null;

        HashSet<string> properties = new(StringComparer.Ordinal);

        foreach (JsonProperty property in item.EnumerateObject())
        {
            if (!properties.Add(property.Name))
            {
                return false;
            }

            switch (property.Name)
            {
                case "path" when property.Value.ValueKind == JsonValueKind.String:
                    path = property.Value.GetString();
                    break;

                case "length" when property.Value.TryGetInt64(out long value):
                    length = value;
                    break;

                case "sha256" when property.Value.ValueKind == JsonValueKind.String:
                    hash = property.Value.GetString();
                    break;

                default:
                    return false;
            }
        }

        if (path is null || !ProductReleasePath.IsValid(path) || length is null or < 0 || hash is not { Length: 64 } || hash.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            return false;
        }

        file = new ProductReleaseFile(path, length.Value, Convert.FromHexString(hash));

        return true;
    }

    private static bool IsValidVersion(string version)
    {
        if (string.IsNullOrEmpty(version) || version.Length > MaximumVersionLength || !char.IsAsciiLetterOrDigit(version[0]) || !char.IsAsciiLetterOrDigit(version[^1]))
        {
            return false;
        }

        return version.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-');
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
