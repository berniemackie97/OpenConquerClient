using System.Security.Cryptography;
using System.Text.Json;

namespace OpenConquer.Client.Startup.Catalog;

/// <summary>Immutable issuer and HTTPS origin authority embedded into the signed client build.</summary>
internal sealed class LoginCatalogAuthority
{
    private const string ResourceName = "OpenConquer.Client.Startup.Catalog.Authority.json";
    private const int MaximumAuthorityLength = 4096;
    private const string P256CurveOid = "1.2.840.10045.3.1.7";
    private readonly byte[] _publicKey;

    public Uri CatalogUri
    {
        get;
    }
    public Uri SignatureUri
    {
        get;
    }

    public LoginCatalogAuthority(Uri catalogUri, Uri signatureUri, ReadOnlySpan<byte> subjectPublicKeyInfo)
    {
        ValidateUri(catalogUri);
        ValidateUri(signatureUri);

        if (Uri.Compare(catalogUri, signatureUri, UriComponents.AbsoluteUri, UriFormat.Unescaped, StringComparison.Ordinal) == 0)
        {
            throw new ArgumentException("The catalog and signature locations must be distinct.", nameof(signatureUri));
        }

        if (subjectPublicKeyInfo.Length is < 64 or > 256)
        {
            throw new ArgumentException("The catalog issuer key has an invalid length.", nameof(subjectPublicKeyInfo));
        }

        using ECDsa verifier = ECDsa.Create();

        try
        {
            verifier.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out int consumed);

            if (consumed != subjectPublicKeyInfo.Length || verifier.KeySize != 256 ||
                verifier.ExportParameters(false).Curve.Oid?.Value != P256CurveOid)
            {
                throw new ArgumentException("The catalog issuer must use a P-256 subject-public-key-info key.", nameof(subjectPublicKeyInfo));
            }
        }
        catch (CryptographicException exception)
        {
            throw new ArgumentException("The catalog issuer public key is invalid.", nameof(subjectPublicKeyInfo), exception);
        }

        CatalogUri = catalogUri;
        SignatureUri = signatureUri;
        _publicKey = subjectPublicKeyInfo.ToArray();
    }

    public bool Verify(ReadOnlySpan<byte> catalog, ReadOnlySpan<byte> signature)
    {
        if (signature.Length != 64)
        {
            return false;
        }

        using ECDsa verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(_publicKey, out _);
        return verifier.VerifyData(catalog, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    public static LoginCatalogAuthority? LoadEmbedded()
    {
        using Stream? source = typeof(LoginCatalogAuthority).Assembly.GetManifestResourceStream(ResourceName);

        if (source is null)
        {
            return null;
        }

        using MemoryStream output = new();
        byte[] buffer = new byte[1024];

        while (true)
        {
            int count = source.Read(buffer, 0, Math.Min(buffer.Length, MaximumAuthorityLength + 1 - (int)output.Length));

            if (count == 0)
            {
                break;
            }

            output.Write(buffer, 0, count);

            if (output.Length > MaximumAuthorityLength)
            {
                throw new InvalidDataException("The embedded catalog authority exceeds its size limit.");
            }
        }

        try
        {
            using JsonDocument document = LoginCatalogJson.Parse(output.ToArray());
            JsonElement root = document.RootElement;
            LoginCatalogJson.RequireObject(root, "catalogUri", "signatureUri", "publicKeySpki");

            string catalog = LoginCatalogJson.RequiredString(root, "catalogUri", 2048);
            string signature = LoginCatalogJson.RequiredString(root, "signatureUri", 2048);
            string key = LoginCatalogJson.RequiredString(root, "publicKeySpki", 512);

            if (!Uri.TryCreate(catalog, UriKind.Absolute, out Uri? catalogUri) ||
                !Uri.TryCreate(signature, UriKind.Absolute, out Uri? signatureUri))
            {
                throw new InvalidDataException("The embedded catalog origin is invalid.");
            }

            return new LoginCatalogAuthority(catalogUri, signatureUri, Convert.FromBase64String(key));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException)
        {
            throw new InvalidDataException("The embedded catalog authority is invalid.", exception);
        }
    }

    private static void ValidateUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 ||
            uri.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            uri.AbsolutePath.Length <= 1)
        {
            throw new ArgumentException("Catalog locations must be absolute HTTPS URIs without credentials, query, or fragment.", nameof(uri));
        }
    }
}
