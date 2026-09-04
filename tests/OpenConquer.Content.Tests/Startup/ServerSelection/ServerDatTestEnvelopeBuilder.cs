using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using OpenConquer.Content.Startup.ServerSelection;

namespace OpenConquer.Content.Tests.Startup.ServerSelection;

/// <summary>
/// Generates test-only RSA material and Server.dat-compatible PKCS#1 type-1 blocks.
/// </summary>
/// <remarks>
/// The private exponent exists only in the test assembly. Tests generate their own 2048-bit keypair
/// and use the private operation solely to construct ciphertext that the production public decoder
/// can verify.
/// </remarks>
internal static class ServerDatTestEnvelopeBuilder
{
    private const int MinimumType1PaddingLength = 8;

    private static readonly Lazy<RSAParameters> s_key = new(CreateKey);

    public static byte[] PublicModulus => [.. GetKey().Modulus!];

    public static byte[] EncodeXml(ReadOnlySpan<byte> xmlPayload)
    {
        return EncodeGzipPayload(Compress(xmlPayload));
    }

    public static byte[] EncodeGzipPayload(ReadOnlySpan<byte> gzipPayload)
    {
        if (gzipPayload.IsEmpty)
        {
            throw new ArgumentException("Gzip test payload cannot be empty.", nameof(gzipPayload));
        }

        int maximumPayloadLength = ServerDatEnvelopeDecoder.EncryptedBlockSize - 3 - MinimumType1PaddingLength;

        int blockCount = checked((gzipPayload.Length + maximumPayloadLength - 1) / maximumPayloadLength);

        byte[] encrypted = new byte[checked(blockCount * ServerDatEnvelopeDecoder.EncryptedBlockSize)];

        byte[] plaintextBlock = new byte[ServerDatEnvelopeDecoder.EncryptedBlockSize];

        int sourceOffset = 0;

        for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            int payloadLength = Math.Min(maximumPayloadLength, gzipPayload.Length - sourceOffset);

            Span<byte> plaintext = plaintextBlock;

            plaintext.Clear();
            plaintext[0] = 0x00;
            plaintext[1] = 0x01;

            int paddingLength = ServerDatEnvelopeDecoder.EncryptedBlockSize - payloadLength - 3;

            plaintext[2..(2 + paddingLength)].Fill(0xFF);

            plaintext[2 + paddingLength] = 0x00;

            gzipPayload.Slice(sourceOffset, payloadLength).CopyTo(plaintext[(3 + paddingLength)..]);

            EncryptRawBlock(plaintext, encrypted.AsSpan(blockIndex * ServerDatEnvelopeDecoder.EncryptedBlockSize, ServerDatEnvelopeDecoder.EncryptedBlockSize));

            sourceOffset += payloadLength;
        }

        return encrypted;
    }

    public static byte[] EncryptRawBlock(ReadOnlySpan<byte> plaintextBlock)
    {
        byte[] encrypted = new byte[ServerDatEnvelopeDecoder.EncryptedBlockSize];

        EncryptRawBlock(plaintextBlock, encrypted);

        return encrypted;
    }

    public static byte[] Compress(ReadOnlySpan<byte> payload)
    {
        using MemoryStream destination = new();

        using (GZipStream gzip = new(destination, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(payload);
        }

        return destination.ToArray();
    }

    private static void EncryptRawBlock(ReadOnlySpan<byte> plaintextBlock, Span<byte> destination)
    {
        if (plaintextBlock.Length != ServerDatEnvelopeDecoder.EncryptedBlockSize)
        {
            throw new ArgumentException($"Test RSA plaintext must be exactly {ServerDatEnvelopeDecoder.EncryptedBlockSize} bytes.", nameof(plaintextBlock));
        }

        if (destination.Length != ServerDatEnvelopeDecoder.EncryptedBlockSize)
        {
            throw new ArgumentException($"Test RSA destination must be exactly {ServerDatEnvelopeDecoder.EncryptedBlockSize} bytes.", nameof(destination));
        }

        RSAParameters key = GetKey();

        BigInteger modulus = new(key.Modulus!, isUnsigned: true, isBigEndian: true);
        BigInteger privateExponent = new(key.D!, isUnsigned: true, isBigEndian: true);
        BigInteger plaintext = new(plaintextBlock, isUnsigned: true, isBigEndian: true);

        if (plaintext >= modulus)
        {
            throw new InvalidOperationException("Generated test plaintext is outside the RSA modulus range.");
        }

        BigInteger ciphertext = BigInteger.ModPow(plaintext, privateExponent, modulus);

        byte[] encodedCiphertext = ciphertext.ToByteArray(isUnsigned: true, isBigEndian: true);

        if (encodedCiphertext.Length > ServerDatEnvelopeDecoder.EncryptedBlockSize)
        {
            throw new InvalidOperationException("Generated test ciphertext exceeds the RSA modulus width.");
        }

        destination.Clear();

        encodedCiphertext.CopyTo(destination[(ServerDatEnvelopeDecoder.EncryptedBlockSize - encodedCiphertext.Length)..]);
    }

    private static RSAParameters GetKey()
    {
        return s_key.Value;
    }

    private static RSAParameters CreateKey()
    {
        using RSA rsa = RSA.Create(ServerDatEnvelopeDecoder.EncryptedBlockSize * 8);

        RSAParameters parameters = rsa.ExportParameters(includePrivateParameters: true);

        if (parameters.Modulus?.Length != ServerDatEnvelopeDecoder.EncryptedBlockSize)
        {
            throw new InvalidOperationException("Generated Server.dat test RSA key does not have the required modulus width.");
        }

        byte[] expectedExponent = [0x01, 0x00, 0x01];

        if (parameters.Exponent is null || !parameters.Exponent.AsSpan().SequenceEqual(expectedExponent))
        {
            throw new InvalidOperationException("Generated Server.dat test RSA key does not use exponent 65537.");
        }

        if (parameters.D is null)
        {
            throw new InvalidOperationException("Generated Server.dat test RSA key does not expose its private exponent.");
        }

        return parameters;
    }
}
