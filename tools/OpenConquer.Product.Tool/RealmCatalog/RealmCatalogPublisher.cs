using System.Security.Cryptography;
using System.Text.Json;

namespace OpenConquer.Product.Tool;

/// <summary>Offline production of a byte-signed realm directory and build-time public authority.</summary>
internal static class RealmCatalogPublisher
{
    private const int MaximumPrivateKeyBytes = 4096;
    private const int MaximumDefinitionBytes = RealmCatalogDefinition.MaximumBytes;
    private const string CurveOid = "1.2.840.10045.3.1.7";
    private static readonly UnixFileMode s_privateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public static void Create(RealmCatalogPublishOptions options, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Sequence <= 0 || options.IssuedAt > now + TimeSpan.FromMinutes(5) || options.ExpiresAt <= now ||
            options.ExpiresAt <= options.IssuedAt || options.ExpiresAt - options.IssuedAt > TimeSpan.FromHours(24) ||
            options.IssuedAt.Offset != TimeSpan.Zero || options.ExpiresAt.Offset != TimeSpan.Zero ||
            options.IssuedAt.Ticks % TimeSpan.TicksPerSecond != 0 || options.ExpiresAt.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new InvalidDataException("The signed realm sequence or UTC validity interval is invalid.");
        }

        ValidateUri(options.CatalogUri);
        ValidateUri(options.SignatureUri);
        if (Uri.Compare(options.CatalogUri, options.SignatureUri, UriComponents.AbsoluteUri, UriFormat.Unescaped, StringComparison.Ordinal) == 0)
        {
            throw new InvalidDataException("Catalog and signature must have distinct publication URIs.");
        }

        string definitionPath = ProductStagingPathGuard.RequireRegularFile(options.DefinitionPath, "realm definition");
        string privateKeyPath = ProductStagingPathGuard.RequireRegularFile(options.PrivateKeyPath, "realm signing private key");
        string outputPath = ProductStagingPathGuard.NormalizePath(options.OutputPath, nameof(options.OutputPath));
        ProductStagingPathGuard.RejectPathWithinRoot(outputPath, definitionPath, "The realm definition cannot be inside the publication output.");
        ProductStagingPathGuard.RejectPathWithinRoot(outputPath, privateKeyPath, "The private key cannot be inside the publication output.");
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(privateKeyPath) & ~s_privateFileMode) != 0)
        {
            throw new UnauthorizedAccessException("The realm signing private key must not be accessible by group or other users.");
        }

        byte[] definition = ReadBounded(definitionPath, MaximumDefinitionBytes);
        byte[] catalog = RealmCatalogDefinition.Compose(definition, options.Sequence, options.IssuedAt, options.ExpiresAt);
        byte[] encodedPrivateKey = ReadBounded(privateKeyPath, MaximumPrivateKeyBytes);
        byte[] signature;
        byte[] publicKey;

        try
        {
            using ECDsa signer = ECDsa.Create();
            try
            {
                signer.ImportPkcs8PrivateKey(encodedPrivateKey, out int read);
                ECParameters parameters = signer.ExportParameters(false);
                if (read != encodedPrivateKey.Length || signer.KeySize != 256 || parameters.Curve.Oid?.Value != CurveOid ||
                    parameters.Q.X is not { Length: 32 } || parameters.Q.Y is not { Length: 32 })
                {
                    throw new InvalidDataException("The signing key must be exactly one unencrypted P-256 PKCS#8 private key.");
                }
                signature = signer.SignData(catalog, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                publicKey = signer.ExportSubjectPublicKeyInfo();
                if (signature.Length != 64 || !signer.VerifyData(catalog, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                {
                    throw new CryptographicException("The signed realm directory failed local signature verification.");
                }
            }
            catch (CryptographicException exception)
            {
                throw new InvalidDataException("The realm signing private key or signature is invalid.", exception);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encodedPrivateKey);
        }

        byte[] authority = CreateAuthority(options.CatalogUri, options.SignatureUri, publicKey);
        ProductStagingPathGuard.PrepareOutputParent(outputPath);
        string parent = Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("Output requires a parent directory.");
        string staging = Path.Combine(parent, $".openconquer-realms-{Guid.NewGuid():N}");
        bool committed = false;
        try
        {
            Directory.CreateDirectory(staging);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(staging, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            WriteFile(Path.Combine(staging, "realms.json"), catalog);
            WriteFile(Path.Combine(staging, "realms.sig"), signature);
            WriteFile(Path.Combine(staging, "authority.json"), authority);
            if (Directory.Exists(outputPath) || File.Exists(outputPath))
            {
                throw new IOException("The realm publication destination already exists.");
            }
            Directory.Move(staging, outputPath);
            committed = true;
        }
        finally
        {
            if (!committed && Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    private static byte[] CreateAuthority(Uri catalogUri, Uri signatureUri, byte[] publicKey)
    {
        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output))
        {
            writer.WriteStartObject();
            writer.WriteString("catalogUri", catalogUri.AbsoluteUri);
            writer.WriteString("signatureUri", signatureUri.AbsoluteUri);
            writer.WriteBase64String("publicKeySpki", publicKey);
            writer.WriteEndObject();
            writer.Flush();
        }
        return output.ToArray();
    }

    private static void ValidateUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 ||
            uri.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath.Length <= 1)
        {
            throw new InvalidDataException("Realm publication locations must be absolute HTTPS:443 URIs with a path, no credentials, query or fragment.");
        }
    }

    private static byte[] ReadBounded(string path, int maximum)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!stream.CanSeek || stream.Length is <= 0 || stream.Length > maximum)
        {
            throw new InvalidDataException("The publisher input file exceeds its size limit.");
        }
        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.Length != bytes.Length)
        {
            throw new IOException("The publisher input file changed during the read.");
        }
        return bytes;
    }

    private static void WriteFile(string path, byte[] contents)
    {
        using FileStream stream = new(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None });
        stream.Write(contents);
        stream.Flush(flushToDisk: true);
    }
}
