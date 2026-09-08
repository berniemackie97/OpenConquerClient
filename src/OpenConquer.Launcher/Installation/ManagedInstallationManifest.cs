using System.Text.Json;

namespace OpenConquer.Launcher.Installation;

/// <summary>Installer-owned managed-product layout metadata, without release or integrity authority.</summary>
internal sealed record ManagedInstallationManifest(int SchemaVersion, string ProductId, string ClientRoot)
{
    public const int CurrentSchemaVersion = 1;
    public const string ExpectedProductId = "OpenConquer";
    public const string ExpectedClientRoot = "client";
    public const string FileName = "openconquer.installation.json";

    private const int MaximumLength = 32 * 1024;

    public static async Task<ManifestReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            if (!IsRegularFile(path))
            {
                return new ManifestReadResult.Rejected(ManagedInstallationIssue.ManifestInvalid);
            }

            FileInfo file = new(path);
            long length = file.Length;

            if (length is <= 0 or > MaximumLength)
            {
                return new ManifestReadResult.Rejected(ManagedInstallationIssue.ManifestInvalid);
            }

            byte[] bytes = new byte[(int)length];

            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            if (stream.Length != length)
            {
                return new ManifestReadResult.Rejected(ManagedInstallationIssue.ReadFailure);
            }

            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);

            if (stream.Length != length)
            {
                return new ManifestReadResult.Rejected(ManagedInstallationIssue.ReadFailure);
            }

            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });

            if (!TryRead(document.RootElement, out ManagedInstallationManifest? manifest) || manifest is null)
            {
                return new ManifestReadResult.Rejected(ManagedInstallationIssue.ManifestInvalid);
            }

            if (manifest.SchemaVersion > CurrentSchemaVersion)
            {
                return new ManifestReadResult.Rejected(ManagedInstallationIssue.UnsupportedManifest);
            }

            if (manifest.SchemaVersion != CurrentSchemaVersion || !string.Equals(manifest.ClientRoot, ExpectedClientRoot, StringComparison.Ordinal))
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

        HashSet<string> properties = new(StringComparer.Ordinal);

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!properties.Add(property.Name))
            {
                return false;
            }

            switch (property.Name)
            {
                case "schemaVersion"
                    when property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out int parsedSchemaVersion):
                    schemaVersion = parsedSchemaVersion;
                    break;

                case "productId" when property.Value.ValueKind == JsonValueKind.String:
                    productId = property.Value.GetString();
                    break;

                case "clientRoot" when property.Value.ValueKind == JsonValueKind.String:
                    clientRoot = property.Value.GetString();
                    break;

                default:
                    return false;
            }
        }

        if (schemaVersion is null || schemaVersion <= 0 || productId is null || clientRoot is null || !string.Equals(productId, ExpectedProductId, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(clientRoot))
        {
            return false;
        }

        manifest = new ManagedInstallationManifest(schemaVersion.Value, productId, clientRoot);
        return true;
    }

    private static bool IsRegularFile(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new LinkedInstallationPathException();
        }

        return (attributes & (FileAttributes.Directory | FileAttributes.Device)) == 0;
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
