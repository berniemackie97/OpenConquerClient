using System.Text.Json;

namespace OpenConquer.Launcher.Installation;

/// <summary>Detached signature metadata for the exact release-manifest bytes.</summary>
internal sealed record ManagedReleaseSignature(string KeyId, byte[] Signature)
{
    public const string FileName = "openconquer.release.sig";
    public const string Algorithm = "ecdsa-p256-sha256-der";

    private const int CurrentSchemaVersion = 1;
    private const int MaximumLength = 4 * 1024;
    private const int MinimumSignatureLength = 64;
    private const int MaximumSignatureLength = 80;

    public static async Task<ReleaseSignatureReadResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            byte[] bytes = await InstallationFile.ReadBoundedAsync(path, MaximumLength, cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            return TryRead(document.RootElement, out ManagedReleaseSignature? signature) && signature is not null
                ? new ReleaseSignatureReadResult.Accepted(signature, bytes)
                : new ReleaseSignatureReadResult.Rejected(ManagedInstallationIssue.ReleaseSignatureInvalid);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            return new ReleaseSignatureReadResult.Rejected(ManagedInstallationIssue.ReleaseMetadataMissing);
        }
        catch (DirectoryNotFoundException)
        {
            return new ReleaseSignatureReadResult.Rejected(ManagedInstallationIssue.ReleaseMetadataMissing);
        }
        catch (UnauthorizedAccessException)
        {
            return new ReleaseSignatureReadResult.Rejected(ManagedInstallationIssue.AccessDenied);
        }
        catch (LinkedInstallationPathException)
        {
            return new ReleaseSignatureReadResult.Rejected(ManagedInstallationIssue.LinkedPath);
        }
        catch (JsonException)
        {
            return new ReleaseSignatureReadResult.Rejected(ManagedInstallationIssue.ReleaseSignatureInvalid);
        }
        catch (FormatException)
        {
            return new ReleaseSignatureReadResult.Rejected(ManagedInstallationIssue.ReleaseSignatureInvalid);
        }
        catch (IOException)
        {
            return new ReleaseSignatureReadResult.Rejected(ManagedInstallationIssue.ReadFailure);
        }
    }

    internal static bool TryRead(JsonElement root, out ManagedReleaseSignature? signature)
    {
        signature = null;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4)
        {
            return false;
        }

        int? schemaVersion = null;
        string? keyId = null;
        string? algorithm = null;
        string? encodedSignature = null;
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
                case "keyId" when property.Value.ValueKind == JsonValueKind.String:
                    keyId = property.Value.GetString();
                    break;
                case "algorithm" when property.Value.ValueKind == JsonValueKind.String:
                    algorithm = property.Value.GetString();
                    break;
                case "signature" when property.Value.ValueKind == JsonValueKind.String:
                    encodedSignature = property.Value.GetString();
                    break;
                default:
                    return false;
            }
        }

        if (schemaVersion != CurrentSchemaVersion || !TrustedReleaseKeys.IsValidKeyId(keyId) ||
            !string.Equals(algorithm, Algorithm, StringComparison.Ordinal) || string.IsNullOrEmpty(encodedSignature))
        {
            return false;
        }

        byte[] bytes = Convert.FromBase64String(encodedSignature);
        if (bytes.Length is < MinimumSignatureLength or > MaximumSignatureLength)
        {
            return false;
        }

        signature = new ManagedReleaseSignature(keyId!, bytes);
        return true;
    }
}

internal abstract record ReleaseSignatureReadResult
{
    private ReleaseSignatureReadResult()
    {
    }

    internal sealed record Accepted(ManagedReleaseSignature Signature, byte[] Bytes) : ReleaseSignatureReadResult;
    internal sealed record Rejected(ManagedInstallationIssue Issue) : ReleaseSignatureReadResult;
}
