using System.Text.Json;

namespace OpenConquer.Launcher.Installation;

internal sealed record ManagedReleaseFile(string Path, long Length, byte[] Sha256);

/// <summary>Publisher-signed metadata describing one complete client component.</summary>
internal sealed record ManagedReleaseManifest(int SchemaVersion, string ProductId, ulong ReleaseSequence, string ReleaseVersion, int MinimumLauncherVersion, string TargetRuntime, string ClientExecutable, IReadOnlyList<ManagedReleaseFile> Files)
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentLauncherVersion = 1;
    public const string ExpectedProductId = "OpenConquer";
    public const string FileName = "openconquer.release.json";
    public const int MaximumFileCount = 16 * 1024;

    private const int MaximumLength = 8 * 1024 * 1024;
    private const int MaximumVersionLength = 64;

    public static async Task<ReleaseManifestReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            byte[] bytes = await InstallationFile.ReadBoundedAsync(path, MaximumLength, cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });

            if (!TryRead(document.RootElement, out ManagedReleaseManifest? manifest) || manifest is null)
            {
                return new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.ReleaseManifestInvalid);
            }

            if (manifest.SchemaVersion > CurrentSchemaVersion)
            {
                return new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.UnsupportedReleaseManifest);
            }

            if (manifest.SchemaVersion != CurrentSchemaVersion)
            {
                return new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.ReleaseManifestInvalid);
            }

            if (manifest.MinimumLauncherVersion > CurrentLauncherVersion)
            {
                return new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.LauncherUpdateRequired);
            }

            return new ReleaseManifestReadResult.Accepted(manifest, bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            return new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.ReleaseMetadataMissing);
        }
        catch (DirectoryNotFoundException)
        {
            return new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.ReleaseMetadataMissing);
        }
        catch (UnauthorizedAccessException)
        {
            return new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.AccessDenied);
        }
        catch (LinkedInstallationPathException)
        {
            return new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.LinkedPath);
        }
        catch (JsonException)
        {
            return new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.ReleaseManifestInvalid);
        }
        catch (IOException)
        {
            return new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.ReadFailure);
        }
    }

    private static bool TryRead(JsonElement root, out ManagedReleaseManifest? manifest)
    {
        manifest = null;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 8)
        {
            return false;
        }

        HashSet<string> properties = new(StringComparer.Ordinal);
        int? schemaVersion = null;
        string? productId = null;
        ulong? releaseSequence = null;
        string? releaseVersion = null;
        int? minimumLauncherVersion = null;
        string? targetRuntime = null;
        string? clientExecutable = null;
        List<ManagedReleaseFile>? files = null;

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

        if (schemaVersion is null or <= 0 || releaseSequence is null or 0 || minimumLauncherVersion is null or <= 0 ||
            !string.Equals(productId, ExpectedProductId, StringComparison.Ordinal) || !IsValidVersion(releaseVersion) ||
            targetRuntime is null || !ReleaseTargetRuntime.IsSupported(targetRuntime) || clientExecutable is null ||
            !ReleasePackagePath.IsValid(clientExecutable) || files is null || files.Count == 0 ||
            !files.Any(file => string.Equals(file.Path, clientExecutable, StringComparison.Ordinal)))
        {
            return false;
        }

        manifest = new ManagedReleaseManifest(schemaVersion.Value, productId, releaseSequence.Value, releaseVersion!, minimumLauncherVersion.Value, targetRuntime, clientExecutable, files);
        return true;
    }

    private static bool TryReadFiles(JsonElement element, out List<ManagedReleaseFile>? files)
    {
        files = null;
        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        List<ManagedReleaseFile> parsed = [];
        HashSet<string> paths = new(StringComparer.Ordinal);
        HashSet<string> portablePaths = new(StringComparer.Ordinal);
        string? previousPath = null;

        foreach (JsonElement item in element.EnumerateArray())
        {
            if (parsed.Count == MaximumFileCount || !TryReadFile(item, out ManagedReleaseFile? file) || file is null ||
                !paths.Add(file.Path) || !portablePaths.Add(ReleasePackagePath.PortableIdentity(file.Path)) ||
                previousPath is not null && string.CompareOrdinal(previousPath, file.Path) >= 0)
            {
                return false;
            }

            parsed.Add(file);
            previousPath = file.Path;
        }

        files = parsed;
        return true;
    }

    private static bool TryReadFile(JsonElement item, out ManagedReleaseFile? file)
    {
        file = null;
        if (item.ValueKind != JsonValueKind.Object || item.EnumerateObject().Count() != 3)
        {
            return false;
        }

        string? path = null;
        long? length = null;
        string? sha256 = null;
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
                    sha256 = property.Value.GetString();
                    break;
                default:
                    return false;
            }
        }

        if (!ReleasePackagePath.IsValid(path) || length is null or < 0 || sha256 is null || sha256.Length != 64 ||
            sha256.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            return false;
        }

        file = new ManagedReleaseFile(path!, length.Value, Convert.FromHexString(sha256));
        return true;
    }

    private static bool IsValidVersion(string? version)
    {
        if (string.IsNullOrEmpty(version) || version.Length > MaximumVersionLength ||
            !char.IsAsciiLetterOrDigit(version[0]) || !char.IsAsciiLetterOrDigit(version[^1]))
        {
            return false;
        }

        return version.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-');
    }
}

internal abstract record ReleaseManifestReadResult
{
    private ReleaseManifestReadResult()
    {
    }

    internal sealed record Accepted(ManagedReleaseManifest Manifest, byte[] Bytes) : ReleaseManifestReadResult;
    internal sealed record Rejected(ManagedInstallationIssue Issue) : ReleaseManifestReadResult;
}
