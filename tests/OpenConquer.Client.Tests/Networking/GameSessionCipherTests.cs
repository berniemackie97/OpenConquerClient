using System.Security.Cryptography;
using OpenConquer.Networking.Game.Cryptography;
using OpenConquer.Networking.Game.Handshake;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameSessionCipherTests
{
    // Independent reference: Python cryptography CAST5/CFB64 using the
    // deterministic DH test secret 02 71 followed by 62 zero bytes.
    private const string ClientOutboundReference = "E96A3ACC730CDF643BDC70270D240D8D";
    private const string ClientInboundReference = "3008641B48568808A401EBB910ABE8A4";
    private const string FragmentedOutboundReference =
        "813606D382BC1B1E123A91D9AB0696E41A0A8E3262432FEDE56129DAE00086CAF06B34B51FA0779CC2FB86A9DDE65076201AC800CE27BAD227187AD3A6A90B579D9042CAD24870EDC2AC07CE3185826DAF45BFFE7B9EE0093F35D1EF8C7641FF7A";

    private static byte[] SessionKey()
    {
        byte[] material = new byte[64];
        material[0] = 0x02;
        material[1] = 0x71;
        return material;
    }

    private static byte[] ClientInboundIv() => [1, 2, 3, 4, 5, 6, 7, 8];
    private static byte[] ClientOutboundIv() => [21, 22, 23, 24, 25, 26, 27, 28];

    [Fact]
    public void BothDirectionsMatchIndependentSessionVectors()
    {
        byte[] key = SessionKey();
        using GameSessionCipher session = new(key, ClientInboundIv(), ClientOutboundIv());
        byte[] clientToServer = "client-to-server"u8.ToArray();
        byte[] serverToClient = Convert.FromHexString(ClientInboundReference);
        byte[] encrypted = new byte[clientToServer.Length];
        byte[] decrypted = new byte[serverToClient.Length];

        session.EncryptOutbound(clientToServer, encrypted);
        session.DecryptInbound(serverToClient, decrypted);

        Assert.Equal(ClientOutboundReference, Convert.ToHexString(encrypted));
        Assert.Equal("server-to-client"u8.ToArray(), decrypted);
        CryptographicOperations.ZeroMemory(key);
    }

    [Fact]
    public void OutboundFragmentsMatchIndependentNinetySevenByteVector()
    {
        byte[] plaintext = Enumerable.Range(0, 97)
            .Select(static i => unchecked((byte)((i * 37) + 11))).ToArray();
        byte[] expected = Convert.FromHexString(FragmentedOutboundReference);
        byte[] encrypted = new byte[plaintext.Length];

        using GameSessionCipher session = new(SessionKey(), ClientInboundIv(), ClientOutboundIv());
        session.EncryptOutbound(plaintext.AsSpan(0, 3), encrypted.AsSpan(0, 3));
        session.EncryptOutbound(plaintext.AsSpan(3, 17), encrypted.AsSpan(3, 17));
        session.EncryptOutbound(plaintext.AsSpan(20, 1), encrypted.AsSpan(20, 1));
        session.EncryptOutbound(plaintext.AsSpan(21, 51), encrypted.AsSpan(21, 51));
        session.EncryptOutbound(plaintext.AsSpan(72), encrypted.AsSpan(72));

        Assert.Equal(expected, encrypted);
    }

    [Fact]
    public void InputAndOutputCanOverlapExactlyButNotPartially()
    {
        byte[] data = "client-to-server"u8.ToArray();
        byte[] original = data.ToArray();
        using (GameSessionCipher writer = new(SessionKey(), ClientInboundIv(), ClientOutboundIv()))
        {
            writer.EncryptOutbound(data, data);
        }

        Assert.Equal(ClientOutboundReference, Convert.ToHexString(data));

        using (GameSessionCipher reader = new(SessionKey(), ClientOutboundIv(), ClientInboundIv()))
        {
            reader.DecryptInbound(data, data);
        }

        Assert.Equal(original, data);
    }

    [Fact]
    public void InvalidOutputAndOverlapPreserveDirectionalStateAndCallerBytes()
    {
        byte[] source = "client-to-server"u8.ToArray();
        byte[] actual = new byte[source.Length];
        byte[] untouched = Enumerable.Repeat((byte)0xA5, source.Length).ToArray();

        using GameSessionCipher session = new(SessionKey(), ClientInboundIv(), ClientOutboundIv());
        Assert.Throws<ArgumentException>(() => session.EncryptOutbound(source, new byte[source.Length - 1]));
        Assert.Throws<ArgumentException>(() =>
            session.EncryptOutbound(untouched.AsSpan(0, 8), untouched.AsSpan(1, 8)));
        Assert.All(untouched, value => Assert.Equal((byte)0xA5, value));

        session.EncryptOutbound(source, actual);
        Assert.Equal(ClientOutboundReference, Convert.ToHexString(actual));

        byte[] inboundCipher = Convert.FromHexString(ClientInboundReference);
        Assert.Throws<ArgumentException>(() => session.DecryptInbound(inboundCipher, new byte[1]));
        byte[] clear = new byte[inboundCipher.Length];
        session.DecryptInbound(inboundCipher, clear);
        Assert.Equal("server-to-client"u8.ToArray(), clear);
    }

    [Fact]
    public void DifferentSessionKeyTailsDoNotChangeNativeCast5FirstSixteenBytes()
    {
        byte[] first = SessionKey();
        byte[] second = SessionKey();
        second.AsSpan(16).Fill(0x7F);
        byte[] plain = "client-to-server"u8.ToArray();
        byte[] a = new byte[plain.Length];
        byte[] b = new byte[plain.Length];

        using (GameSessionCipher left = new(first, ClientInboundIv(), ClientOutboundIv()))
        using (GameSessionCipher right = new(second, ClientInboundIv(), ClientOutboundIv()))
        {
            left.EncryptOutbound(plain, a);
            right.EncryptOutbound(plain, b);
        }

        Assert.Equal(a, b);
        Assert.Equal(ClientOutboundReference, Convert.ToHexString(a));
        CryptographicOperations.ZeroMemory(first);
        CryptographicOperations.ZeroMemory(second);
    }

    [Fact]
    public void ConstructorRejectsInvalidSessionMaterialBeforeUse()
    {
        Assert.Throws<ArgumentException>(() => new GameSessionCipher(new byte[63], ClientInboundIv(), ClientOutboundIv()));
        Assert.Throws<ArgumentException>(() => new GameSessionCipher(new byte[65], ClientInboundIv(), ClientOutboundIv()));
        Assert.Throws<ArgumentException>(() => new GameSessionCipher(SessionKey(), new byte[7], ClientOutboundIv()));
        Assert.Throws<ArgumentException>(() => new GameSessionCipher(SessionKey(), ClientInboundIv(), new byte[9]));
    }

    [Fact]
    public void ExchangeTransfersNativeSecretOnlyOnceAfterResponseIssuance()
    {
        GameServerChallenge challenge = GameServerChallenge.FromValidatedParameters(
            ClientInboundIv(), ClientOutboundIv(), "19"u8);

        using GameClientDhExchange exchange = GameClientDhExchange.CreateFromParameters(
            challenge, [2], new byte[7], new byte[10]);

        Assert.Throws<InvalidOperationException>(() => GameSessionCipher.CreateFromExchange(exchange));

        byte[] response = new byte[exchange.EncryptedResponseLength];
        exchange.WriteEncryptedResponse(response);

        using GameSessionCipher session = GameSessionCipher.CreateFromExchange(exchange);
        Assert.Throws<InvalidOperationException>(() => GameSessionCipher.CreateFromExchange(exchange));

        byte[] encrypted = new byte[16];
        session.EncryptOutbound("client-to-server"u8, encrypted);
        Assert.Equal(ClientOutboundReference, Convert.ToHexString(encrypted));
        CryptographicOperations.ZeroMemory(response);
    }

    [Fact]
    public void DisposeIsIdempotentAndTerminatesBothDirections()
    {
        GameSessionCipher session = new(SessionKey(), ClientInboundIv(), ClientOutboundIv());
        session.Dispose();
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => session.DecryptInbound(new byte[1], new byte[1]));
        Assert.Throws<ObjectDisposedException>(() => session.EncryptOutbound(new byte[1], new byte[1]));
    }

    [Fact]
    public void ExistingFixedBootstrapVectorStillMatchesAfterSharedPrimitiveRefactor()
    {
        byte[] plaintext = "OpenConquer-5517-CFB64"u8.ToArray();
        byte[] cipher = new byte[plaintext.Length];
        using (GameHandshakeBootstrapCipher encryptor = GameHandshakeBootstrapCipher.CreateEncryptor())
        {
            encryptor.Transform(plaintext, cipher);
        }

        Assert.Equal("6A13741CE64645FCEC5A9D7A9630A3D18A9F3539C28C",
            Convert.ToHexString(cipher));
    }
}
