using System.Globalization;
using System.Text.Json;

namespace OpenConquer.Launcher.Installation;

/// <summary>Installer-owned active-release pointer without release or integrity authority.</summary>
internal sealed record ManagedInstallationManifest(int SchemaVersion, string ProductId, string? ClientRoot, string? ActiveRelease, string? FallbackRelease)
{
    public const int CurrentSchemaVersion = 2;
    public const string ExpectedProductId = "OpenConquer";
    public const string ExpectedClientRoot = "client";
    public const string ReleasesRoot = "releases";
    public const string FileName = "openconquer.installation.json";

    private const int MaximumLength = 32 * 1024;
    private const int ReleaseDigestLength = 64;
    private const int GenerationNonceLength = 32;
    private const int ReleaseSequenceLength = 20;
    private static readonly UnixFileMode s_descriptorFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    private static readonly UnixFileMode s_privateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public static ManagedInstallationManifest CreateCurrent(string activeRelease, string? fallbackRelease)
    {
        if (!IsValidReleaseId(activeRelease) || fallbackRelease is not null && !IsValidReleaseId(fallbackRelease) || string.Equals(activeRelease, fallbackRelease, StringComparison.Ordinal))
        {
            throw new ArgumentException("The active and fallback release identities are invalid.");
        }

        return new ManagedInstallationManifest(CurrentSchemaVersion, ExpectedProductId, ClientRoot: null, activeRelease, fallbackRelease);
    }

