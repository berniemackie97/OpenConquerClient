using System.Buffers.Binary;
using System.Text;
using OpenConquer.Networking.Game.Handshake;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameServerChallengeDecoderTests
{
    [Fact]
    public void DecodesExactNativeCompatibleServerChallenge()
    {
        byte[] challenge = BuildChallenge();
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
        Assert.Equal((byte)1, parsed.ClientInboundIv.Span[0]);
        Assert.Equal((byte)21, parsed.ClientOutboundIv.Span[0]);
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
    [InlineData("")]
    public void RejectsInvalidDhPublicKeys(string publicKey)
    {
        byte[] challenge = BuildChallenge(publicKey: publicKey);
        Assert.Throws<InvalidDataException>(() =>
            GameServerChallengeDecoder.TryRead(challenge, out _));
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
        WriteChunk(stream, Encoding.ASCII.GetBytes(prime ?? GameServerChallengeDecoder.PrimeHex));
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
