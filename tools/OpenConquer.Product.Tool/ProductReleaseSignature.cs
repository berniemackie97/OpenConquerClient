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
    private const string KeyIdPrefix = "sha256:";
    private const string PublicKeyPemHeader = "-----BEGIN PUBLIC KEY-----";

    public static void Create(ReleaseSignatureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        byte[] manifest = ProductReleaseManifest.ReadRegularFile(options.ReleaseManifestPath, 8 * 1024 * 1024);

        _ = ProductReleaseManifest.Read(options.ReleaseManifestPath);

        CreateVerifiedEnvelope(manifest, options.ReleaseManifestPath, options.PublicKeyPath,
            options.SignaturePath, options.OutputPath, 8 * 1024 * 1024);
    }

    public static void Create(CatalogSignatureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        byte[] catalog = ProductReleaseManifest.ReadRegularFile(
            options.ReleaseCatalogPath, ProductReleaseCatalog.MaximumLength);
        _ = ProductReleaseCatalog.Read(catalog);

        CreateVerifiedEnvelope(catalog, options.ReleaseCatalogPath, options.PublicKeyPath,
            options.SignaturePath, options.OutputPath, ProductReleaseCatalog.MaximumLength);
    }

    private static void CreateVerifiedEnvelope(
        byte[] signedContent,
        string signedContentPath,
        string publicKeyPath,
        string signaturePath,
        string outputPathValue,
        int maximumContentLength)
    {
        byte[] publicKeyFile = ProductReleaseManifest.ReadRegularFile(
            publicKeyPath, MaximumPublicKeyLength);
        byte[] signature = ProductReleaseManifest.ReadRegularFile(
            signaturePath, MaximumSignatureLength);

        if (signature.Length is < MinimumSignatureLength or > MaximumSignatureLength)
        {
            throw new InvalidDataException("The ECDSA signature length is invalid.");
        }

        byte[] publicKey = ImportPublicKey(publicKeyFile);

        using ECDsa algorithm = ECDsa.Create();

        try
        {
            algorithm.ImportSubjectPublicKeyInfo(publicKey, out int bytesRead);

            if (bytesRead != publicKey.Length || !algorithm.VerifyData(signedContent, signature,
                    HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
            {
                throw new InvalidDataException("The signature does not authenticate the signed release metadata.");
            }
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("The release signature or public key is invalid.", exception);
        }

        string outputPath = ProductStagingPathGuard.NormalizePath(
            outputPathValue, nameof(outputPathValue));

        string[] inputPaths =
        [
            signedContentPath,
            publicKeyPath,
            signaturePath,
        ];

        StringComparison pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (string inputPath in inputPaths)
        {
            string normalizedInputPath = ProductStagingPathGuard.NormalizePath(
                inputPath, nameof(inputPath));

            if (string.Equals(normalizedInputPath, outputPath, pathComparison))
            {
                throw new InvalidOperationException("The signature envelope output must not replace an input file.");
            }
        }

        ProductStagingPathGuard.PrepareFileOutput(outputPath);

        string temporaryPath = outputPath + $".tmp-{Guid.NewGuid():N}";
        bool completed = false;

        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
                writer.WriteString("keyId", GetKeyId(publicKey));
                writer.WriteString("algorithm", Algorithm);
                writer.WriteBase64String("signature", signature);
                writer.WriteEndObject();

                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (!signedContent.AsSpan().SequenceEqual(ProductReleaseManifest.ReadRegularFile(
                    signedContentPath, maximumContentLength)))
            {
                throw new IOException("The signed content changed while creating its envelope.");
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

    public static void ValidateEnvelope(string path)
    {
        byte[] bytes = ProductReleaseManifest.ReadRegularFile(path, MaximumEnvelopeLength);

        ValidateEnvelope(bytes);
    }

    internal static void ValidateEnvelope(ReadOnlyMemory<byte> bytes)
    {
        _ = ReadEnvelope(bytes);
    }

    internal static void VerifyEnvelope(
        ReadOnlyMemory<byte> signedContent,
        ReadOnlyMemory<byte> envelope,
        string publicKeyPath)
    {
        (string keyId, byte[] signature) = ReadEnvelope(envelope);
        byte[] publicKey = ExportSubjectPublicKeyInfo(publicKeyPath);
        if (!string.Equals(keyId, GetKeyId(publicKey), StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The release signature does not identify the supplied public key.");
        }

        using ECDsa algorithm = ECDsa.Create();
        try
        {
            algorithm.ImportSubjectPublicKeyInfo(publicKey, out int bytesRead);
            if (bytesRead != publicKey.Length || !algorithm.VerifyData(signedContent.Span,
                    signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
            {
                throw new InvalidDataException(
                    "The signature does not authenticate the signed release metadata.");
            }
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException(
                "The release signature or public key is invalid.", exception);
        }
    }

    private static (string KeyId, byte[] Signature) ReadEnvelope(
        ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Length > MaximumEnvelopeLength)
        {
            throw new InvalidDataException("The release signature envelope is invalid.");
        }

        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });

        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4 || !root.TryGetProperty("schemaVersion", out JsonElement schema)
            || !schema.TryGetInt32(out int schemaVersion) || schemaVersion != CurrentSchemaVersion || !root.TryGetProperty("keyId", out JsonElement keyId)
            || !root.TryGetProperty("algorithm", out JsonElement algorithm) || !root.TryGetProperty("signature", out JsonElement signature)
            || keyId.ValueKind != JsonValueKind.String || algorithm.ValueKind != JsonValueKind.String || signature.ValueKind != JsonValueKind.String
            || !IsValidKeyId(keyId.GetString()) || !string.Equals(algorithm.GetString(), Algorithm, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The release signature envelope is invalid.");
        }

        HashSet<string> properties = root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);

        if (!properties.SetEquals(["schemaVersion", "keyId", "algorithm", "signature"]))
        {
            throw new InvalidDataException("The release signature envelope is invalid.");
        }

        string? encoded = signature.GetString();

        if (encoded is null)
        {
            throw new InvalidDataException("The release signature envelope is invalid.");
        }

        byte[] signatureBytes;

        try
        {
            signatureBytes = Convert.FromBase64String(encoded);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The release signature envelope is invalid.", exception);
        }

        if (signatureBytes.Length is < MinimumSignatureLength or > MaximumSignatureLength)
        {
            throw new InvalidDataException("The release signature envelope is invalid.");
        }

        return (keyId.GetString()!, signatureBytes);
    }

    internal static byte[] ExportSubjectPublicKeyInfo(string publicKeyPath)
    {
        return ImportPublicKey(ProductReleaseManifest.ReadRegularFile(publicKeyPath, MaximumPublicKeyLength));
    }

    private static byte[] ImportPublicKey(ReadOnlySpan<byte> publicKeyFile)
    {
        using ECDsa algorithm = ECDsa.Create();

        try
        {
            if (publicKeyFile.StartsWith("-----BEGIN"u8))
            {
                string pem = Encoding.ASCII.GetString(publicKeyFile);

                if (!pem.StartsWith(PublicKeyPemHeader, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Only public-key PEM input is accepted for release verification.");
                }

                algorithm.ImportFromPem(pem);
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

            if (parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value || parameters.Q.X is not { Length: 32 } || parameters.Q.Y is not { Length: 32 })
            {
                throw new InvalidDataException("Only ECDSA P-256 release public keys are supported.");
            }

            return algorithm.ExportSubjectPublicKeyInfo();
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("The release public key is invalid.", exception);
        }
    }

    internal static string GetKeyId(ReadOnlySpan<byte> publicKey)
    {
        return KeyIdPrefix + Convert.ToHexStringLower(SHA256.HashData(publicKey));
    }

    private static bool IsValidKeyId(string? keyId)
    {
        if (keyId is not { Length: 71 } || !keyId.StartsWith(KeyIdPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> digest = keyId.AsSpan(KeyIdPrefix.Length);

        foreach (char character in digest)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
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
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
