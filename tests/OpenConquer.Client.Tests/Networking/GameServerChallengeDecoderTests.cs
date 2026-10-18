using System.Buffers.Binary;
using System.Text;
using OpenConquer.Networking.Game.Handshake;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameServerChallengeDecoderTests
{
    // Independent reference: fixed plaintext bytes from OpenConquerServer's
    // GameHandshakeExchange.BuildChallenge contract, NOT a live network capture.
    // An independent literal prevents the decoder from defining its own fixture.
    private const string ServerReferenceChallengeHex =
        "A0A1A2A3A4A5A6A7A8A9AAC40000000C0000005A5A5A5A5A5A5A5A5A5A5A5A0800000001020304050607080800000015161718191A1B1C8000000045374136394542444631303546324136424244454144374537393846373641323039414437334642343636343331453245373335324544323632463843353538463130424546454139373744453945323144434545394230344432343546333030454343424241303345373236333035353644303131303233463945383537460200000030350200000031395451536572766572";

    private const string ServerReferencePrimeHex =
        "E7A69EBDF105F2A6BBDEAD7E798F76A209AD73FB466431E2E7352ED262F8C558F10BEFEA977DE9E21DCEE9B04D245F300ECCBBA03E72630556D011023F9E857F";

    [Fact]
    public void DecodesExactNativeCompatibleServerChallenge()
    {
        byte[] challenge = Convert.FromHexString(ServerReferenceChallengeHex);
        Assert.True(GameServerChallengeDecoder.TryGetFrameLength(challenge.AsSpan(0, 15), out int required));
        Assert.Equal(challenge.Length, required);

        Assert.True(GameServerChallengeDecoder.TryRead(challenge, out GameServerChallenge? parsed));
        Assert.NotNull(parsed);
        Assert.Equal(Enumerable.Range(0, 8).Select(static x => (byte)(x + 1)),
            parsed.ClientInboundIv.ToArray());
        Assert.Equal(Enumerable.Range(0, 8).Select(static x => (byte)(x + 21)),
            parsed.ClientOutboundIv.ToArray());
        Assert.Equal("19", parsed.ServerPublicKeyHex);

        // The decoded output cannot alias the short-lived decrypted frame.
        challenge.AsSpan().Clear();
        Assert.Equal((byte)1, parsed.ClientInboundIv[0]);
        Assert.Equal((byte)21, parsed.ClientOutboundIv[0]);
    }

    [Fact]
    public void FragmentedPrefixAndBodyDoNotYieldPartialParameters()
    {
        byte[] challenge = BuildChallenge();
        for (int length = 0; length < GameServerChallengeDecoder.HeaderLength; length++)
        {
            Assert.False(GameServerChallengeDecoder.TryGetFrameLength(challenge.AsSpan(0, length), out int required));
            Assert.Equal(0, required);
            Assert.False(GameServerChallengeDecoder.TryRead(challenge.AsSpan(0, length), out GameServerChallenge? decoded));
            Assert.Null(decoded);
        }

        for (int length = GameServerChallengeDecoder.HeaderLength; length < challenge.Length; length++)
        {
            Assert.False(GameServerChallengeDecoder.TryRead(challenge.AsSpan(0, length), out GameServerChallenge? decoded));
            Assert.Null(decoded);
        }
    }

    [Theory]
    [InlineData(11)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1025)]
    [InlineData(int.MaxValue)]
    public void RejectsImpossibleDeclaredLengthWithoutAllocating(int declaredLength)
    {
        byte[] challenge = BuildChallenge();
        BinaryPrimitives.WriteInt32LittleEndian(challenge.AsSpan(11), declaredLength);

        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryGetFrameLength(challenge, out _));
    }

    [Fact]
    public void RejectsTrailerAndOverread()
    {
        byte[] invalidTrailer = BuildChallenge();
        invalidTrailer[^1] ^= 0x7F;
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(invalidTrailer, out _));

        byte[] trailing = [.. BuildChallenge(), 0xFF];
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(trailing, out _));
    }

    [Fact]
    public void RejectsChunkLengthCorruptionAndInvalidIv()
    {
        byte[] invalidJunk = BuildChallenge();
        BinaryPrimitives.WriteInt32LittleEndian(invalidJunk.AsSpan(15), -1);
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(invalidJunk, out _));

        byte[] overlong = BuildChallenge();
        BinaryPrimitives.WriteInt32LittleEndian(overlong.AsSpan(15), int.MaxValue);
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(overlong, out _));

        byte[] ivTooShort = BuildChallenge(outboundIv: [1, 2, 3, 4, 5, 6, 7]);
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(ivTooShort, out _));
    }

    [Theory]
    [InlineData("00")]
    [InlineData("01")]
    [InlineData("GG")]
    [InlineData("g0")]
    public void RejectsInvalidDhPublicKeys(string publicKey)
    {
        byte[] challenge = BuildChallenge(publicKey: publicKey);
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(challenge, out _));
    }

    [Fact]
    public void EmptyPublicKeyChunkIsRejectedByChunkFraming()
    {
        byte[] challenge = BuildChallenge(publicKey: "");
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(challenge, out _));
    }

    [Fact]
    public void ChallengeParametersAreValidatedAndDefensivelyCopied()
    {
        byte[] inbound = [1, 2, 3, 4, 5, 6, 7, 8];
        byte[] outbound = [11, 12, 13, 14, 15, 16, 17, 18];
        GameServerChallenge challenge = GameServerChallenge.FromValidatedParameters(
            inbound, outbound, "19"u8);
        inbound[0] = 0;
        outbound[0] = 0;
        Assert.Equal((byte)1, challenge.ClientInboundIv[0]);
        Assert.Equal((byte)11, challenge.ClientOutboundIv[0]);

        Assert.Throws<InvalidDataException>(() =>
            GameServerChallenge.FromValidatedParameters(inbound.AsSpan(0, 7), outbound, "19"u8));
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallenge.FromValidatedParameters(inbound, outbound, "00"u8));
    }

    [Fact]
    public void RejectsUnsafeGroupOrPublicKeyOutsidePinnedGroup()
    {
        byte[] alteredGroup = BuildChallenge(prime: new string('F', 128));
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(alteredGroup, out _));

        byte[] alteredGenerator = BuildChallenge(generator: "03");
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(alteredGenerator, out _));

        byte[] publicKeyAtPrime = BuildChallenge(publicKey: GameServerChallengeDecoder.PrimeHex);
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(publicKeyAtPrime, out _));
    }

    [Fact]
    public void RejectsInconsistentChunkTailAndOversizedPublicKey()
    {
        byte[] extraChunk = BuildChallenge(extraChunk: true);
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(extraChunk, out _));

        byte[] oversizedKey = BuildChallenge(publicKey: new string('2', 129));
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(oversizedKey, out _));
    }

    private static byte[] BuildChallenge(string? prime = null, string generator = "05",
        string publicKey = "19", byte[]? outboundIv = null, bool extraChunk = false)
    {
        using MemoryStream stream = new();
        stream.Write(new byte[11]); // Native challenge has an opaque eleven-byte prefix.
        stream.Write(new byte[4]);  // Declared length, overwritten after assembling frame.
        WriteChunk(stream, Enumerable.Repeat((byte)0x5A, 12).ToArray());
        WriteChunk(stream, outboundIv ?? Enumerable.Range(1, 8).Select(static i => (byte)i).ToArray());
        WriteChunk(stream, Enumerable.Range(21, 8).Select(static i => (byte)i).ToArray());
        WriteChunk(stream, Encoding.ASCII.GetBytes(prime ?? ServerReferencePrimeHex));
        WriteChunk(stream, Encoding.ASCII.GetBytes(generator));
        WriteChunk(stream, Encoding.ASCII.GetBytes(publicKey));

        if (extraChunk)
        {
            WriteChunk(stream, "bad"u8);
        }

        stream.Write("TQServer"u8);
        byte[] challenge = stream.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(challenge.AsSpan(11), challenge.Length - 11);
        return challenge;
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        stream.Write(length);
        stream.Write(bytes);
    }
}
