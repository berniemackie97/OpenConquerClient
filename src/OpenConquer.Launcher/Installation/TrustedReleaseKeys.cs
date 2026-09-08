using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace OpenConquer.Launcher.Installation;

/// <summary>Immutable publisher trust roots compiled into the launcher package.</summary>
internal sealed class TrustedReleaseKeys
{
    public const string EmbeddedResourceName = "OpenConquer.Launcher.release-trust.json";

    private const int CurrentSchemaVersion = 1;
    private const int MaximumLength = 32 * 1024;
    private const int MaximumKeyCount = 8;
    private const int MaximumPublicKeyLength = 256;
    private const string KeyIdPrefix = "sha256:";

    private readonly IReadOnlyDictionary<string, byte[]> _keys;

    private TrustedReleaseKeys(IReadOnlyDictionary<string, byte[]> keys, bool isConfigured)
    {
        _keys = keys;
        IsConfigured = isConfigured;
    }

    public bool IsConfigured
    {
        get;
    }

    public static TrustedReleaseKeys LoadEmbedded(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        using Stream? stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
        if (stream is null || stream.Length is <= 0 or > MaximumLength)
        {
            return Unconfigured();
        }

        byte[] bytes = new byte[(int)stream.Length];

        try
        {
            stream.ReadExactly(bytes);

            return TryParse(bytes, out TrustedReleaseKeys? keys) && keys is not null ? keys : Unconfigured();
        }
        catch (IOException)
        {
            return Unconfigured();
        }
    }

    internal static TrustedReleaseKeys Create(params byte[][] subjectPublicKeys)
    {
        ArgumentNullException.ThrowIfNull(subjectPublicKeys);

        Dictionary<string, byte[]> keys = new(StringComparer.Ordinal);

        foreach (byte[] publicKey in subjectPublicKeys)
        {
            ArgumentNullException.ThrowIfNull(publicKey);

            byte[] validated = ValidatePublicKey(publicKey);
            if (!keys.TryAdd(GetKeyId(validated), validated))
            {
                throw new ArgumentException("Trusted release keys must be unique.", nameof(subjectPublicKeys));
            }
        }

        if (keys.Count is 0 or > MaximumKeyCount)
        {
            throw new ArgumentException("The trusted release key count is invalid.", nameof(subjectPublicKeys));
        }

        return new TrustedReleaseKeys(keys, isConfigured: true);
    }

    public bool Verify(ManagedReleaseSignature signature, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(signature);

        if (!IsConfigured || !_keys.TryGetValue(signature.KeyId, out byte[]? publicKey))
        {
            return false;
        }

        try
        {
            using ECDsa algorithm = ECDsa.Create();

            algorithm.ImportSubjectPublicKeyInfo(publicKey, out int bytesRead);

            return bytesRead == publicKey.Length && algorithm.VerifyData(data, signature.Signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static string GetKeyId(ReadOnlySpan<byte> subjectPublicKey)
    {
        return KeyIdPrefix + Convert.ToHexStringLower(SHA256.HashData(subjectPublicKey));
    }

    public static bool IsValidKeyId(string? keyId)
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

    internal static bool TryParse(ReadOnlyMemory<byte> json, out TrustedReleaseKeys? keys)
    {
        keys = null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 5 });

            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 || !root.TryGetProperty("schemaVersion", out JsonElement schema)
                || !schema.TryGetInt32(out int schemaVersion) || schemaVersion != CurrentSchemaVersion || !root.TryGetProperty("keys", out JsonElement entries)
                || entries.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            HashSet<string> rootProperties = root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);

            if (!rootProperties.SetEquals(["schemaVersion", "keys"]))
            {
                return false;
            }

            List<byte[]> publicKeys = [];

            foreach (JsonElement entry in entries.EnumerateArray())
            {
                if (publicKeys.Count == MaximumKeyCount || entry.ValueKind != JsonValueKind.Object || entry.EnumerateObject().Count() != 2
                    || !entry.TryGetProperty("algorithm", out JsonElement algorithm) || !entry.TryGetProperty("publicKey", out JsonElement encoded)
                    || algorithm.ValueKind != JsonValueKind.String || encoded.ValueKind != JsonValueKind.String
                    || !string.Equals(algorithm.GetString(), ManagedReleaseSignature.Algorithm, StringComparison.Ordinal))
                {
                    return false;
                }

                HashSet<string> entryProperties = entry.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);

                if (!entryProperties.SetEquals(["algorithm", "publicKey"]))
                {
                    return false;
                }

                string? value = encoded.GetString();
                if (string.IsNullOrEmpty(value))
                {
                    return false;
                }

                byte[] publicKey = Convert.FromBase64String(value);
                if (publicKey.Length is 0 or > MaximumPublicKeyLength)
                {
                    return false;
                }

                publicKeys.Add(publicKey);
            }

            keys = Create([.. publicKeys]);

            return true;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static byte[] ValidatePublicKey(ReadOnlySpan<byte> subjectPublicKey)
    {
        if (subjectPublicKey.Length is 0 or > MaximumPublicKeyLength)
        {
            throw new ArgumentException("The release public key length is invalid.", nameof(subjectPublicKey));
        }

        using ECDsa algorithm = ECDsa.Create();

        algorithm.ImportSubjectPublicKeyInfo(subjectPublicKey, out int bytesRead);

        ECParameters parameters = algorithm.ExportParameters(includePrivateParameters: false);

        if (bytesRead != subjectPublicKey.Length || parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value || parameters.Q.X is not { Length: 32 } || parameters.Q.Y is not { Length: 32 })
        {
            throw new CryptographicException("Only ECDSA P-256 release public keys are supported.");
        }

        return subjectPublicKey.ToArray();
    }

    private static TrustedReleaseKeys Unconfigured()
    {
        return new TrustedReleaseKeys(new Dictionary<string, byte[]>(StringComparer.Ordinal), isConfigured: false);
    }
}