    public static async Task<ManifestReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            byte[] bytes = await InstallationFile.ReadBoundedAsync(path, MaximumLength, cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });

            if (HasUnsupportedSchema(document.RootElement))
            {
                return new ManifestReadResult.Rejected(ManagedInstallationIssue.UnsupportedManifest);
            }

            if (!TryRead(document.RootElement, out ManagedInstallationManifest? manifest) || manifest is null)
            {
                return new ManifestReadResult.Rejected(ManagedInstallationIssue.ManifestInvalid);
            }

            return new ManifestReadResult.Accepted(manifest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            return new ManifestReadResult.Rejected(ManagedInstallationIssue.ManifestMissing);
        }
        catch (DirectoryNotFoundException)
        {
            return new ManifestReadResult.Rejected(ManagedInstallationIssue.ManifestMissing);
        }
        catch (UnauthorizedAccessException)
        {
            return new ManifestReadResult.Rejected(ManagedInstallationIssue.AccessDenied);
        }
        catch (LinkedInstallationPathException)
        {
            return new ManifestReadResult.Rejected(ManagedInstallationIssue.LinkedPath);
        }
        catch (JsonException)
        {
            return new ManifestReadResult.Rejected(ManagedInstallationIssue.ManifestInvalid);
        }
        catch (IOException)
        {
            return new ManifestReadResult.Rejected(ManagedInstallationIssue.ReadFailure);
        }
    }

    public static async Task WriteCurrentAsync(string path, string activeRelease, string? fallbackRelease, CancellationToken cancellationToken)
    {
        ManagedInstallationManifest manifest = CreateCurrent(activeRelease, fallbackRelease);
        string? directoryPath = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("The installation descriptor must have a parent directory.", nameof(path));
        }

        bool destinationExists = File.Exists(path);
        string temporaryPath = Path.Combine(directoryPath, $".{FileName}.{Guid.NewGuid():N}.tmp");
        bool activated = false;
        try
        {
            FileStreamOptions options = new()
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            };

            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = s_privateFileMode;
            }

            await using (FileStream stream = new(temporaryPath, options))
            {
                await JsonSerializer.SerializeAsync(stream, new
                {
                    schemaVersion = manifest.SchemaVersion,
                    productId = manifest.ProductId,
                    activeRelease = manifest.ActiveRelease,
                    fallbackRelease = manifest.FallbackRelease,
                }, cancellationToken: cancellationToken).ConfigureAwait(false);

                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporaryPath, s_descriptorFileMode);
            }

            cancellationToken.ThrowIfCancellationRequested();
            InstallationFile.Commit(temporaryPath, path, destinationExists);
            activated = true;
        }
        finally
        {
            if (!activated)
            {
                TryDelete(temporaryPath);
            }
        }
    }

    public static bool IsValidReleaseId(string? value)
    {
        int canonicalLength = ReleaseSequenceLength + 1 + ReleaseDigestLength;
        if (value is null || value.Length is not (ReleaseSequenceLength + 1 + ReleaseDigestLength) and not (ReleaseSequenceLength + 1 + ReleaseDigestLength + 1 + GenerationNonceLength) || value[ReleaseSequenceLength] != '-')
        {
            return false;
        }

        for (int index = 0; index < ReleaseSequenceLength; index++)
        {
            if (value[index] is not (>= '0' and <= '9'))
            {
                return false;
            }
        }

        for (int index = ReleaseSequenceLength + 1; index < value.Length; index++)
        {
            if (index == canonicalLength)
            {
                if (value[index] != '-')
                {
                    return false;
                }

                continue;
            }

            if (value[index] is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return ulong.TryParse(value.AsSpan(0, ReleaseSequenceLength), NumberStyles.None, CultureInfo.InvariantCulture, out ulong sequence) && sequence > 0;
    }

    private static bool TryRead(JsonElement root, out ManagedInstallationManifest? manifest)
    {
        manifest = null;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        int? schemaVersion = null;
        string? productId = null;
        string? clientRoot = null;
        string? activeRelease = null;
        string? fallbackRelease = null;
        bool fallbackSeen = false;
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
                case "clientRoot" when property.Value.ValueKind == JsonValueKind.String:
                    clientRoot = property.Value.GetString();
                    break;
                case "activeRelease" when property.Value.ValueKind == JsonValueKind.String:
                    activeRelease = property.Value.GetString();
                    break;
                case "fallbackRelease" when property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null:
                    fallbackRelease = property.Value.GetString();
                    fallbackSeen = true;
                    break;
                default:
                    return false;
            }
        }

        if (schemaVersion is null or <= 0 || !string.Equals(productId, ExpectedProductId, StringComparison.Ordinal))
        {
            return false;
        }

        if (schemaVersion == 1 && properties.SetEquals(["schemaVersion", "productId", "clientRoot"]) && string.Equals(clientRoot, ExpectedClientRoot, StringComparison.Ordinal))
        {
            manifest = new ManagedInstallationManifest(1, ExpectedProductId, clientRoot, ActiveRelease: null, FallbackRelease: null);
            return true;
        }

        if (schemaVersion == CurrentSchemaVersion && fallbackSeen && properties.SetEquals(["schemaVersion", "productId", "activeRelease", "fallbackRelease"])
            && IsValidReleaseId(activeRelease) && (fallbackRelease is null || IsValidReleaseId(fallbackRelease)) && !string.Equals(activeRelease, fallbackRelease, StringComparison.Ordinal))
        {
            manifest = new ManagedInstallationManifest(CurrentSchemaVersion, ExpectedProductId, ClientRoot: null, activeRelease, fallbackRelease);
            return true;
        }

        return false;
    }

    private static bool HasUnsupportedSchema(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        int? schemaVersion = null;
        string? productId = null;
        HashSet<string> properties = new(StringComparer.Ordinal);

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!properties.Add(property.Name))
            {
                return false;
            }

            if (property.Name == "schemaVersion" && property.Value.TryGetInt32(out int value))
            {
                schemaVersion = value;
            }
            else if (property.Name == "productId" && property.Value.ValueKind == JsonValueKind.String)
            {
                productId = property.Value.GetString();
            }
        }

        return schemaVersion > CurrentSchemaVersion && string.Equals(productId, ExpectedProductId, StringComparison.Ordinal);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Cleanup must not replace the descriptor write failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup must not replace the descriptor write failure.
        }
    }
}

internal abstract record ManifestReadResult
{
    private ManifestReadResult()
    {
    }

    internal sealed record Accepted(ManagedInstallationManifest Manifest) : ManifestReadResult;
    internal sealed record Rejected(ManagedInstallationIssue Issue) : ManifestReadResult;
}
