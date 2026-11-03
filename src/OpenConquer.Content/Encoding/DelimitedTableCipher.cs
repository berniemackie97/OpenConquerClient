namespace OpenConquer.Content.Encoding;

internal static class DelimitedTableCipher
{
    internal static void DecodeInPlace(Span<byte> bytes)
    {
        Span<byte> key = stackalloc byte[128];
        uint state = 0x2537;

        for (int index = 0; index < key.Length; index++)
        {
            state = unchecked(state * 214013 + 2531011);
            key[index] = (byte)(state >> 16);
        }

        for (int index = 0; index < bytes.Length; index++)
        {
            byte value = (byte)(bytes[index] ^ key[index % key.Length]);
            int rotation = index & 7;
            bytes[index] = unchecked((byte)((value >> rotation) | (value << (8 - rotation))));
        }
    }
}
