using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.Handshake;

/// <summary>
/// Exact plaintext native 5517 client DH response, before independent
/// fixed-key CAST5-CFB64 bootstrap encryption. Not a packet-header frame.
/// </summary>
internal static class GameClientDhResponseEnvelope
{
    internal const int PrefixLength = 7;
    internal const int MinimumPaddingLength = 10;
    internal const int MaximumPaddingLength = 49;
    internal const int MaximumPublicKeyHexLength = GameServerChallengeDecoder.PrimeHex.Length;

    internal static int GetFrameLength(int paddingLength, ReadOnlySpan<byte> clientPublicKeyHex)
    {
        if (paddingLength is < MinimumPaddingLength or > MaximumPaddingLength)
        {
            throw new ArgumentOutOfRangeException(nameof(paddingLength));
        }

        if (!GameServerChallengeDecoder.TryParsePublicKey(clientPublicKeyHex, out _))
        {
            throw new ArgumentException("Invalid client Diffie-Hellman public key.", nameof(clientPublicKeyHex));
        }

        return checked(PrefixLength + sizeof(int) + sizeof(int) + paddingLength +
            sizeof(int) + clientPublicKeyHex.Length + "TQClient"u8.Length);
    }

    internal static void Write(Span<byte> destination, ReadOnlySpan<byte> randomPrefix,
        ReadOnlySpan<byte> randomPadding, ReadOnlySpan<byte> clientPublicKeyHex)
    {
        if (randomPrefix.Length != PrefixLength)
        {
            throw new ArgumentException("The response prefix must be exactly seven bytes.", nameof(randomPrefix));
        }

        int frameLength = GetFrameLength(randomPadding.Length, clientPublicKeyHex);
        if (destination.Length != frameLength)
        {
            throw new ArgumentException("The client DH response destination has an invalid length.", nameof(destination));
        }

        // The caller may provide prefix/padding from within the destination.
        // Preserve those bytes before zeroing and writing the envelope.
        Span<byte> prefix = stackalloc byte[PrefixLength];
        Span<byte> padding = stackalloc byte[MaximumPaddingLength];
        Span<byte> publicKey = stackalloc byte[MaximumPublicKeyHexLength];
        randomPrefix.CopyTo(prefix);
        randomPadding.CopyTo(padding);
        clientPublicKeyHex.CopyTo(publicKey);
        try
        {
            destination.Clear();
            prefix.CopyTo(destination);
            int offset = PrefixLength;

            BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], frameLength - PrefixLength);
            offset += sizeof(int);
            BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], randomPadding.Length);
            offset += sizeof(int);
            padding[..randomPadding.Length].CopyTo(destination[offset..]);
            offset += randomPadding.Length;
            BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], clientPublicKeyHex.Length);
            offset += sizeof(int);
            publicKey[..clientPublicKeyHex.Length].CopyTo(destination[offset..]);
            offset += clientPublicKeyHex.Length;
            "TQClient"u8.CopyTo(destination[offset..]);
        }
        finally
        {
            prefix.Clear();
            padding.Clear();
            publicKey.Clear();
        }
    }
}
