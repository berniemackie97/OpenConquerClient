using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace OpenConquer.Networking.Game.Handshake;

/// <summary>
/// One offline native 5517 client DH exchange: fresh private exponent, pinned-group
/// key agreement, OpenSSL-compatible shared-secret layout, and a bootstrap-encrypted
/// client response. Does not create or authorize a GameServer connection.
/// </summary>
internal sealed class GameClientDhExchange : IDisposable
{
    internal const int SessionKeyMaterialLength = 64;
    internal const int InitializationVectorLength = 8;

    private static readonly BigInteger s_prime =
        new(Convert.FromHexString(GameServerChallengeDecoder.PrimeHex), isUnsigned: true, isBigEndian: true);

    private readonly Lock _gate = new();
    private readonly byte[] _encryptedResponse;
    private readonly byte[] _sessionKeyMaterial;
    private readonly byte[] _clientInboundIv;
    private readonly byte[] _clientOutboundIv;

    private bool _responseIssued;
    private bool _sessionParametersTransferred;
    private bool _disposed;

    private GameClientDhExchange(byte[] encryptedResponse, byte[] sessionKeyMaterial,
        byte[] clientInboundIv, byte[] clientOutboundIv)
    {
        _encryptedResponse = encryptedResponse;
        _sessionKeyMaterial = sessionKeyMaterial;
        _clientInboundIv = clientInboundIv;
        _clientOutboundIv = clientOutboundIv;
    }

