using System.Net.Sockets;
using OpenConquer.Client.Tests.UI.Role;
using OpenConquer.Client.UI.Role;
using OpenConquer.Networking.Authentication;
using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed partial class GameServerHandshakeSessionTests
{
    [Fact]
    public async Task RoleOwner_HandsTheAdmittedSessionOffBeforeClosingTheScreenAndKeepsItUsable()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = CreationPeerAsync(games, CreationScript.AcceptAndEnterWorld, token);
        await using LoginAccountExchangeResult account = await AuthenticatedAccountAsync(accounts, Port(games), token);
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
        GameWorldEntrySnapshot world = await GameWorldEntryBootstrap.RunAsync(game, character, cancellationToken: token);

        List<string> calls = [];
        TestRoleCreationScreen screen = new(calls)
        {
            AdmittedSession = game,
            AdmittedWorld = world
        };
        bool hasWorld = false;
        int admissions = 0;
        await using ClientRoleCreation owner = new(new ClientRoleCreationTests.MissingContentSource(), _ => { }, () => { },
            () => !hasWorld, () => false, (accepted, entry) =>
            {
                calls.Add("admit");
                admissions++;
                Assert.Same(game, accepted);
                Assert.Same(world, entry);
                Assert.Equal(0, screen.DisposeCalls);
                hasWorld = true;
            }, _ => throw new InvalidOperationException("Admission must not return to login."));
        owner.TryOpen(() => screen, () => throw new InvalidOperationException("The screen owns the accepted session."));

        owner.Update(TimeSpan.FromMilliseconds(25));
        owner.ObserveClose();
        owner.Update(TimeSpan.Zero);
        await owner.DisposeAsync();

        Assert.Equal(["tick", "take", "admit", "dispose"], calls);
        Assert.Equal(1, admissions);
        Assert.Equal(1, screen.DisposeCalls);
        Assert.False(owner.IsActive);
        Task<GameSecuredInboundFrame?> echoed = game.ReadWorldFrameAsync(token);
        await game.SendWorldWalkAsync(character.EntityId, 6, 0, 0x01020304, 0x05060708, token);
        using GameSecuredInboundFrame walk = await echoed.WaitAsync(TimeSpan.FromSeconds(5), token) ??
            throw new EndOfStreamException();
        Assert.Equal(GameWalk10005.PacketLength, walk.Packet.Length);
        GameWalk10005 confirmed = GameWalk10005.Read(GameMessages.Load(walk.Packet.ToArray()));
        Assert.Equal(character.EntityId, confirmed.EntityId);
        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }
}
