namespace OpenConquer.Networking.Authentication;

/// <summary>Reproduces MSVCRT srand/rand key material derived from the 5517 AccountServer login seed.</summary>
internal static class LoginSeedKey
{
    internal const int KeyLength = 16;

    internal static void Derive(uint seed, Span<byte> destination)
    {
        if (destination.Length != KeyLength)
        {
            throw new ArgumentException("The native credential key must have exactly 16 bytes.", nameof(destination));
        }

        uint state = seed;
        for (int index = 0; index < destination.Length; index++)
        {
            state = unchecked(state * 0x343FDu + 0x269EC3u);
            destination[index] = (byte)((state >> 16) & 0xFFu);
        }
    }
}
