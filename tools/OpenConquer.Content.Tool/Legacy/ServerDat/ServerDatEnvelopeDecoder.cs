using System.IO.Compression;
using System.Numerics;

namespace OpenConquer.Content.Tool.Legacy.ServerDat;

/// <summary>
/// Decodes the verified RSA/PKCS#1/gzip envelope used by retail <c>Server.dat</c>.
/// </summary>
/// <remarks>
/// Native 5517 reads RSA blocks at the public modulus width, performs the public-key operation with
/// exponent 65537 and PKCS#1 type-1 padding, concatenates each extracted payload, verifies the gzip
/// signature, and inflates the result before XML parsing.
/// </remarks>
internal static class ServerDatEnvelopeDecoder
{
    internal const int EncryptedBlockSize = 256;
    internal const int MaximumEncryptedBlockCount = 64;
    internal const int MaximumInflatedXmlLength = 1024 * 1024;

    private const int MinimumType1PaddingLength = 8;
    private const byte GzipMagicByte0 = 0x1F;
    private const byte GzipMagicByte1 = 0x8B;

    /// <summary>
    /// Decodes retail <c>Server.dat</c> using the independently verified native 5517 public key.
    /// </summary>
    public static byte[] DecodeToXml(ReadOnlySpan<byte> encryptedServerDat)
    {
        return DecodeToXml(encryptedServerDat, ServerDatNativePublicKey.Modulus);
    }

    /// <summary>
    /// Decodes a Server.dat-compatible envelope using a supplied 2048-bit modulus.
    /// </summary>
    /// <remarks>
    /// This overload exists so parity tests can generate independent RSA keypairs while exercising
    /// the exact production envelope implementation.
    /// </remarks>
    internal static byte[] DecodeToXml(ReadOnlySpan<byte> encryptedServerDat, ReadOnlySpan<byte> publicModulus)
    {
        ValidatePublicModulus(publicModulus);

        if (encryptedServerDat.IsEmpty)
        {
            throw new InvalidDataException("Server.dat is empty.");
        }

        if (encryptedServerDat.Length % EncryptedBlockSize != 0)
        {
            throw new InvalidDataException($"Server.dat length {encryptedServerDat.Length} is not a whole number of {EncryptedBlockSize}-byte RSA blocks.");
        }

        int blockCount = encryptedServerDat.Length / EncryptedBlockSize;

        if (blockCount > MaximumEncryptedBlockCount)
        {
            throw new InvalidDataException($"Server.dat contains {blockCount} RSA blocks, which exceeds the modern safety limit of {MaximumEncryptedBlockCount}.");
        }

        BigInteger modulus = new(publicModulus, isUnsigned: true, isBigEndian: true);
        BigInteger exponent = new(ServerDatNativePublicKey.PublicExponent);

        int maximumDecodedPayloadLength = checked(blockCount * (EncryptedBlockSize - 3 - MinimumType1PaddingLength));

        using MemoryStream decodedPayload = new(maximumDecodedPayloadLength);

        Span<byte> decryptedBlock = stackalloc byte[EncryptedBlockSize];

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            ReadOnlySpan<byte> encryptedBlock = encryptedServerDat.Slice(blockIndex * EncryptedBlockSize, EncryptedBlockSize);

            BigInteger ciphertext = new(encryptedBlock, isUnsigned: true, isBigEndian: true);

            if (ciphertext >= modulus)
            {
                throw new InvalidDataException($"Server.dat RSA block {blockIndex} is outside the public modulus range.");
            }

            BigInteger plaintext = BigInteger.ModPow(ciphertext, exponent, modulus);

            decryptedBlock.Clear();

            byte[] encodedPlaintext = plaintext.ToByteArray(isUnsigned: true, isBigEndian: true);

            if (encodedPlaintext.Length > EncryptedBlockSize)
            {
                throw new InvalidDataException($"Server.dat RSA block {blockIndex} decoded beyond the verified block width.");
            }

            encodedPlaintext.CopyTo(decryptedBlock[(EncryptedBlockSize - encodedPlaintext.Length)..]);

            ReadOnlySpan<byte> payload = ExtractPkcs1Type1Payload(decryptedBlock, blockIndex);

            decodedPayload.Write(payload);
        }

        byte[] gzipPayload = decodedPayload.ToArray();

        if (gzipPayload.Length < 2 || gzipPayload[0] != GzipMagicByte0 || gzipPayload[1] != GzipMagicByte1)
        {
            throw new InvalidDataException("Server.dat decoded payload does not contain the verified gzip signature.");
        }

        return InflateGzip(gzipPayload);
    }

    private static void ValidatePublicModulus(ReadOnlySpan<byte> publicModulus)
    {
        if (publicModulus.Length != EncryptedBlockSize)
        {
            throw new ArgumentException($"Server.dat requires a {EncryptedBlockSize}-byte RSA public modulus.", nameof(publicModulus));
        }

        bool anyNonZero = false;

        foreach (byte value in publicModulus)
        {
            if (value == 0)
            {
                continue;
            }

            anyNonZero = true;
            break;
        }

        if (!anyNonZero)
        {
            throw new ArgumentException("Server.dat RSA public modulus cannot be zero.", nameof(publicModulus));
        }
    }

    private static ReadOnlySpan<byte> ExtractPkcs1Type1Payload(ReadOnlySpan<byte> block, int blockIndex)
    {
        if (block.Length != EncryptedBlockSize || block[0] != 0x00 || block[1] != 0x01)
        {
            throw new InvalidDataException($"Server.dat RSA block {blockIndex} does not contain PKCS#1 type-1 padding.");
        }

        int separatorIndex = 2;

        while (separatorIndex < block.Length && block[separatorIndex] == 0xFF)
        {
            separatorIndex++;
        }

        int paddingLength = separatorIndex - 2;

        if (paddingLength < MinimumType1PaddingLength)
        {
            throw new InvalidDataException($"Server.dat RSA block {blockIndex} contains insufficient PKCS#1 type-1 padding.");
        }

        if (separatorIndex >= block.Length || block[separatorIndex] != 0x00)
        {
            throw new InvalidDataException($"Server.dat RSA block {blockIndex} contains malformed PKCS#1 type-1 padding.");
        }

        int payloadOffset = separatorIndex + 1;

        if (payloadOffset >= block.Length)
        {
            throw new InvalidDataException($"Server.dat RSA block {blockIndex} contains an empty PKCS#1 payload.");
        }

        return block[payloadOffset..];
    }

    private static byte[] InflateGzip(ReadOnlySpan<byte> gzipPayload)
    {
        try
        {
            using MemoryStream compressedStream = new(gzipPayload.ToArray(), writable: false);

            using GZipStream gzipStream = new(compressedStream, CompressionMode.Decompress, leaveOpen: false);

            using MemoryStream inflatedStream = new();

            byte[] buffer = new byte[8192];

            while (true)
            {
                int bytesRead = gzipStream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    break;
                }

                if (inflatedStream.Length > MaximumInflatedXmlLength - bytesRead)
                {
                    throw new InvalidDataException($"Server.dat inflated XML exceeds the modern safety limit of {MaximumInflatedXmlLength} bytes.");
                }

                inflatedStream.Write(buffer, 0, bytesRead);
            }

            if (inflatedStream.Length == 0)
            {
                throw new InvalidDataException("Server.dat gzip payload inflates to an empty XML document.");
            }

            return inflatedStream.ToArray();
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException("Server.dat contains an invalid or unsupported gzip payload.", exception);
        }
        catch (IOException exception)
        {
            throw new InvalidDataException("Server.dat gzip payload could not be fully inflated.", exception);
        }
    }
}
