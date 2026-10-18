using OpenConquer.Networking.Game.Handshake;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameHandshakeBootstrapCipherTests
{
    private const string ServerReferenceChallengeHex =
        "A0A1A2A3A4A5A6A7A8A9AAC40000000C0000005A5A5A5A5A5A5A5A5A5A5A5A0800000001020304050607080800000015161718191A1B1C8000000045374136394542444631303546324136424244454144374537393846373641323039414437334642343636343331453245373335324544323632463843353538463130424546454139373744453945323144434545394230344432343546333030454343424241303345373236333035353644303131303233463945383537460200000030350200000031395451536572766572";

    private const string ReferencePlaintext = "OpenConquer-5517-CFB64";
    private const string ReferenceCiphertextHex = "6A13741CE64645FCEC5A9D7A9630A3D18A9F3539C28C";

    [Fact]
    public void FixedNativeBootstrapCiphertextMatchesServerReference()
    {
        byte[] plaintext = "OpenConquer-5517-CFB64"u8.ToArray();
        byte[] ciphertext = new byte[plaintext.Length];
        using GameHandshakeBootstrapCipher cipher = GameHandshakeBootstrapCipher.CreateEncryptor();

        cipher.Transform(plaintext, ciphertext);

        Assert.Equal(ReferenceCiphertextHex, Convert.ToHexString(ciphertext));
    }

    [Fact]
    public void FixedNativeBootstrapCiphertextDecryptsToKnownPlaintext()
    {
        byte[] ciphertext = Convert.FromHexString(ReferenceCiphertextHex);
        byte[] plaintext = new byte[ciphertext.Length];
        using GameHandshakeBootstrapCipher cipher = GameHandshakeBootstrapCipher.CreateDecryptor();

        cipher.Transform(ciphertext, plaintext);

        Assert.Equal(ReferencePlaintext, System.Text.Encoding.ASCII.GetString(plaintext));
    }

    [Fact]
    public void StreamingTransformRetainsCfb64FeedbackAcrossArbitraryChunks()
    {
        byte[] plaintext = Enumerable.Range(0, 97).Select(static i => unchecked((byte)(i * 37 + 11))).ToArray();
        byte[] expected = new byte[plaintext.Length];
        byte[] chunked = new byte[plaintext.Length];

        using (GameHandshakeBootstrapCipher oneShot = GameHandshakeBootstrapCipher.CreateEncryptor())
        {
            oneShot.Transform(plaintext, expected);
        }

        using (GameHandshakeBootstrapCipher streaming = GameHandshakeBootstrapCipher.CreateEncryptor())
        {
            streaming.Transform(plaintext.AsSpan(0, 3), chunked.AsSpan(0, 3));
            streaming.Transform(plaintext.AsSpan(3, 17), chunked.AsSpan(3, 17));
            streaming.Transform(plaintext.AsSpan(20, 1), chunked.AsSpan(20, 1));
            streaming.Transform(plaintext.AsSpan(21, 51), chunked.AsSpan(21, 51));
            streaming.Transform(plaintext.AsSpan(72), chunked.AsSpan(72));
        }

        Assert.Equal(expected, chunked);
        byte[] restored = new byte[chunked.Length];
        using GameHandshakeBootstrapCipher decryptor = GameHandshakeBootstrapCipher.CreateDecryptor();
        decryptor.Transform(chunked.AsSpan(0, 15), restored.AsSpan(0, 15));
        decryptor.Transform(chunked.AsSpan(15), restored.AsSpan(15));
        Assert.Equal(plaintext, restored);
    }

    [Fact]
    public void BootstrapChallengeDecryptsWithContinuousFeedbackAndPassesEnvelopeValidation()
    {
        byte[] challenge = Convert.FromHexString(ServerReferenceChallengeHex);
        byte[] ciphertext = new byte[challenge.Length];
        using (GameHandshakeBootstrapCipher server = GameHandshakeBootstrapCipher.CreateEncryptor())
        {
            server.Transform(challenge, ciphertext);
        }

        byte[] plaintext = new byte[challenge.Length];
        using (GameHandshakeBootstrapCipher client = GameHandshakeBootstrapCipher.CreateDecryptor())
        {
            client.Transform(ciphertext.AsSpan(0, 15), plaintext.AsSpan(0, 15));
            Assert.True(GameServerChallengeDecoder.TryGetFrameLength(plaintext.AsSpan(0, 15), out int length));
            Assert.Equal(challenge.Length, length);
            client.Transform(ciphertext.AsSpan(15, 23), plaintext.AsSpan(15, 23));
            client.Transform(ciphertext.AsSpan(38), plaintext.AsSpan(38));
        }

        Assert.Equal(challenge, plaintext);
        Assert.True(GameServerChallengeDecoder.TryRead(plaintext, out GameServerChallenge? decoded));
        Assert.NotNull(decoded);
        Assert.Equal("19", decoded.ServerPublicKeyHex);
    }

    [Fact]
    public void InPlaceTransformAndFreshInstancesUseIndependentFeedback()
    {
        byte[] frame = "in-place-game-cipher"u8.ToArray();
        byte[] original = frame.ToArray();
        using (GameHandshakeBootstrapCipher cipher = GameHandshakeBootstrapCipher.CreateEncryptor())
        {
            cipher.Transform(frame, frame);
        }

        using (GameHandshakeBootstrapCipher cipher = GameHandshakeBootstrapCipher.CreateDecryptor())
        {
            cipher.Transform(frame, frame);
        }

        Assert.Equal(original, frame);

        byte[] first = new byte[original.Length];
        byte[] second = new byte[original.Length];
        using (GameHandshakeBootstrapCipher a = GameHandshakeBootstrapCipher.CreateEncryptor())
        using (GameHandshakeBootstrapCipher b = GameHandshakeBootstrapCipher.CreateEncryptor())
        {
            a.Transform(original, first);
            b.Transform(original, second);
        }

        Assert.Equal(first, second);
    }

    [Fact]
    public void InvalidOutputOrOverlapNeverAdvancesCipherFeedback()
    {
        byte[] input = Enumerable.Range(0, 16).Select(static i => (byte)i).ToArray();
        byte[] result = new byte[input.Length];
        byte[] expected = new byte[input.Length];

        using GameHandshakeBootstrapCipher actual = GameHandshakeBootstrapCipher.CreateEncryptor();
        using GameHandshakeBootstrapCipher reference = GameHandshakeBootstrapCipher.CreateEncryptor();

        Assert.Throws<ArgumentException>(() => actual.Transform(input, new byte[15]));
        Assert.Throws<ArgumentException>(() => actual.Transform(input.AsSpan(0, 8), input.AsSpan(1, 8)));
        actual.Transform(input, result);
        reference.Transform(input, expected);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DisposeIsIdempotentAndForbidsFurtherTransforms()
    {
        GameHandshakeBootstrapCipher cipher = GameHandshakeBootstrapCipher.CreateDecryptor();
        cipher.Dispose();
        cipher.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cipher.Transform(new byte[1], new byte[1]));
    }
}
