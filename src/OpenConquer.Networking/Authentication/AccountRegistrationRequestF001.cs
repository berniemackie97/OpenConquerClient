using System.Buffers.Binary;
using System.Security.Cryptography;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Serializes the OpenConquer account-registration request (type 0xF001). Retail 5517 registered accounts on a
/// website; this packet is a project extension sent in place of 1060 on a fresh AccountServer connection. Its
/// body is the first 256 bytes of 1060: the account field and the seeded-RC5 credential field, with no server
/// name because accounts are not bound to a realm.
/// </summary>
internal static class AccountRegistrationRequestF001
{
    internal const int PacketLength = 260;
    internal const ushort PacketType = 0xF001;
    internal const int AccountOffset = StandardAccountRequest1060.AccountOffset;
    internal const int AccountFieldLength = StandardAccountRequest1060.AccountFieldLength;
    internal const int CredentialOffset = StandardAccountRequest1060.CredentialOffset;
    internal const int StandardEncryptedCredentialLength = StandardAccountRequest1060.StandardEncryptedCredentialLength;

    /// <summary>
    /// Writes the full plaintext packet, which the session cipher encrypts afterwards. The credential argument
    /// must already be the seeded-RC5 ciphertext.
    /// </summary>
    internal static void Write(Span<byte> destination, ReadOnlySpan<byte> accountName, ReadOnlySpan<byte> encryptedCredentialBlock)
    {
        if (destination.Length != PacketLength)
        {
            throw new ArgumentException($"A type-0xF001 packet requires exactly {PacketLength} destination bytes.", nameof(destination));
        }

        if (accountName.IsEmpty || accountName.Length > AccountFieldLength)
        {
            throw new ArgumentException($"Name must contain between 1 and {AccountFieldLength} bytes.", nameof(accountName));
        }

        foreach (byte character in accountName)
        {
            if (character is < 0x20 or 0x7F)
            {
                throw new ArgumentException("A wire name cannot contain NUL or ASCII control bytes.", nameof(accountName));
            }
        }

        if (encryptedCredentialBlock.Length != StandardEncryptedCredentialLength)
        {
            throw new ArgumentException($"The credential block must contain exactly {StandardEncryptedCredentialLength} encrypted bytes.", nameof(encryptedCredentialBlock));
        }

        // Snapshot inputs before zero-filling the destination: callers may supply overlapping spans.
        Span<byte> accountCopy = stackalloc byte[AccountFieldLength];
        Span<byte> credentialCopy = stackalloc byte[StandardEncryptedCredentialLength];
        accountName.CopyTo(accountCopy);
        encryptedCredentialBlock.CopyTo(credentialCopy);

        try
        {
            destination.Clear();
            BinaryPrimitives.WriteUInt16LittleEndian(destination, PacketLength);
            BinaryPrimitives.WriteUInt16LittleEndian(destination[sizeof(ushort)..], PacketType);
            accountCopy[..accountName.Length].CopyTo(destination.Slice(AccountOffset, accountName.Length));
            credentialCopy.CopyTo(destination.Slice(CredentialOffset, StandardEncryptedCredentialLength));
        }
        finally
        {
            accountCopy.Clear();
            CryptographicOperations.ZeroMemory(credentialCopy);
        }
    }
}