    /// <summary>
    /// Production path: only cryptographically secure entropy is used for the private
    /// exponent, seven-byte prefix, and native 10–49-byte padding.
    /// </summary>
    internal static GameClientDhExchange Create(GameServerChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        byte[] privateExponent = new byte[SessionKeyMaterialLength];
        byte[] prefix = new byte[GameClientDhResponseEnvelope.PrefixLength];
        byte[] padding = new byte[RandomNumberGenerator.GetInt32(
            GameClientDhResponseEnvelope.MinimumPaddingLength,
            GameClientDhResponseEnvelope.MaximumPaddingLength + 1)];

        try
        {
            GeneratePrivateExponent(privateExponent);
            RandomNumberGenerator.Fill(prefix);
            RandomNumberGenerator.Fill(padding);
            return CreateFromParameters(challenge, privateExponent, prefix, padding);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateExponent);
            CryptographicOperations.ZeroMemory(prefix);
            CryptographicOperations.ZeroMemory(padding);
        }
    }

    /// <summary>
    /// Deterministic protocol-verification entry point. It performs the same validation
    /// and key agreement as the production RNG path. Never use caller-chosen exponents
    /// for live connections.
    /// </summary>
    internal static GameClientDhExchange CreateFromParameters(GameServerChallenge challenge,
        ReadOnlySpan<byte> privateExponent, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> padding)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        if (privateExponent.IsEmpty || privateExponent.Length > SessionKeyMaterialLength)
        {
            throw new ArgumentException("Invalid DH private exponent length.", nameof(privateExponent));
        }

        BigInteger exponent = new(privateExponent, isUnsigned: true, isBigEndian: true);
        if (exponent <= BigInteger.One || exponent >= s_prime - BigInteger.One)
        {
            throw new ArgumentException("Invalid DH private exponent range.", nameof(privateExponent));
        }

        if (prefix.Length != GameClientDhResponseEnvelope.PrefixLength ||
            padding.Length is < GameClientDhResponseEnvelope.MinimumPaddingLength or
                > GameClientDhResponseEnvelope.MaximumPaddingLength)
        {
            throw new ArgumentException("Invalid native client DH response entropy lengths.");
        }

        byte[] serverPublicHex = Encoding.ASCII.GetBytes(challenge.ServerPublicKeyHex);
        if (!GameServerChallengeDecoder.TryParsePublicKey(serverPublicHex, out BigInteger serverPublic))
        {
            throw new InvalidDataException("The GameServer DH challenge public key is invalid.");
        }

        BigInteger clientPublic = BigInteger.ModPow(new BigInteger(5), exponent, s_prime);
        if (clientPublic <= BigInteger.One || clientPublic >= s_prime - BigInteger.One)
        {
            throw new CryptographicException("The generated client DH public key is invalid.");
        }

        BigInteger sharedSecret = BigInteger.ModPow(serverPublic, exponent, s_prime);
        if (sharedSecret <= BigInteger.One || sharedSecret >= s_prime - BigInteger.One)
        {
            throw new CryptographicException("The derived DH shared secret is invalid.");
        }

        byte[] sharedSecretBytes = sharedSecret.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] sessionKey = new byte[SessionKeyMaterialLength];
        byte[]? encryptedResponse = null;
        byte[]? inboundIv = null;
        byte[]? outboundIv = null;

        try
        {
            if (sharedSecretBytes.Length > sessionKey.Length)
            {
                throw new CryptographicException("The derived DH shared secret exceeds the pinned group size.");
            }

            // Native OpenSSL DH_compute_key writes a minimal big-endian result
            // at the START of a zeroed DH_size-sized buffer, not left-padded.
            sharedSecretBytes.CopyTo(sessionKey, 0);

            string publicHex = clientPublic.ToString("X", CultureInfo.InvariantCulture).TrimStart('0');
            byte[] publicHexBytes = Encoding.ASCII.GetBytes(publicHex);
            int responseLength = GameClientDhResponseEnvelope.GetFrameLength(padding.Length, publicHexBytes);
            byte[] plaintext = new byte[responseLength];
            encryptedResponse = new byte[responseLength];

            try
            {
                GameClientDhResponseEnvelope.Write(plaintext, prefix, padding, publicHexBytes);
                using GameHandshakeBootstrapCipher cipher = GameHandshakeBootstrapCipher.CreateEncryptor();
                cipher.Transform(plaintext, encryptedResponse);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            inboundIv = challenge.ClientInboundIv.ToArray();
            outboundIv = challenge.ClientOutboundIv.ToArray();
            return new GameClientDhExchange(encryptedResponse, sessionKey, inboundIv, outboundIv);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(sessionKey);
            if (encryptedResponse is not null)
            {
                CryptographicOperations.ZeroMemory(encryptedResponse);
            }

            if (inboundIv is not null)
            {
                CryptographicOperations.ZeroMemory(inboundIv);
            }

            if (outboundIv is not null)
            {
                CryptographicOperations.ZeroMemory(outboundIv);
            }

            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedSecretBytes);
        }
    }

    internal int EncryptedResponseLength
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _encryptedResponse.Length;
            }
        }
    }

    /// <summary>
    /// Copies the encrypted response exactly once to caller-owned storage. Copying
    /// is not evidence of network transmission; any live owner must enforce that
    /// ordering and abort its connection after an uncertain or failed send.
    /// </summary>
    internal void WriteEncryptedResponse(Span<byte> destination)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_responseIssued)
            {
                throw new InvalidOperationException("The client DH response has already been issued.");
            }

            if (destination.Length != _encryptedResponse.Length)
            {
                throw new ArgumentException("The encrypted client DH response destination has an invalid length.", nameof(destination));
            }

            _encryptedResponse.CopyTo(destination);
            _responseIssued = true;
        }
    }

    /// <summary>
    /// Transfers the DH_size-sized native CAST5 key material and both directional
    /// IVs exactly once. The caller must clear its copies when the session ends.
    /// </summary>
    internal void TransferSessionParameters(Span<byte> keyMaterial,
        Span<byte> clientInboundIv, Span<byte> clientOutboundIv)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_responseIssued)
            {
                throw new InvalidOperationException("The client DH response must be issued before session parameters transfer.");
            }

            if (_sessionParametersTransferred)
            {
                throw new InvalidOperationException("The client DH session parameters have already been transferred.");
            }

            if (keyMaterial.Length != SessionKeyMaterialLength ||
                clientInboundIv.Length != InitializationVectorLength ||
                clientOutboundIv.Length != InitializationVectorLength)
            {
                throw new ArgumentException("Invalid native GameServer session parameter destination lengths.");
            }

            // Distinct outputs prevent aliasing from corrupting key material and IVs.
            if (keyMaterial.Overlaps(clientInboundIv) ||
                keyMaterial.Overlaps(clientOutboundIv) ||
                clientInboundIv.Overlaps(clientOutboundIv))
            {
                throw new ArgumentException("The key and directional IV destinations must not overlap.");
            }

            _sessionKeyMaterial.CopyTo(keyMaterial);
            _clientInboundIv.CopyTo(clientInboundIv);
            _clientOutboundIv.CopyTo(clientOutboundIv);
            _sessionParametersTransferred = true;
            CryptographicOperations.ZeroMemory(_sessionKeyMaterial);
            CryptographicOperations.ZeroMemory(_clientInboundIv);
            CryptographicOperations.ZeroMemory(_clientOutboundIv);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CryptographicOperations.ZeroMemory(_encryptedResponse);
            CryptographicOperations.ZeroMemory(_sessionKeyMaterial);
            CryptographicOperations.ZeroMemory(_clientInboundIv);
            CryptographicOperations.ZeroMemory(_clientOutboundIv);
        }
    }

    private static void GeneratePrivateExponent(Span<byte> destination)
    {
        // Rejection sampling over the full 512-bit pinned group without modulo bias.
        // In normal conditions acceptance is high; cap attempts to avoid an
        // unbounded loop if the RNG or environment fails catastrophically.
        for (int attempt = 0; attempt < 128; attempt++)
        {
            RandomNumberGenerator.Fill(destination);
            BigInteger value = new(destination, isUnsigned: true, isBigEndian: true);
            if (value > BigInteger.One && value < s_prime - BigInteger.One)
            {
                BigInteger publicKey = BigInteger.ModPow(new BigInteger(5), value, s_prime);
                if (publicKey > BigInteger.One && publicKey < s_prime - BigInteger.One)
                {
                    return;
                }
            }
        }

        throw new CryptographicException("Unable to generate a valid native GameServer DH private exponent.");
    }
}
