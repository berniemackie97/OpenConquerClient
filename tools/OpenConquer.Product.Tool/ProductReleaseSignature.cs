using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenConquer.Product.Tool;

/// <summary>Validates externally produced signatures and writes their bounded envelope.</summary>
internal static class ProductReleaseSignature
{
    public const string FileName = "openconquer.release.sig";
    public const string Algorithm = "ecdsa-p256-sha256-der";

    private const int CurrentSchemaVersion = 1;
    private const int MaximumEnvelopeLength = 4 * 1024;
    private const int MaximumPublicKeyLength = 4 * 1024;
    private const int MaximumSignatureLength = 80;
    private const int MinimumSignatureLength = 64;

    public static void Create(ReleaseSignatureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        byte[] manifest = ProductReleaseManifest.ReadRegularFile(options.ReleaseManifestPath, 8 * 1024 * 1024);
        _ = ProductReleaseManifest.Read(options.ReleaseManifestPath);
        byte[] publicKeyFile = ProductReleaseManifest.ReadRegularFile(options.PublicKeyPath, MaximumPublicKeyLength);
        byte[] signature = ProductReleaseManifest.ReadRegularFile(options.SignaturePath, MaximumSignatureLength);
        if (signature.Length is < MinimumSignatureLength or > MaximumSignatureLength)
        {
            throw new InvalidDataException("The ECDSA signature length is invalid.");
        }

        byte[] publicKey = ImportPublicKey(publicKeyFile);
        using ECDsa algorithm = ECDsa.Create();
        algorithm.ImportSubjectPublicKeyInfo(publicKey, out int bytesRead);
        if (bytesRead != publicKey.Length || !algorithm.VerifyData(manifest, signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence))
        {
            throw new InvalidDataException("The signature does not authenticate the release manifest.");
        }

        string outputPath = ProductStagingPathGuard.NormalizePath(options.OutputPath, nameof(options.OutputPath));
        foreach (string input in new[] { options.ReleaseManifestPath, options.PublicKeyPath, options.SignaturePath })
        {
            if (string.Equals(ProductStagingPathGuard.NormalizePath(input, nameof(options)), outputPath,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The signature envelope output must not replace an input file.");
            }
        }

        ProductStagingPathGuard.PrepareFileOutput(outputPath);
        string temporaryPath = outputPath + $".tmp-{Guid.NewGuid():N}";
        bool completed = false;
        try
        {
            using FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
            writer.WriteString("keyId", "sha256:" + Convert.ToHexStringLower(SHA256.HashData(publicKey)));
            writer.WriteString("algorithm", Algorithm);
            writer.WriteBase64String("signature", signature);
            writer.WriteEndObject();
            writer.Flush();
            stream.Flush(flushToDisk: true);
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

    public static void ValidateEnvelope(string path)
    {
        byte[] bytes = ProductReleaseManifest.ReadRegularFile(path, MaximumEnvelopeLength);
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4 ||
            !root.TryGetProperty("schemaVersion", out JsonElement schema) || !schema.TryGetInt32(out int schemaVersion) ||
            schemaVersion != CurrentSchemaVersion || !root.TryGetProperty("keyId", out JsonElement keyId) ||
            !root.TryGetProperty("algorithm", out JsonElement algorithm) || !root.TryGetProperty("signature", out JsonElement signature) ||
            keyId.ValueKind != JsonValueKind.String || algorithm.ValueKind != JsonValueKind.String || signature.ValueKind != JsonValueKind.String ||
            !IsValidKeyId(keyId.GetString()) || !string.Equals(algorithm.GetString(), Algorithm, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The release signature envelope is invalid.");
        }

        HashSet<string> properties = root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (!properties.SetEquals(["schemaVersion", "keyId", "algorithm", "signature"]))
        {
            throw new InvalidDataException("The release signature envelope is invalid.");
        }

        string? encoded = signature.GetString();
        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(encoded ?? string.Empty);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The release signature envelope is invalid.", exception);
        }

        if (signatureBytes.Length is < MinimumSignatureLength or > MaximumSignatureLength)
        {
            throw new InvalidDataException("The release signature envelope is invalid.");
        }
    }

    internal static byte[] ExportSubjectPublicKeyInfo(string publicKeyPath)
    {
        return ImportPublicKey(ProductReleaseManifest.ReadRegularFile(publicKeyPath, MaximumPublicKeyLength));
    }

    private static byte[] ImportPublicKey(ReadOnlySpan<byte> publicKeyFile)
    {
        using ECDsa algorithm = ECDsa.Create();
        if (publicKeyFile.StartsWith("-----BEGIN"u8))
        {
            algorithm.ImportFromPem(Encoding.ASCII.GetString(publicKeyFile));
        }
        else
        {
            algorithm.ImportSubjectPublicKeyInfo(publicKeyFile, out int bytesRead);
            if (bytesRead != publicKeyFile.Length)
            {
                throw new InvalidDataException("The release public key contains trailing data.");
            }
        }

        ECParameters parameters = algorithm.ExportParameters(includePrivateParameters: false);
        if (parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value ||
            parameters.Q.X is not { Length: 32 } || parameters.Q.Y is not { Length: 32 })
        {
            throw new InvalidDataException("Only ECDSA P-256 release public keys are supported.");
        }

        return algorithm.ExportSubjectPublicKeyInfo();
    }

    private static bool IsValidKeyId(string? keyId)
    {
        return keyId is { Length: 71 } && keyId.StartsWith("sha256:", StringComparison.Ordinal) &&
            keyId.AsSpan(7).All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
