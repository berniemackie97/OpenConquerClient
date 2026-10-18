using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace OpenConquer.Networking.Game.Handshake;

/// <summary>
/// Parses one decrypted native 5517 GameServer DH challenge. Bootstrap CAST5
/// decryption, TCP buffering, key agreement and connection admission are separate phases.
/// </summary>
internal static class GameServerChallengeDecoder
{
    internal const int PrefixLength = 11;
    internal const int HeaderLength = PrefixLength + sizeof(int);
    internal const int MaximumDeclaredLength = 1024;
    internal const int MaximumFrameLength = PrefixLength + MaximumDeclaredLength;

    // The negotiated group currently supported by OpenConquerServer.
    internal const string PrimeHex =
        "E7A69EBDF105F2A6BBDEAD7E798F76A209AD73FB466431E2E7352ED262F8C558F10BEFEA977DE9E21DCEE9B04D245F300ECCBBA03E72630556D011023F9E857F";

    private static readonly BigInteger s_prime =
        new(Convert.FromHexString(PrimeHex), isUnsigned: true, isBigEndian: true);

    /// <summary>
    /// Returns false only while fewer than 15 decrypted bytes are present. A bad
    /// declared length is terminal, never interpreted as a request for more data.
    /// </summary>
    internal static bool TryGetFrameLength(ReadOnlySpan<byte> decryptedPrefix, out int frameLength)
    {
        frameLength = 0;
        if (decryptedPrefix.Length < HeaderLength)
        {
            return false;
        }

        int declaredLength = BinaryPrimitives.ReadInt32LittleEndian(decryptedPrefix[PrefixLength..]);
        if (declaredLength is < 12 or > MaximumDeclaredLength)
        {
            throw new InvalidDataException("Invalid GameServer challenge length.");
        }

        frameLength = PrefixLength + declaredLength;
        return true;
    }

    /// <summary>
    /// Parses an exact decrypted frame. A valid prefix with an incomplete frame
    /// returns false; malformed complete frames terminate with InvalidDataException.
    /// No fields are returned until all six chunks and the trailer validate.
    /// </summary>
    internal static bool TryRead(ReadOnlySpan<byte> plaintext, out GameServerChallenge? challenge)
    {
        challenge = null;
        if (!TryGetFrameLength(plaintext, out int length) || plaintext.Length < length)
        {
            return false;
        }

        if (plaintext.Length != length)
        {
            throw new InvalidDataException("GameServer challenge must contain exactly one envelope.");
        }

        if (!plaintext[^8..].SequenceEqual("TQServer"u8))
        {
            throw new InvalidDataException("GameServer challenge has an invalid signature.");
        }

        int payloadEnd = plaintext.Length - 8;
        int offset = HeaderLength;

        ReadOnlySpan<byte> junk = ReadChunk(plaintext, ref offset, payloadEnd);
        ReadOnlySpan<byte> serverOutboundIv = ReadChunk(plaintext, ref offset, payloadEnd);
        ReadOnlySpan<byte> serverInboundIv = ReadChunk(plaintext, ref offset, payloadEnd);
        ReadOnlySpan<byte> primeHex = ReadChunk(plaintext, ref offset, payloadEnd);
        ReadOnlySpan<byte> generatorHex = ReadChunk(plaintext, ref offset, payloadEnd);
        ReadOnlySpan<byte> publicKeyHex = ReadChunk(plaintext, ref offset, payloadEnd);

        if (offset != payloadEnd || junk.Length is < 1 or > 64 ||
            serverOutboundIv.Length != 8 || serverInboundIv.Length != 8 ||
            !EqualsAsciiIgnoreCase(primeHex, PrimeHex) ||
            !generatorHex.SequenceEqual("05"u8) ||
            !TryParsePublicKey(publicKeyHex, out _))
        {
            throw new InvalidDataException("GameServer challenge parameters are invalid.");
        }

        // Both directional IVs and the public key are copied out of the caller's
        // temporary plaintext buffer before it is cleared by its future owner.
        challenge = GameServerChallenge.FromValidatedParameters(serverOutboundIv,
            serverInboundIv, publicKeyHex);
        return true;
    }

