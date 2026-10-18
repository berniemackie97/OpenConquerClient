using System.Security.Cryptography;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Builds the standard native 1060 plaintext envelope from caller-owned credential characters.
/// Pure composition: no socket access, session authorization, or account-login state changes.
/// </summary>
internal static class StandardAccountCredentialComposer
{
    /// <summary>
    /// Writes the entire 276-byte request: account-seeded keypad bytes, login-seed RC5
    /// encryption of the first 32 credential bytes, and the existing 1060 envelope.
    /// Neither the source password nor intermediate credential material is retained.
    /// </summary>
    internal static void Write(Span<byte> destination, ReadOnlySpan<byte> accountName,
        ReadOnlySpan<char> password, ReadOnlySpan<byte> protocolServerName, uint loginSeed)
    {
        if (destination.Length != StandardAccountRequest1060.PacketLength)
        {
            throw new ArgumentException("The 1060 request must be exactly 276 bytes.", nameof(destination));
        }

        Span<byte> encoded = stackalloc byte[CredentialRc5.StandardFieldLength];
        Span<byte> encrypted = stackalloc byte[CredentialRc5.StandardFieldLength];

        try
        {
            LoginPasswordKeypadEncoder.Encode(accountName, password, encoded);
            CredentialRc5.EncryptStandard(loginSeed, encoded, encrypted);
            StandardAccountRequest1060.Write(destination, accountName, encrypted, protocolServerName);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
            CryptographicOperations.ZeroMemory(encrypted);
        }
    }
}
