namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Implements the native 5517 AccountServer legacy stream transform (socket encryption mode 1).
/// This is compatibility obfuscation, not authenticated encryption. The two directions own independent
/// 16-bit stream positions and must never be shared between concurrent operations.
/// </summary>
internal sealed class LoginLegacyStreamCipher
{
    private const byte Mask = 0xAB;
    private static readonly byte[] s_streamA = CreateStreamA();
    private static readonly byte[] s_streamB = CreateStreamB();
    private ushort _sendPosition;
    private ushort _receivePosition;
    private int _sending;
    private int _receiving;

    /// <summary>Transforms client-to-AccountServer plaintext into native wire bytes in place.</summary>
    internal void EncryptOutbound(Span<byte> data)
    {
        if (Interlocked.Exchange(ref _sending, 1) != 0)
        {
            throw new InvalidOperationException("Concurrent sends cannot share a login cipher stream.");
        }

        try
        {
            Transform(data, ref _sendPosition);
        }
        finally
        {
            Volatile.Write(ref _sending, 0);
        }
    }

    /// <summary>Transforms AccountServer-to-client wire bytes into plaintext in place.</summary>
    internal void DecryptInbound(Span<byte> data)
    {
        if (Interlocked.Exchange(ref _receiving, 1) != 0)
        {
            throw new InvalidOperationException("Concurrent receives cannot share a login cipher stream.");
        }

        try
        {
            Transform(data, ref _receivePosition);
        }
        finally
        {
            Volatile.Write(ref _receiving, 0);
        }
    }

    // The server uses the inverse operation in both directions. The client applies the
    // documented forward operation to its outgoing plaintext AND incoming server ciphertext.
    private static void Transform(Span<byte> data, ref ushort position)
    {
        foreach (ref byte value in data)
        {
            byte result = (byte)(value ^ s_streamA[(byte)position] ^ s_streamB[(byte)(position >> 8)]);
            result = (byte)((result << 4) | (result >> 4));
            value = (byte)(result ^ Mask);
            position = unchecked((ushort)(position + 1));
        }
    }

    private static byte[] CreateStreamA()
    {
        byte[] table = new byte[256];
        byte key = 0x9D;
        for (int index = 0; index < table.Length; index++)
        {
            table[index] = key;
            byte multiplied = unchecked((byte)(key * 0xFA));
            byte added = unchecked((byte)(multiplied + 0x0F));
            key = unchecked((byte)(added * key + 0x13));
        }
        return table;
    }

    private static byte[] CreateStreamB()
    {
        byte[] table = new byte[256];
        byte key = 0x62;
        for (int index = 0; index < table.Length; index++)
        {
            table[index] = key;
            byte multiplied = unchecked((byte)(key * 0x5C));
            byte subtracted = unchecked((byte)(0x79 - multiplied));
            key = unchecked((byte)(subtracted * key + 0x6D));
        }
        return table;
    }
}
