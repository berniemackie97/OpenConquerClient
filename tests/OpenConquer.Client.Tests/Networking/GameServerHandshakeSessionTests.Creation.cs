using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using OpenConquer.Networking.Authentication;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed partial class GameServerHandshakeSessionTests
{
    private static readonly byte[] s_hardwareAddress = [0, 0x11, 0x22, 0xAA, 0xBB, 0xCC];

    // Independently composed plaintext fixtures. The session token 0x11223344 and key 0x89ABCDEF come from the
    // pinned 1055 fixture; the proof bytes repeat the AUTH-001M vector.
    private const string ProofPacket = "1C001C0444332211EFCDAB897C00456E001122AABBCC00008D150000";
    private static readonly string s_heroRequest =
        "7C00E903" + "00000000" + Zeros(16) + "4865726F" + Zeros(44) + "EB03" + "0A00" + "44332211" + Zeros(44);
    private static readonly string s_cancelRequest =
        "7C00E903" + "01000000" + Zeros(16) + Zeros(48) + "0000" + "0000" + "44332211" + Zeros(44);

    [Fact]
    public async Task CreationContinuationRetriesAfterRefusalAndRejoinsTheExistingBootstrap()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = CreationPeerAsync(games, CreationScript.RefuseThenAccept, token);

        await using LoginAccountExchangeResult account = await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game = await OpenCreationAsync(account, token);

        await game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), token);
        GameCharacterCreationOutcome refused = await game.ReadCharacterCreationResultAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(GameCharacterCreationKind.Refused, refused.Kind);
        Assert.Equal("Character name is already in use.", Encoding.ASCII.GetString(refused.RefusalBytes.Span));

        await game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), token);
        GameCharacterCreationOutcome created = await game.ReadCharacterCreationResultAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(GameCharacterCreationKind.Created, created.Kind);
        Assert.True(created.RefusalBytes.IsEmpty);

        await game.SendRepeatedLoginProofAsync(0x6E45, s_hardwareAddress, TimeSpan.FromSeconds(5), token);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.SendRepeatedLoginProofAsync(0x6E45, s_hardwareAddress, TimeSpan.FromSeconds(5), token));
        Assert.Equal(GameLoginControlKind.ExistingCharacter,
            (await game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token)).Kind);
        GameExistingCharacterPrelude prelude = await game.ReadExistingCharacterPreludeAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(1u, prelude.ServerState.Value);
        GameCharacterInfo1006 character = await game.ReadCharacterInfoAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(0x11223344u, character.EntityId);

        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task CancelSendsTheNativeCancelAndClosesTheSession()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = CreationPeerAsync(games, CreationScript.Cancel, token);

        await using LoginAccountExchangeResult account = await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game = await OpenCreationAsync(account, token);

        await game.SendCharacterCreationCancelAsync(TimeSpan.FromSeconds(5), token);

        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task CreationStepsRejectOutOfOrderUse()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = CreationPeerAsync(games, CreationScript.NewRoleOnly, token);

        await using LoginAccountExchangeResult account = await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game = await OpenCreationAsync(account, token);

        // Nothing but the create-role exchange may use the socket, in its own order.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadCharacterCreationResultAsync(TimeSpan.FromSeconds(5), token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.SendRepeatedLoginProofAsync(0x6E45, s_hardwareAddress, TimeSpan.FromSeconds(5), token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadExistingCharacterPreludeAsync(TimeSpan.FromSeconds(5), token));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            game.SendCharacterCreationAsync(new byte[16], 1003, 10, TimeSpan.FromSeconds(5), token));

        // Misuse leaves the exchange intact.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadCharacterCreationResultAsync(TimeSpan.FromSeconds(5), token));

        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task CreationIsUnavailableOnTheExistingCharacterRoute()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = SecuredGamePeerAsync(games, PeerResponse.AcceptedThenHistory, token);

        await using LoginAccountExchangeResult account = await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game = await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        await game.SendLoginProofAsync(0x6E45, s_hardwareAddress, TimeSpan.FromSeconds(5), token);
        Assert.Equal(GameLoginControlKind.ExistingCharacter,
            (await game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token)).Kind);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.SendCharacterCreationCancelAsync(TimeSpan.FromSeconds(5), token));

        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Theory]
    [InlineData(CreationScript.ResultOnLoginChannel)]
    [InlineData(CreationScript.CloseInsteadOfResult)]
    public async Task UnexpectedCreationResultIsTerminal(CreationScript script)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = CreationPeerAsync(games, script, token);

        await using LoginAccountExchangeResult account = await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game = await OpenCreationAsync(account, token);
        await game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), token);

        Exception failure = await Assert.ThrowsAnyAsync<Exception>(() =>
            game.ReadCharacterCreationResultAsync(TimeSpan.FromSeconds(5), token));
        Assert.True(failure is InvalidDataException or EndOfStreamException);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), token));

        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task AnotherNewRoleAfterTheRepeatedProofIsTerminal()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = CreationPeerAsync(games, CreationScript.NewRoleAfterProof, token);

        await using LoginAccountExchangeResult account = await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game = await OpenCreationAsync(account, token);
        await game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), token);
        Assert.Equal(GameCharacterCreationKind.Created,
            (await game.ReadCharacterCreationResultAsync(TimeSpan.FromSeconds(5), token)).Kind);
        await game.SendRepeatedLoginProofAsync(0x6E45, s_hardwareAddress, TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<InvalidDataException>(() => game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token));

        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    public enum CreationScript
    {
        RefuseThenAccept,
        Cancel,
        NewRoleOnly,
        ResultOnLoginChannel,
        CloseInsteadOfResult,
        NewRoleAfterProof,
    }

    private static async Task<GameServerHandshakeSession> OpenCreationAsync(LoginAccountExchangeResult account,
        CancellationToken token)
    {
        GameServerHandshakeSession game = await GameServerHandshakeSession.OpenAsync(account, s_timeouts, token);
        try
        {
            await game.SendLoginProofAsync(0x6E45, s_hardwareAddress, TimeSpan.FromSeconds(5), token);
            Assert.Equal(GameLoginControlKind.CharacterCreation,
                (await game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token)).Kind);
            return game;
        }
        catch
        {
            await game.DisposeAsync();
            throw;
        }
    }

    private static async Task CreationPeerAsync(TcpListener listener, CreationScript script, CancellationToken token)
    {
        using SecuredTestPeer peer = await AcceptSecuredPeerAsync(listener, token);
        Assert.Equal(ProofPacket, Convert.ToHexString(await peer.ReadPacketAsync(28, token)));
        await peer.WritePacketsAsync(token, Talk(GameControlTalk1004.LoginChannel, "NEW_ROLE"));

        switch (script)
        {
            case CreationScript.NewRoleOnly:
                break;

            case CreationScript.Cancel:
                Assert.Equal(s_cancelRequest, Convert.ToHexString(await peer.ReadPacketAsync(124, token)));
                break;

            case CreationScript.ResultOnLoginChannel:
                Assert.Equal(s_heroRequest, Convert.ToHexString(await peer.ReadPacketAsync(124, token)));
                await peer.WritePacketsAsync(token, Talk(GameControlTalk1004.LoginChannel, "ANSWER_OK"));
                break;

            case CreationScript.CloseInsteadOfResult:
                Assert.Equal(s_heroRequest, Convert.ToHexString(await peer.ReadPacketAsync(124, token)));
                return;

            case CreationScript.RefuseThenAccept:
            case CreationScript.NewRoleAfterProof:
                if (script == CreationScript.RefuseThenAccept)
                {
                    Assert.Equal(s_heroRequest, Convert.ToHexString(await peer.ReadPacketAsync(124, token)));
                    await peer.WritePacketsAsync(token,
                        Talk(GameControlTalk1004.CreationChannel, "Character name is already in use."));
                }

                Assert.Equal(s_heroRequest, Convert.ToHexString(await peer.ReadPacketAsync(124, token)));
                await peer.WritePacketsAsync(token, Talk(GameControlTalk1004.CreationChannel, "ANSWER_OK"));
                Assert.Equal(ProofPacket, Convert.ToHexString(await peer.ReadPacketAsync(28, token)));
                if (script == CreationScript.NewRoleAfterProof)
                {
                    await peer.WritePacketsAsync(token, Talk(GameControlTalk1004.LoginChannel, "NEW_ROLE"));
                    break;
                }

                // The existing-character bootstrap that follows on the same socket.
                byte[] characterWire = CreateCharacterInfoWire(invalidType: false);
                await peer.WritePacketsAsync(token, Talk(GameControlTalk1004.LoginChannel, "ANSWER_OK"),
                    Convert.FromHexString("0A001E08000000000000"), Convert.FromHexString("08001F0801000000"),
                    characterWire[..^8]);
                break;
        }

        Assert.Equal(0, await ObserveCloseAsync(peer.Stream, token));
    }

    private static string Zeros(int byteCount) => new('0', byteCount * 2);

    /// <summary>A 1004 talk with three empty entries before the message, as the GameServer composes it.</summary>
    private static byte[] Talk(ushort channel, string message)
    {
        byte[] text = Encoding.ASCII.GetBytes(message);
        byte[] packet = new byte[25 + 3 + 1 + text.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)packet.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), GameControlTalk1004.PacketType);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), channel);
        packet[24] = 4;
        packet[28] = (byte)text.Length;
        text.CopyTo(packet, 29);
        return packet;
    }
}
