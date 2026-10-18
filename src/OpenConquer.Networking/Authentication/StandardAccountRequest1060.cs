using System.Buffers.Binary;
using System.Security.Cryptography;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Serializes the verified standard 5517 AccountServer request envelope (type 1060).
/// This type does not encrypt credentials, establish a connection, or authorize login.
/// </summary>
internal static class StandardAccountRequest1060
{
    internal const int PacketLength = 276;
    internal const ushort PacketType = 1060;
    internal const int AccountOffset = 4;
    internal const int AccountFieldLength = 128;
    internal const int CredentialOffset = 132;
    internal const int CredentialFieldLength = 128;
    internal const int StandardEncryptedCredentialLength = 32;
    internal const int ServerOffset = 260;
    internal const int ServerFieldLength = 16;

    /// <summary>
    /// Writes the full plaintext packet, which the session cipher encrypts afterwards. The credential argument
    /// must already be the seeded-RC5 ciphertext, not the password or the keypad-encoded buffer.
    /// </summary>
    internal static void Write(Span<byte> destination, ReadOnlySpan<byte> accountName, ReadOnlySpan<byte> encryptedCredentialBlock, ReadOnlySpan<byte> protocolServerName)
    {
        if (destination.Length != PacketLength)
        {
            throw new ArgumentException($"A type-1060 packet requires exactly {PacketLength} destination bytes.", nameof(destination));
        }

        ValidateName(accountName, AccountFieldLength, nameof(accountName));
        ValidateName(protocolServerName, ServerFieldLength, nameof(protocolServerName));

        if (encryptedCredentialBlock.Length != StandardEncryptedCredentialLength)
        {
            throw new ArgumentException($"The standard 5517 credential block must contain exactly {StandardEncryptedCredentialLength} encrypted bytes.", nameof(encryptedCredentialBlock));
        }

        // Snapshot inputs before zero-filling the destination: callers may supply overlapping spans.
        Span<byte> accountCopy = stackalloc byte[AccountFieldLength];
        Span<byte> credentialCopy = stackalloc byte[StandardEncryptedCredentialLength];
        Span<byte> serverCopy = stackalloc byte[ServerFieldLength];
        accountName.CopyTo(accountCopy);
        encryptedCredentialBlock.CopyTo(credentialCopy);
        protocolServerName.CopyTo(serverCopy);

        try
        {
            destination.Clear();
            BinaryPrimitives.WriteUInt16LittleEndian(destination, PacketLength);
            BinaryPrimitives.WriteUInt16LittleEndian(destination[sizeof(ushort)..], PacketType);
            accountCopy[..accountName.Length].CopyTo(destination.Slice(AccountOffset, accountName.Length));
            credentialCopy.CopyTo(destination.Slice(CredentialOffset, StandardEncryptedCredentialLength));
            serverCopy[..protocolServerName.Length].CopyTo(destination.Slice(ServerOffset, protocolServerName.Length));
        }
        finally
        {
            accountCopy.Clear();
            CryptographicOperations.ZeroMemory(credentialCopy);
            serverCopy.Clear();
        }
    }

    private static void ValidateName(ReadOnlySpan<byte> value, int maximumLength, string parameterName)
    {
        if (value.IsEmpty || value.Length > maximumLength)
        {
            throw new ArgumentException($"Name must contain between 1 and {maximumLength} bytes.", parameterName);
        }

        foreach (byte character in value)
        {
            if (character is < 0x20 or 0x7F)
            {
                throw new ArgumentException("A wire name cannot contain NUL or ASCII control bytes.", parameterName);
            }
        }
    }
}
