using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace OpenConquer.Networking.Authentication;

/// <summary>Native 5517 credential-field RC5-32/12/16 encryption; never a transport cipher or password encoder.</summary>
internal static class CredentialRc5
{
    internal const int BlockLength = 8;
    internal const int StandardFieldLength = 32;
    private const int SubkeyCount = 26;
    private const int KeyWordCount = 4;

    /// <summary>Encrypts the 32 already-keypad-encoded credential bytes for the standard type-1060 variant.</summary>
    internal static void EncryptStandard(uint loginSeed, ReadOnlySpan<byte> encodedCredential, Span<byte> destination)
    {
        if (encodedCredential.Length != StandardFieldLength || destination.Length != StandardFieldLength)
        {
            throw new ArgumentException("Standard 5517 credentials require exactly 32 source and destination bytes.");
        }

        Span<byte> key = stackalloc byte[LoginSeedKey.KeyLength];
        Span<byte> source = stackalloc byte[StandardFieldLength];
        Span<uint> subkeys = stackalloc uint[SubkeyCount];
        try
        {
            encodedCredential.CopyTo(source);
            LoginSeedKey.Derive(loginSeed, key);
            ExpandKey(key, subkeys);
            for (int offset = 0; offset < StandardFieldLength; offset += BlockLength)
            {
                EncryptOne(subkeys, source.Slice(offset, BlockLength), destination.Slice(offset, BlockLength));
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(source);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(subkeys));
        }
    }

    /// <summary>Single-block primitive for verified RC5 test vectors; callers supply an exact 16-byte key.</summary>
    internal static void EncryptBlock(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, Span<byte> destination)
    {
        if (key.Length != LoginSeedKey.KeyLength || plaintext.Length != BlockLength || destination.Length != BlockLength)
        {
            throw new ArgumentException("RC5-32/12/16 requires a 16-byte key and exact 8-byte blocks.");
        }

        Span<uint> subkeys = stackalloc uint[SubkeyCount];
        try
        {
            ExpandKey(key, subkeys);
            EncryptOne(subkeys, plaintext, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(subkeys));
        }
    }

    private static void ExpandKey(ReadOnlySpan<byte> key, Span<uint> subkeys)
    {
        Span<uint> words = stackalloc uint[KeyWordCount];
        try
        {
            for (int index = 0; index < KeyWordCount; index++)
            {
                words[index] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(index * sizeof(uint), sizeof(uint)));
            }

            subkeys[0] = 0xB7E15163u;
            for (int index = 1; index < SubkeyCount; index++)
            {
                subkeys[index] = unchecked(subkeys[index - 1] + 0x9E3779B9u);
            }

            uint a = 0;
            uint b = 0;
            int i = 0;
            int j = 0;
            for (int step = 0; step < 3 * SubkeyCount; step++)
            {
                a = subkeys[i] = BitOperations.RotateLeft(unchecked(subkeys[i] + a + b), 3);
                b = words[j] = BitOperations.RotateLeft(unchecked(words[j] + a + b), unchecked((int)(a + b)));
                i = (i + 1) % SubkeyCount;
                j = (j + 1) % KeyWordCount;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(words));
        }
    }

    private static void EncryptOne(ReadOnlySpan<uint> subkeys, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        uint a = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(source) + subkeys[0]);
        uint b = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(source[sizeof(uint)..]) + subkeys[1]);

        for (int round = 1; round <= 12; round++)
        {
            a = unchecked(BitOperations.RotateLeft(a ^ b, unchecked((int)b)) + subkeys[2 * round]);
            b = unchecked(BitOperations.RotateLeft(b ^ a, unchecked((int)a)) + subkeys[2 * round + 1]);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination, a);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[sizeof(uint)..], b);
    }
}