    private static ReadOnlySpan<byte> ReadChunk(ReadOnlySpan<byte> frame, ref int offset, int end)
    {
        if (end - offset < sizeof(int))
        {
            throw new InvalidDataException("GameServer challenge has a truncated chunk header.");
        }

        int length = BinaryPrimitives.ReadInt32LittleEndian(frame[offset..]);
        offset += sizeof(int);
        if (length <= 0 || length > end - offset)
        {
            throw new InvalidDataException("GameServer challenge has an invalid chunk length.");
        }

        ReadOnlySpan<byte> value = frame.Slice(offset, length);
        offset += length;
        return value;
    }

    private static bool EqualsAsciiIgnoreCase(ReadOnlySpan<byte> input, string expected)
    {
        if (input.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            byte value = input[index];
            if (value is >= (byte)'a' and <= (byte)'z')
            {
                value = (byte)(value - 32);
            }

            if (value != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    internal static bool TryParsePublicKey(ReadOnlySpan<byte> encoded, out BigInteger value)
    {
        value = BigInteger.Zero;
        if (encoded.IsEmpty || encoded.Length > PrimeHex.Length)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[(PrimeHex.Length + 1) / 2];
        int sourceOffset = 0;
        int outputOffset = 0;

        if ((encoded.Length & 1) != 0)
        {
            int leading = HexNibble(encoded[0]);
            if (leading < 0)
            {
                return false;
            }

            bytes[outputOffset++] = (byte)leading;
            sourceOffset = 1;
        }

        while (sourceOffset < encoded.Length)
        {
            int high = HexNibble(encoded[sourceOffset++]);
            int low = HexNibble(encoded[sourceOffset++]);
            if (high < 0 || low < 0)
            {
                return false;
            }

            bytes[outputOffset++] = (byte)((high << 4) | low);
        }

        value = new BigInteger(bytes[..outputOffset], isUnsigned: true, isBigEndian: true);
        return value > BigInteger.One && value < s_prime - BigInteger.One;
    }

    private static int HexNibble(byte value)
    {
        if (value is >= (byte)'0' and <= (byte)'9')
        {
            return value - '0';
        }

        if (value is >= (byte)'A' and <= (byte)'F')
        {
            return value - 'A' + 10;
        }

        return value is >= (byte)'a' and <= (byte)'f' ? value - 'a' + 10 : -1;
    }
}

/// <summary>
/// Validated parameters of one decrypted challenge, not an authenticated peer.
/// ClientInboundIv is the server's outbound IV, and ClientOutboundIv is its inbound IV.
/// </summary>
internal sealed class GameServerChallenge
{
    private readonly byte[] _clientInboundIv;
    private readonly byte[] _clientOutboundIv;

    private GameServerChallenge(byte[] clientInboundIv, byte[] clientOutboundIv, string serverPublicKeyHex)
    {
        _clientInboundIv = clientInboundIv;
        _clientOutboundIv = clientOutboundIv;
        ServerPublicKeyHex = serverPublicKeyHex;
    }

    // Even friend-assembly callers cannot construct a result with invalid
    // parameters or retain mutable references to caller-owned IV arrays.
    internal static GameServerChallenge FromValidatedParameters(
        ReadOnlySpan<byte> serverOutboundIv, ReadOnlySpan<byte> serverInboundIv,
        ReadOnlySpan<byte> serverPublicKeyHex)
    {
        if (serverOutboundIv.Length != 8 || serverInboundIv.Length != 8 ||
            !GameServerChallengeDecoder.TryParsePublicKey(serverPublicKeyHex, out _))
        {
            throw new InvalidDataException("GameServer challenge parameters are invalid.");
        }

        return new GameServerChallenge(serverOutboundIv.ToArray(),
            serverInboundIv.ToArray(), Encoding.ASCII.GetString(serverPublicKeyHex));
    }

    internal ReadOnlySpan<byte> ClientInboundIv => _clientInboundIv;
    internal ReadOnlySpan<byte> ClientOutboundIv => _clientOutboundIv;
    internal string ServerPublicKeyHex
    {
        get;
    }
}
