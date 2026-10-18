using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using OpenConquer.Networking.Game.Handshake;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameClientDhExchangeTests
{
    // Independent, fixed native-compatible plaintext, and CAST5-CFB64 ciphertext
    // generated with a separately validated cryptographic provider.
    private const string ResponsePlaintextHex =
        "01020304050607200000000A000000AAAAAAAAAAAAAAAAAAAA0200000031395451436C69656E74";
    private const string ResponseCiphertextHex =
        "24611276A02F2CADD12908209DFF3458840EFBC31B1EEF3D0ADC104255979ABD4CC06689D78F35";

    private static GameServerChallenge Challenge() => GameServerChallenge.FromValidatedParameters(
        [1, 2, 3, 4, 5, 6, 7, 8], [21, 22, 23, 24, 25, 26, 27, 28], "19"u8);

    [Fact]
    public void DeterministicDhMatchesNativeResponseAndOpenSslSecretLayout()
    {
        using GameClientDhExchange exchange = GameClientDhExchange.CreateFromParameters(
            Challenge(), [2], [1, 2, 3, 4, 5, 6, 7], Enumerable.Repeat((byte)0xAA, 10).ToArray());

        Assert.Equal(39, exchange.EncryptedResponseLength);
        byte[] encrypted = new byte[exchange.EncryptedResponseLength];
        exchange.WriteEncryptedResponse(encrypted);
        Assert.Equal(ResponseCiphertextHex, Convert.ToHexString(encrypted));

        byte[] sessionKey = new byte[GameClientDhExchange.SessionKeyMaterialLength];
        byte[] inboundIv = new byte[8];
        byte[] outboundIv = new byte[8];
        exchange.TransferSessionParameters(sessionKey, inboundIv, outboundIv);

        Assert.Equal(0x02, sessionKey[0]);
        Assert.Equal(0x71, sessionKey[1]);
        Assert.All(sessionKey.AsSpan(2).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, inboundIv);
        Assert.Equal(new byte[] { 21, 22, 23, 24, 25, 26, 27, 28 }, outboundIv);

        Assert.Throws<InvalidOperationException>(() =>
            exchange.WriteEncryptedResponse(new byte[encrypted.Length]));
        Assert.Throws<InvalidOperationException>(() =>
            exchange.TransferSessionParameters(new byte[64], new byte[8], new byte[8]));

        CryptographicOperations.ZeroMemory(sessionKey);
        CryptographicOperations.ZeroMemory(encrypted);
    }

    [Fact]
    public void ResponseBuilderMatchesNativeSevenBytePrefixAndServerParserContract()
    {
        byte[] prefix = [1, 2, 3, 4, 5, 6, 7];
        byte[] padding = Enumerable.Repeat((byte)0xAA, 10).ToArray();
        byte[] frame = new byte[GameClientDhResponseEnvelope.GetFrameLength(padding.Length, "19"u8)];
        GameClientDhResponseEnvelope.Write(frame, prefix, padding, "19"u8);

        Assert.Equal(ResponsePlaintextHex, Convert.ToHexString(frame));
        Assert.Equal(32, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(7)));
        Assert.Equal(frame.Length - 7, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(7)));

        // Independent server response decoder consumes exactly two length-prefixed
        // entries and an eight-byte TQClient trailer.
        int offset = 11;
        int padLength = ReadLength(frame, ref offset);
        Assert.Equal(10, padLength);
        Assert.Equal(padding, frame.AsSpan(offset, padLength).ToArray());
        offset += padLength;
        int keyLength = ReadLength(frame, ref offset);
        Assert.Equal("19", Encoding.ASCII.GetString(frame.AsSpan(offset, keyLength)));
        offset += keyLength;
        Assert.Equal("TQClient"u8.ToArray(), frame.AsSpan(offset).ToArray());

        // All three supplied spans may share the output buffer.
        byte[] aliased = new byte[frame.Length];
        prefix.CopyTo(aliased, 0);
        padding.CopyTo(aliased, 15);
        "19"u8.CopyTo(aliased.AsSpan(29));
        GameClientDhResponseEnvelope.Write(aliased, aliased.AsSpan(0, 7),
            aliased.AsSpan(15, 10), aliased.AsSpan(29, 2));
        Assert.Equal(frame, aliased);
    }

    [Fact]
    public void IndependentCipherDecryptsCompleteDeterministicResponse()
    {
        byte[] encrypted = Convert.FromHexString(ResponseCiphertextHex);
        byte[] decrypted = new byte[encrypted.Length];
        using GameHandshakeBootstrapCipher cipher = GameHandshakeBootstrapCipher.CreateDecryptor();
        cipher.Transform(encrypted.AsSpan(0, 11), decrypted.AsSpan(0, 11));
        cipher.Transform(encrypted.AsSpan(11), decrypted.AsSpan(11));
        Assert.Equal(ResponsePlaintextHex, Convert.ToHexString(decrypted));
    }

    [Fact]
    public void ProductionEntropyCreatesValidServerCompatibleResponseAndKeyAgreement()
    {
        GameServerChallenge challenge = Challenge();
        using GameClientDhExchange exchange = GameClientDhExchange.Create(challenge);
        Assert.InRange(exchange.EncryptedResponseLength, 39, 204);

        byte[] ciphertext = new byte[exchange.EncryptedResponseLength];
        exchange.WriteEncryptedResponse(ciphertext);

        byte[] plaintext = new byte[ciphertext.Length];
        using (GameHandshakeBootstrapCipher decoder = GameHandshakeBootstrapCipher.CreateDecryptor())
        {
            decoder.Transform(ciphertext, plaintext);
        }

        int declared = BinaryPrimitives.ReadInt32LittleEndian(plaintext.AsSpan(7));
        Assert.Equal(plaintext.Length - 7, declared);
        int offset = 11;
        int randomBytes = ReadLength(plaintext, ref offset);
        Assert.InRange(randomBytes, 10, 49);
        offset += randomBytes;
        int hexLength = ReadLength(plaintext, ref offset);
        Assert.InRange(hexLength, 1, 128);
        byte[] hex = plaintext.AsSpan(offset, hexLength).ToArray();
        offset += hexLength;
        Assert.Equal("TQClient"u8.ToArray(), plaintext.AsSpan(offset).ToArray());
        Assert.True(GameServerChallengeDecoder.TryParsePublicKey(hex, out BigInteger clientPublic));

        byte[] key = new byte[64];
        byte[] inbound = new byte[8];
        byte[] outbound = new byte[8];
        exchange.TransferSessionParameters(key, inbound, outbound);

        // Fixture server public key 0x19 is 5^2: server exponent=2.
        // Both peers must therefore derive clientPublic^2 mod p.
        BigInteger prime = new(Convert.FromHexString(GameServerChallengeDecoder.PrimeHex),
            isUnsigned: true, isBigEndian: true);
        BigInteger shared = BigInteger.ModPow(clientPublic, new BigInteger(2), prime);
        byte[] minimal = shared.ToByteArray(isUnsigned: true, isBigEndian: true);
        Assert.Equal(minimal, key.AsSpan(0, minimal.Length).ToArray());
        Assert.All(key.AsSpan(minimal.Length).ToArray(), x => Assert.Equal((byte)0, x));
        Assert.Equal(challenge.ClientInboundIv.ToArray(), inbound);
        Assert.Equal(challenge.ClientOutboundIv.ToArray(), outbound);
        CryptographicOperations.ZeroMemory(minimal);
        CryptographicOperations.ZeroMemory(key);
    }

    [Theory]
    [InlineData("00")]
    [InlineData("01")]
    [InlineData("E7A69EBDF105F2A6BBDEAD7E798F76A209AD73FB466431E2E7352ED262F8C558F10BEFEA977DE9E21DCEE9B04D245F300ECCBBA03E72630556D011023F9E857E")]
    public void RejectsInvalidDeterministicPrivateExponent(string exponentHex)
    {
        byte[] exponent = Convert.FromHexString(exponentHex);
        Assert.Throws<ArgumentException>(() =>
            GameClientDhExchange.CreateFromParameters(Challenge(), exponent, new byte[7], new byte[10]));
    }

    [Fact]
    public void RejectsInvalidResponseLengthsKeysAndPreservesDestination()
    {
        byte[] destination = Enumerable.Repeat((byte)0xA5, 39).ToArray();
        Assert.Throws<ArgumentException>(() =>
            GameClientDhResponseEnvelope.Write(destination, new byte[6], new byte[10], "19"u8));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameClientDhResponseEnvelope.Write(destination, new byte[7], new byte[9], "19"u8));
        Assert.Throws<ArgumentException>(() =>
            GameClientDhResponseEnvelope.Write(destination, new byte[7], new byte[10], "GG"u8));
        Assert.Throws<ArgumentException>(() =>
            GameClientDhResponseEnvelope.Write(destination, new byte[7], new byte[10], "1"u8));
        Assert.All(destination, x => Assert.Equal((byte)0xA5, x));
    }

    [Fact]
    public void FailureDoesNotConsumeResponseIssuanceOrSessionTransfer()
    {
        using GameClientDhExchange exchange = GameClientDhExchange.CreateFromParameters(
            Challenge(), [2], new byte[7], new byte[10]);

        Assert.Throws<InvalidOperationException>(() =>
            exchange.TransferSessionParameters(new byte[64], new byte[8], new byte[8]));
        Assert.Throws<ArgumentException>(() => exchange.WriteEncryptedResponse(new byte[1]));
        byte[] encoded = new byte[exchange.EncryptedResponseLength];
        exchange.WriteEncryptedResponse(encoded);

        byte[] destination = Enumerable.Repeat((byte)0xA5, 80).ToArray();
        Assert.Throws<ArgumentException>(() =>
            exchange.TransferSessionParameters(destination.AsSpan(0, 64),
                destination.AsSpan(60, 8), destination.AsSpan(72, 8)));
        Assert.All(destination, x => Assert.Equal((byte)0xA5, x));

        byte[] key = new byte[64];
        byte[] inbound = new byte[8];
        byte[] outbound = new byte[8];
        exchange.TransferSessionParameters(key, inbound, outbound);
        Assert.Equal((byte)0x02, key[0]);
        CryptographicOperations.ZeroMemory(key);
    }

    [Fact]
    public void DisposeIsIdempotentAndTerminatesAllOutputs()
    {
        GameClientDhExchange exchange = GameClientDhExchange.CreateFromParameters(
            Challenge(), [2], new byte[7], new byte[10]);
        exchange.Dispose();
        exchange.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = exchange.EncryptedResponseLength);
        Assert.Throws<ObjectDisposedException>(() => exchange.WriteEncryptedResponse(new byte[39]));
        Assert.Throws<ObjectDisposedException>(() =>
            exchange.TransferSessionParameters(new byte[64], new byte[8], new byte[8]));
    }

    private static int ReadLength(ReadOnlySpan<byte> frame, ref int offset)
    {
        int length = BinaryPrimitives.ReadInt32LittleEndian(frame[offset..]);
        offset += 4;
        Assert.True(length > 0);
        Assert.True(offset + length <= frame.Length - 8);
        return length;
    }
}
