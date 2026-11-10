using System.Net.Sockets;
using OpenConquer.Networking.Authentication;
using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed partial class GameServerHandshakeSessionTests
{
    [Fact]
    public async Task AdmittedGroundJumpUsesTheExistingSecuredSocketAndWriteLane()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = CreationPeerAsync(games, CreationScript.AcceptAndEnterWorld, token,
            world: new WorldEntryOptions(ExpectHeartbeat: true, ExpectGroundJump: true));

        await using LoginAccountExchangeResult account =
            await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game = await OpenCreationAsync(account, token);

        await game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), token);
        Assert.Equal(GameCharacterCreationKind.Created,
            (await game.ReadCharacterCreationResultAsync(TimeSpan.FromSeconds(5), token)).Kind);
        await game.SendRepeatedLoginProofAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(GameLoginControlKind.ExistingCharacter,
            (await game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token)).Kind);
        _ = await game.ReadExistingCharacterPreludeAsync(TimeSpan.FromSeconds(5), token);
        GameCharacterInfo1006 character = await game.ReadCharacterInfoAsync(TimeSpan.FromSeconds(5), token);
        _ = await GameWorldEntryBootstrap.RunAsync(game, character, cancellationToken: token);

        Task<GameSecuredInboundFrame?> walkReply = game.ReadWorldFrameAsync(token);
        await game.SendWorldWalkAsync(character.EntityId, 6, 0, 0x01020304, 0x05060708, token);
        using (GameSecuredInboundFrame walk = await walkReply.WaitAsync(TimeSpan.FromSeconds(5), token) ??
            throw new EndOfStreamException())
        {
            Assert.Equal((ushort)10005, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(walk.Packet.Span[2..]));
        }

        await game.SendWorldGroundJumpAsync(character.EntityId, 62, 110, 63, 106, 1010, 0x0585923B, token);

        await game.SendWorldLatencyTickAsync(character.EntityId, 0x0000ABCD, token);
        await game.SendWorldLatencyReportAsync(0x0337D30A, 42, 0x01020304, token);
        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }
}
