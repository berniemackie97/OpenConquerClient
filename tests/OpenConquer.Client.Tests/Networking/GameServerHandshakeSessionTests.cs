using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using OpenConquer.Networking.Authentication;
using OpenConquer.Networking.Game.Cryptography;
using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameServerHandshakeSessionTests
{
    private const string SeedWire = "C54869128E317C0F";
    private const string SuccessWire =
        "BFF0C21A058F75E70B44B1BAB2056F3EB900E32A626FF42466CAF8503A345F0E9D5053BA";

    // Independent full CAST5-CFB64 challenge reference (AUTH-001H). This is
    // source-derived, not a capture of a remote server or proof of peer identity.
    private const string EncryptedChallenge =
        "85C2B3D1018C8D2AA9320638D733861E1019DDDBB8BB9F1DAD33F5408456C31DF4D709DDC12C014F953FE330AAE708790E97B357C832F8F9E97D94CDE1D53F0FB6EE5C3128EC848ECBB876F061AF86A28BE538C23EC01F0BDB9B6D6D52793BE8288033574A74C0D9AEACEF01708516E5583C0254762063CAFA8153DB60E6DCA9AC1632816338CA1D77F394901CB6C82198CB025DEAA1567F12A08B8B52CF58570C833AD58C9DC94B65178873DDC4CB0E7361E640D029A3D55048E6DFDA9AF13EDB9EE35ECE04B9B6022BDD6D791FB5";

    private static readonly GameServerHandshakeTimeouts s_timeouts =
        new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

    [Fact]
    public async Task CompletesFragmentedLoopbackHandshakeAndTransfersNativeSession()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener gameListener = Listen();
        using TcpListener accountListener = Listen();
        Task accountPeer = AccountPeerAsync(accountListener, token);
        TaskCompletionSource<byte[]> peerEncrypted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task gamePeer = CompletedGamePeerAsync(gameListener, peerEncrypted, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accountListener, Port(gameListener), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);

        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);

        byte[] encrypted = await peerEncrypted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        byte[] plaintext = new byte[encrypted.Length];
        game.SessionCipher.DecryptInbound(encrypted.AsSpan(0, 5), plaintext.AsSpan(0, 5));
        game.SessionCipher.DecryptInbound(encrypted.AsSpan(5), plaintext.AsSpan(5));
        Assert.Equal("native-server-session"u8.ToArray(), plaintext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GameServerHandshakeSession.OpenAsync(account, s_timeouts, token));

        await game.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => _ = game.SessionCipher);
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task UntrustedRedirectFailsBeforeConnectingOrClaimingSessionTwice()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accountListener = Listen();
        Task peer = AccountPeerAsync(accountListener, token);
        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accountListener, 5816, token, host: "192.0.2.1");
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GameServerHandshakeSession.OpenAsync(account, s_timeouts, token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GameServerHandshakeSession.OpenAsync(account, s_timeouts, token));
    }

    [Fact]
    public async Task FailedAccountExchangeCannotStartGameHandshake()
    {
        await using LoginAccountExchangeResult failed = LoginAccountExchangeResult.Failure(
            new LoginAccountResponse1055(0, 20, 0, 0, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GameServerHandshakeSession.OpenAsync(failed, s_timeouts,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MalformedEncryptedChallengeFailsClosed()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accountListener = Listen();
        using TcpListener gameListener = Listen();
        Task accountPeer = AccountPeerAsync(accountListener, token);
        byte[] challenge = Convert.FromHexString(EncryptedChallenge);
        // Corrupt a full challenge. Structural parsing must terminate this socket.
        challenge[^1] ^= 0x01;
        Task gamePeer = ChallengeOnlyPeerAsync(gameListener, challenge, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accountListener, Port(gameListener), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            GameServerHandshakeSession.OpenAsync(account, s_timeouts, token));
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task ChallengeTimeoutAbortsSocketAndConsumesHandoff()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accountListener = Listen();
        using TcpListener gameListener = Listen();
        Task accountPeer = AccountPeerAsync(accountListener, token);
        Task gamePeer = SilentGamePeerAsync(gameListener, token);
        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accountListener, Port(gameListener), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            GameServerHandshakeSession.OpenAsync(account,
                new GameServerHandshakeTimeouts(TimeSpan.FromSeconds(5),
                    TimeSpan.FromMilliseconds(150)), token));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GameServerHandshakeSession.OpenAsync(account, s_timeouts, token));
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task DisposingAccountResultCancelsActiveHandshake()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accountListener = Listen();
        using TcpListener gameListener = Listen();
        Task accountPeer = AccountPeerAsync(accountListener, token);
        TaskCompletionSource<bool> accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task gamePeer = SilentGamePeerAsync(gameListener, token, accepted);
        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accountListener, Port(gameListener), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);

        Task<GameServerHandshakeSession> pending =
            GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        await account.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task InvalidTimeoutAndPreCancellationDoNotConsumeHandoff()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accountListener = Listen();
        using TcpListener gameListener = Listen();
        Task accountPeer = AccountPeerAsync(accountListener, token);
        TaskCompletionSource<byte[]> encrypted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task gamePeer = CompletedGamePeerAsync(gameListener, encrypted, token);
        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accountListener, Port(gameListener), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            GameServerHandshakeSession.OpenAsync(account,
                new GameServerHandshakeTimeouts(TimeSpan.Zero, TimeSpan.FromSeconds(1)), token));

        using CancellationTokenSource canceled = CancellationTokenSource.CreateLinkedTokenSource(token);
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            GameServerHandshakeSession.OpenAsync(account, s_timeouts, canceled.Token));

        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await encrypted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task SecuredProofAndConsecutiveInboundFramesPreserveBothCipherStreams()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = SecuredGamePeerAsync(games, PeerResponse.TwoFrames, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));

        await game.SendLoginProofAsync(0x6E45, new byte[] { 0, 0x11, 0x22, 0xAA, 0xBB, 0xCC },
            TimeSpan.FromSeconds(5), token);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.SendLoginProofAsync(0x6E45, ReadOnlyMemory<byte>.Empty,
                TimeSpan.FromSeconds(5), token));

        using GameSecuredInboundFrame first = Assert.IsType<GameSecuredInboundFrame>(
            await game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        Assert.Equal(1052, first.PacketType);
        Assert.Equal("08001C0455667788", Convert.ToHexString(first.Packet.Span));
        using GameSecuredInboundFrame second = Assert.IsType<GameSecuredInboundFrame>(
            await game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        Assert.Equal(1009, second.PacketType);
        Assert.Equal("0400F103", Convert.ToHexString(second.Packet.Span));

        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task InvalidSecuredSignatureClosesSocketAndTerminatesReads()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = SecuredGamePeerAsync(games, PeerResponse.InvalidSignature, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await game.SendLoginProofAsync(0x6E45, new byte[] { 0, 0x11, 0x22, 0xAA, 0xBB, 0xCC },
            TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task PartialSecuredFrameEofClosesSocketWithoutReturningPacket()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = SecuredGamePeerAsync(games, PeerResponse.PartialFrameEof, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await game.SendLoginProofAsync(0x6E45, new byte[] { 0, 0x11, 0x22, 0xAA, 0xBB, 0xCC },
            TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task SecuredFrameTimeoutTerminatesSessionAndAbortsSocket()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = SecuredGamePeerAsync(games, PeerResponse.SilentAfterProof, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await game.SendLoginProofAsync(0x6E45, new byte[] { 0, 0x11, 0x22, 0xAA, 0xBB, 0xCC },
            TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            game.ReadFrameAsync(TimeSpan.FromMilliseconds(150), token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task AccountDisposalBeforeProofIsTerminalAndNoProofIsSent()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        TaskCompletionSource<byte[]> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task gamePeer = CompletedGamePeerAsync(games, completed, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        await account.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            game.SendLoginProofAsync(0x6E45, ReadOnlyMemory<byte>.Empty,
                TimeSpan.FromSeconds(5), token));
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task FirstLoginControlStartsExistingCharacterBootstrapWithoutSkippingNextFrame()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = SecuredGamePeerAsync(games, PeerResponse.AcceptedThenHistory, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await game.SendLoginProofAsync(0x6E45, new byte[] { 0, 0x11, 0x22, 0xAA, 0xBB, 0xCC },
            TimeSpan.FromSeconds(5), token);

        GameLoginControlOutcome result = await game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(GameLoginControlKind.ExistingCharacter, result.Kind);
        Assert.Null(result.RefusalText);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token));

        using GameSecuredInboundFrame history = Assert.IsType<GameSecuredInboundFrame>(
            await game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        Assert.Equal(2078, history.PacketType);
        Assert.Equal("0A001E08000000000000", Convert.ToHexString(history.Packet.Span));

        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Theory]
    [InlineData(PeerResponse.NewRole, GameLoginControlKind.CharacterCreation, null)]
    [InlineData(PeerResponse.LoginRefused, GameLoginControlKind.Refused,
        "The server is full. Please try again later.")]
    public async Task FirstLoginControlRoutesCreationOrTerminatesRefusal(
        PeerResponse reply, GameLoginControlKind expected, string? refusal)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = SecuredGamePeerAsync(games, reply, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await game.SendLoginProofAsync(0x6E45, new byte[] { 0, 0x11, 0x22, 0xAA, 0xBB, 0xCC },
            TimeSpan.FromSeconds(5), token);

        GameLoginControlOutcome result = await game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(expected, result.Kind);
        Assert.Equal(refusal, result.RefusalText);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token));
        if (expected == GameLoginControlKind.Refused)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        }

        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Theory]
    [InlineData(PeerResponse.WrongLoginChannel)]
    [InlineData(PeerResponse.WrongFirstPacket)]
    public async Task UnexpectedInitialLoginControlIsTerminal(PeerResponse reply)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = SecuredGamePeerAsync(games, reply, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await game.SendLoginProofAsync(0x6E45, new byte[] { 0, 0x11, 0x22, 0xAA, 0xBB, 0xCC },
            TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task RawFrameReadCannotBeRelabeledAsTheFirstLoginControl()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = SecuredGamePeerAsync(games, PeerResponse.AcceptedThenHistory, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await game.SendLoginProofAsync(0x6E45, new byte[] { 0, 0x11, 0x22, 0xAA, 0xBB, 0xCC },
            TimeSpan.FromSeconds(5), token);

        using GameSecuredInboundFrame first = Assert.IsType<GameSecuredInboundFrame>(
            await game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        Assert.Equal(1004, first.PacketType);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token));

        using GameSecuredInboundFrame next = Assert.IsType<GameSecuredInboundFrame>(
            await game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        Assert.Equal(2078, next.PacketType);

        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Theory]
    [InlineData(PeerResponse.NoLoginResponse)]
    [InlineData(PeerResponse.SilentAfterProof)]
    public async Task MissingInitialLoginControlNeverReturnsSuccess(PeerResponse reply)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = SecuredGamePeerAsync(games, reply, token);

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game =
            await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await game.SendLoginProofAsync(0x6E45, new byte[] { 0, 0x11, 0x22, 0xAA, 0xBB, 0xCC },
            TimeSpan.FromSeconds(5), token);

        if (reply == PeerResponse.NoLoginResponse)
        {
            await Assert.ThrowsAsync<EndOfStreamException>(() =>
                game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token));
        }
        else
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                game.ReadLoginControlAsync(TimeSpan.FromMilliseconds(150), token));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    private enum PeerResponse
    {
        TwoFrames,
        InvalidSignature,
        PartialFrameEof,
        SilentAfterProof,
        AcceptedThenHistory,
        NewRole,
        LoginRefused,
        WrongLoginChannel,
        WrongFirstPacket,
        NoLoginResponse,
    }

    private static async Task SecuredGamePeerAsync(TcpListener listener, PeerResponse response,
        CancellationToken token)
    {
        using TcpClient peer = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = peer.GetStream();
        await stream.WriteAsync(Convert.FromHexString(EncryptedChallenge), token);

        // Server-side interpretation of the client's unframed DH answer:
        // seven random prefix bytes, including length, two chunks, trailer.
        byte[] header = new byte[11];
        await stream.ReadExactlyAsync(header, token);
        using GameHandshakeBootstrapCipher decryptor = GameHandshakeBootstrapCipher.CreateDecryptor();
        decryptor.Transform(header, header);
        int declared = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(7));
        Assert.InRange(declared, 31, 1017);
        byte[] body = new byte[declared - sizeof(int)];
        await stream.ReadExactlyAsync(body, token);
        decryptor.Transform(body, body);
        int offset = 0;
        int paddingLength = BinaryPrimitives.ReadInt32LittleEndian(body);
        Assert.InRange(paddingLength, 10, 49);
        offset += sizeof(int) + paddingLength;
        int publicLength = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(offset));
        offset += sizeof(int);
        Assert.InRange(publicLength, 1, 128);
        Assert.True(GameServerChallengeDecoder.TryParsePublicKey(
            body.AsSpan(offset, publicLength), out BigInteger publicKey));
        offset += publicLength;
        Assert.Equal("TQClient"u8.ToArray(), body.AsSpan(offset).ToArray());

        BigInteger prime = new(Convert.FromHexString(GameServerChallengeDecoder.PrimeHex),
            isUnsigned: true, isBigEndian: true);
        BigInteger shared = BigInteger.ModPow(publicKey, new BigInteger(2), prime);
        byte[] minimal = shared.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] key = new byte[64];
        minimal.CopyTo(key, 0);
        try
        {
            using GameSessionCipher server = new(key,
                [21, 22, 23, 24, 25, 26, 27, 28], [1, 2, 3, 4, 5, 6, 7, 8]);

            byte[] proofWire = new byte[36];
            await stream.ReadExactlyAsync(proofWire, token);
            server.DecryptInbound(proofWire, proofWire);
            Assert.Equal(
                "1C001C0444332211EFCDAB897C00456E001122AABBCC00008D150000" +
                "5451436C69656E74", Convert.ToHexString(proofWire));

            switch (response)
            {
                case PeerResponse.TwoFrames:
                    byte[] first = Convert.FromHexString("08001C04556677885451536572766572");
                    byte[] second = Convert.FromHexString("0400F1035451536572766572");
                    server.EncryptOutbound(first, first);
                    server.EncryptOutbound(second, second);
                    byte[] both = new byte[first.Length + second.Length];
                    first.CopyTo(both, 0);
                    second.CopyTo(both, first.Length);
                    await stream.WriteAsync(both, token);
                    Assert.Equal(0, await ObserveCloseAsync(stream, token));
                    return;

                case PeerResponse.InvalidSignature:
                    byte[] invalid = Convert.FromHexString("08001C04556677885451436C69656E74");
                    server.EncryptOutbound(invalid, invalid);
                    await stream.WriteAsync(invalid, token);
                    Assert.Equal(0, await ObserveCloseAsync(stream, token));
                    return;

                case PeerResponse.PartialFrameEof:
                    byte[] partial = Convert.FromHexString("08001C04556677885451536572766572");
                    server.EncryptOutbound(partial, partial);
                    await stream.WriteAsync(partial.AsMemory(0, 5), token);
                    return;

                case PeerResponse.AcceptedThenHistory:
                case PeerResponse.NewRole:
                case PeerResponse.LoginRefused:
                case PeerResponse.WrongLoginChannel:
                case PeerResponse.WrongFirstPacket:
                    // Independently composed GameChatPacket 1004 and GameLoginHistoryPacket
                    // 2078 plaintext fixtures; only wire encryption uses session cipher.
                    byte[] talk = Convert.FromHexString(response switch
                    {
                        PeerResponse.NewRole =>
                            "2500EC03000000003508000000000000000000000000000004000000084E45575F524F4C45",
                        PeerResponse.LoginRefused =>
                            "4800EC030000000035080000000000000000000000000000040000002B546865207365727665722069732066756C6C2E20506C656173652074727920616761696E206C617465722E",
                        _ => "2600EC0300000000350800000000000000000000000000000400000009414E535745525F4F4B",
                    });
                    if (response == PeerResponse.WrongLoginChannel)
                    {
                        talk[8] = 0x34;
                    }
                    else if (response == PeerResponse.WrongFirstPacket)
                    {
                        talk[2] = 0xED;
                    }

                    byte[] firstWire = new byte[talk.Length + 8];
                    talk.CopyTo(firstWire, 0);
                    "TQServer"u8.CopyTo(firstWire.AsSpan(talk.Length));
                    server.EncryptOutbound(firstWire, firstWire);

                    if (response == PeerResponse.AcceptedThenHistory)
                    {
                        byte[] historyWire =
                            Convert.FromHexString("0A001E080000000000005451536572766572");
                        server.EncryptOutbound(historyWire, historyWire);
                        byte[] joined = new byte[firstWire.Length + historyWire.Length];
                        firstWire.CopyTo(joined, 0);
                        historyWire.CopyTo(joined, firstWire.Length);
                        await stream.WriteAsync(joined, token);
                    }
                    else
                    {
                        await stream.WriteAsync(firstWire, token);
                    }

                    Assert.Equal(0, await ObserveCloseAsync(stream, token));
                    return;

                case PeerResponse.NoLoginResponse:
                    return;

                case PeerResponse.SilentAfterProof:
                    Assert.Equal(0, await ObserveCloseAsync(stream, token));
                    return;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(minimal);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static async Task<LoginAccountExchangeResult> AuthenticatedAccountAsync(
        TcpListener listener, int port, CancellationToken token, string host = "127.0.0.1")
    {
        LoginAccountSession session = await LoginAccountSession.OpenAsync(
            "127.0.0.1", Port(listener),
            new LoginAccountTimeouts(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)), token);
        try
        {
            LoginAccountResponse1055 response = await session.AuthenticateAsync(
                "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
                static () => true, TimeSpan.FromSeconds(5), token);
            Assert.True(response.IsSuccess);
            // The local peer uses a fixed, independent native 1055 ciphertext.
            // The test only substitutes its loopback game-listener port.
            LoginAccountResponse1055 handoff = response with
            {
                GamePort = (uint)port,
                GameHost = host
            };
            LoginAccountExchangeResult result = LoginAccountExchangeResult.Success(handoff, session);
            try
            {
                await result.CompletePostAuthenticationAsync(ReadOnlyMemory<byte>.Empty,
                    5517, TimeSpan.FromSeconds(5), token);
                return result;
            }
            catch
            {
                await result.DisposeAsync();
                throw;
            }
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    private static async Task AccountPeerAsync(TcpListener listener, CancellationToken token)
    {
        using TcpClient peer = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = peer.GetStream();
        await stream.WriteAsync(Convert.FromHexString(SeedWire), token);
        byte[] request = new byte[StandardAccountRequest1060.PacketLength];
        await stream.ReadExactlyAsync(request, token);
        await stream.WriteAsync(Convert.FromHexString(SuccessWire), token);

        byte[] reports = new byte[52 + 28];
        await stream.ReadExactlyAsync(reports, token);
        // Independently pinned post-1055 account-report ciphertext fixtures.
        Assert.Equal(
            "0294E07140E1CE61C516AABE84506DF9409127320BD7E779C413ADBF8B5568F24F9E2A3302DCE272C310A0B0825A63FB4E9B2D34" +
            "C8D12C3B862E81A0510E6EF46ACE17D746C0AF74C11AA6B2805469FD",
            Convert.ToHexString(reports));
    }

    private static async Task CompletedGamePeerAsync(TcpListener listener,
        TaskCompletionSource<byte[]> encryptedMessage, CancellationToken token)
    {
        try
        {
            await CompletedGamePeerCoreAsync(listener, encryptedMessage, token);
        }
        catch (Exception error)
        {
            // Surface a failed peer parser immediately instead of making the
            // parent test time out on an unresolved completion source.
            encryptedMessage.TrySetException(error);
            throw;
        }
    }

    private static async Task CompletedGamePeerCoreAsync(TcpListener listener,
        TaskCompletionSource<byte[]> encryptedMessage, CancellationToken token)
    {
        using TcpClient peer = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = peer.GetStream();
        byte[] challenge = Convert.FromHexString(EncryptedChallenge);
        await stream.WriteAsync(challenge.AsMemory(0, 3), token);
        await stream.WriteAsync(challenge.AsMemory(3, 12), token);
        await stream.WriteAsync(challenge.AsMemory(15), token);

        byte[] header = new byte[11];
        await stream.ReadExactlyAsync(header, token);
        using GameHandshakeBootstrapCipher decryptor = GameHandshakeBootstrapCipher.CreateDecryptor();
        decryptor.Transform(header, header);
        int declared = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(7));
        Assert.InRange(declared, 31, 1017);

        // The 7-byte native prefix precedes a declared region that INCLUDES
        // its own four-byte length field, already consumed in 'header'.
        byte[] body = new byte[declared - sizeof(int)];
        await stream.ReadExactlyAsync(body, token);
        decryptor.Transform(body, body);
        int offset = 0;
        int paddingLength = BinaryPrimitives.ReadInt32LittleEndian(body);
        Assert.InRange(paddingLength, 10, 49);
        offset += 4 + paddingLength;
        int publicLength = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(offset));
        offset += 4;
        Assert.InRange(publicLength, 1, 128);
        byte[] publicHex = body.AsSpan(offset, publicLength).ToArray();
        offset += publicLength;
        Assert.Equal("TQClient"u8.ToArray(), body.AsSpan(offset).ToArray());

        Assert.True(GameServerChallengeDecoder.TryParsePublicKey(publicHex, out BigInteger clientPublic));
        BigInteger prime = new(Convert.FromHexString(GameServerChallengeDecoder.PrimeHex),
            isUnsigned: true, isBigEndian: true);
        BigInteger shared = BigInteger.ModPow(clientPublic, new BigInteger(2), prime);
        byte[] minimal = shared.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] key = new byte[64];
        minimal.CopyTo(key, 0);
        try
        {
            byte[] message = "native-server-session"u8.ToArray();
            byte[] encoded = new byte[message.Length];
            using GameSessionCipher server = new(key,
                [21, 22, 23, 24, 25, 26, 27, 28], [1, 2, 3, 4, 5, 6, 7, 8]);
            server.EncryptOutbound(message, encoded);
            encryptedMessage.TrySetResult(encoded);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(minimal);
        }

        Assert.Equal(0, await ObserveCloseAsync(stream, token));
    }

    private static async Task ChallengeOnlyPeerAsync(
        TcpListener listener, byte[] challenge, CancellationToken token)
    {
        using TcpClient peer = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = peer.GetStream();
        await stream.WriteAsync(challenge, token);
        Assert.Equal(0, await ObserveCloseAsync(stream, token));
    }

    private static async Task SilentGamePeerAsync(TcpListener listener,
        CancellationToken token, TaskCompletionSource<bool>? accepted = null)
    {
        using TcpClient peer = await listener.AcceptTcpClientAsync(token);
        accepted?.TrySetResult(true);
        Assert.Equal(0, await ObserveCloseAsync(peer.GetStream(), token));
    }

    private static async Task<int> ObserveCloseAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] one = new byte[1];
        try
        {
            return await stream.ReadAsync(one, token);
        }
        catch (IOException error) when (error.InnerException is SocketException
        { SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted })
        {
            return 0;
        }
    }

    private static TcpListener Listen()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static int Port(TcpListener listener) =>
        ((IPEndPoint)listener.LocalEndpoint).Port;
}
