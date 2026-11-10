using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using OpenConquer.Networking.Authentication;
using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

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
        // The login coordinator disposes the AccountServer exchange before the role screen submits a name.
        await account.DisposeAsync();

        await game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), token);
        GameCharacterCreationOutcome refused = await game.ReadCharacterCreationResultAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(GameCharacterCreationKind.Refused, refused.Kind);
        Assert.Equal("Character name is already in use.", Encoding.ASCII.GetString(refused.RefusalBytes.Span));

        await game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), token);
        GameCharacterCreationOutcome created = await game.ReadCharacterCreationResultAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(GameCharacterCreationKind.Created, created.Kind);
        Assert.True(created.RefusalBytes.IsEmpty);

        await game.SendRepeatedLoginProofAsync(TimeSpan.FromSeconds(5), token);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.SendRepeatedLoginProofAsync(TimeSpan.FromSeconds(5), token));
        Assert.Equal(GameLoginControlKind.ExistingCharacter,
            (await game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token)).Kind);
        GameExistingCharacterPrelude prelude = await game.ReadExistingCharacterPreludeAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(1u, prelude.ServerState.Value);
        GameCharacterInfo1006 character = await game.ReadCharacterInfoAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(0x11223344u, character.EntityId);

        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    // 20_notes Network/bootstrap-tolerance-cp24d.md. nativeShortFrames: 1110/1016/10010 at lengths native parsers
    // accept (CP2.4d1). bit38Map: 1110 flags with bit38 make the client send 408 (0x732CFD); otherwise none is sent.
    // packedAppearance 12001 (body digit 2): 1151 subtype 2 follows EnterMap (0x7648B4..0x7648E6).
    [Theory]
    [InlineData(false, false, 0u)]
    [InlineData(true, false, 0u)]
    [InlineData(false, true, 12001u)]
    [InlineData(true, true, 12001u)]
    [InlineData(false, false, 0u, true)]
    public async Task CreatedCharacter_CompletesWorldAdmissionRequestsOnTheOriginalSocket(bool nativeShortFrames, bool bit38Map,
        uint packedAppearance, bool scrambled = false)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        Task gamePeer = CreationPeerAsync(games, nativeShortFrames
            ? CreationScript.AcceptAndEnterWorldWithNativeShortFrames
            : CreationScript.AcceptAndEnterWorld, token,
            world: new WorldEntryOptions(ClientStateAppliedMap: bit38Map, PackedAppearance: packedAppearance, Scrambled: scrambled,
                ExpectHeartbeat: true, ClientTimestamp: 0x100, ExpectMissionCatalog: true));

        await using LoginAccountExchangeResult account = await AuthenticatedAccountAsync(accounts, Port(games), token);
        await accountPeer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await using GameServerHandshakeSession game = await OpenCreationAsync(account, token);
        int[] missionIds = [600, 601];
        game.SetMissionCatalog(missionIds);
        missionIds[0] = 999;

        await game.SendCharacterCreationAsync("Hero"u8.ToArray(), 1003, 10, TimeSpan.FromSeconds(5), token);
        Assert.Equal(GameCharacterCreationKind.Created,
            (await game.ReadCharacterCreationResultAsync(TimeSpan.FromSeconds(5), token)).Kind);
        await game.SendRepeatedLoginProofAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(GameLoginControlKind.ExistingCharacter,
            (await game.ReadLoginControlAsync(TimeSpan.FromSeconds(5), token)).Kind);
        await game.SendMissionCatalogAsync(TimeSpan.FromSeconds(5), token);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.SendMissionCatalogAsync(TimeSpan.FromSeconds(5), token));
        _ = await game.ReadExistingCharacterPreludeAsync(TimeSpan.FromSeconds(5), token);
        GameCharacterInfo1006 character = await game.ReadCharacterInfoAsync(TimeSpan.FromSeconds(5), token);

        int mapEntered = 0;
        using ManualDeadlineClock clock = new((long)uint.MaxValue + 0x101);
        GameWorldEntrySnapshot world = await GameWorldEntryBootstrap.RunAsync(game, character, () => mapEntered++,
            clock: clock, cancellationToken: token);

        Assert.Equal(0x100u, world.HeartbeatStartedAt);
        Assert.Equal(1, mapEntered);
        Assert.Equal(0x11223344u, world.Character.EntityId);
        // Scrambled: the map key comes from the 74 reply (0x6FF478), weather kind 6 is a no-op (0x7606CD), and the
        // unknown frame before the reply is kept for world ingress ahead of the entry position.
        Assert.Equal((1002u, scrambled ? 1016u : 1015u, (ushort)61, (ushort)101),
            (world.MapId, world.MapDataId, world.X, world.Y));
        Assert.Equal(scrambled ? 0u : 1u, world.WeatherKind);
        Assert.Equal((byte)5, world.DirectionIndex);
        Assert.Equal(scrambled ? 1 : 0, world.HydrationPackets.Count);
        Assert.Equal(scrambled ? 1 : 0, world.HydrationEnteredAt);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            game.ReadFrameAsync(TimeSpan.FromSeconds(5), token));

        Task<GameSecuredInboundFrame?> echoed = game.ReadWorldFrameAsync(token);
        await game.SendWorldWalkAsync(character.EntityId, 6, 0, 0x01020304, 0x05060708, token);
        using (GameSecuredInboundFrame walk = await echoed.WaitAsync(TimeSpan.FromSeconds(5), token) ??
            throw new EndOfStreamException("GameServer closed before the walk acknowledgement."))
        {
            Assert.Equal(GameWalk10005.PacketLength, walk.Packet.Length);
            GameWalk10005 confirmed = GameWalk10005.Read(GameMessages.Load(walk.Packet.ToArray()));
            Assert.Equal((character.EntityId, (byte)6, 0x01020304u, 0x05060708u),
                (confirmed.EntityId, confirmed.DirectionIndex, confirmed.MovementTick, confirmed.MapState));
        }

        // The heartbeat and latency report share the world write lane (CHero_TickHeartbeatAndPeriodicSessionSync 0x661ADD).
        await game.SendWorldLatencyTickAsync(character.EntityId, 0x0000ABCD, token);
        await game.SendWorldLatencyReportAsync(0x0337D30A, 42, 0x01020304, token);

        using CancellationTokenSource stopWorldRead = new();
        Task pending = game.ReadWorldFrameAsync(stopWorldRead.Token);
        Assert.False(pending.IsCompleted);
        await stopWorldRead.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        await game.DisposeAsync();
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorldEntryCancellationClosesTheSessionAndPreservesTheCause(bool cancelCaller)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener accounts = Listen();
        using TcpListener games = Listen();
        Task accountPeer = AccountPeerAsync(accounts, token);
        TaskCompletionSource worldEntryRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task gamePeer = CreationPeerAsync(games, CreationScript.StallWorldEntry, token, worldEntryRequested: worldEntryRequested);

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

        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        using ManualDeadlineClock clock = new();
        TimeSpan timeout = TimeSpan.FromMilliseconds(250);
        Task<GameWorldEntrySnapshot> bootstrap = GameWorldEntryBootstrap.RunAsync(game,
            character, completionTimeout: timeout, clock: clock, cancellationToken: caller.Token);
        await worldEntryRequested.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        Assert.Equal(timeout, clock.DueTime);
        Assert.False(bootstrap.IsCompleted);

        if (cancelCaller)
        {
            await caller.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bootstrap);
        }
        else
        {
            clock.Fire();
            await Assert.ThrowsAsync<TimeoutException>(() => bootstrap);
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            game.SendWorldActionAsync(GameWorldAction10010.GetItems, character.EntityId, 0,
                TimeSpan.FromSeconds(5), token));
        await gamePeer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task CancelSendsCreateRoleCancellationAndClosesTheSession()
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
            game.SendRepeatedLoginProofAsync(TimeSpan.FromSeconds(5), token));
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
        await game.SendRepeatedLoginProofAsync(TimeSpan.FromSeconds(5), token);

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
        AcceptAndEnterWorld,
        AcceptAndEnterWorldWithNativeShortFrames,
        StallWorldEntry,
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

    internal static Task CoordinatorCreationPeerAsync(TcpListener listener, bool enterWorld, CancellationToken token) =>
        CreationPeerAsync(listener, enterWorld ? CreationScript.AcceptAndEnterWorld : CreationScript.NewRoleOnly, token, coordinator: true);

    private sealed record WorldEntryOptions(bool NativeShortFrames = false, bool ClientStateAppliedMap = false,
        uint PackedAppearance = 0, bool Scrambled = false, bool ExpectHeartbeat = false,
        uint? ClientTimestamp = null, bool ExpectMissionCatalog = false, bool ExpectGroundJump = false);

    private static async Task CreationPeerAsync(TcpListener listener, CreationScript script, CancellationToken token,
        bool coordinator = false, WorldEntryOptions? world = null, TaskCompletionSource? worldEntryRequested = null)
    {
        world ??= new WorldEntryOptions();
        using SecuredTestPeer peer = await AcceptSecuredPeerAsync(listener, token);
        // Coordinator fixtures model native NetBIOS failure: six raw zeros instead of the explicit test MAC.
        string proof = coordinator ? ProofPacket.Replace("001122AABBCC", "000000000000", StringComparison.Ordinal) : ProofPacket;
        Assert.Equal(proof, Convert.ToHexString(await peer.ReadPacketAsync(28, token)));
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
            case CreationScript.AcceptAndEnterWorld:
            case CreationScript.AcceptAndEnterWorldWithNativeShortFrames:
            case CreationScript.StallWorldEntry:
                if (script == CreationScript.RefuseThenAccept)
                {
                    Assert.Equal(s_heroRequest, Convert.ToHexString(await peer.ReadPacketAsync(124, token)));
                    await peer.WritePacketsAsync(token,
                        Talk(GameControlTalk1004.CreationChannel, "Character name is already in use."));
                }

                Assert.Equal(s_heroRequest, Convert.ToHexString(await peer.ReadPacketAsync(124, token)));
                await peer.WritePacketsAsync(token, Talk(GameControlTalk1004.CreationChannel, "ANSWER_OK"));
                Assert.Equal(proof, Convert.ToHexString(await peer.ReadPacketAsync(28, token)));
                if (script == CreationScript.NewRoleAfterProof)
                {
                    await peer.WritePacketsAsync(token, Talk(GameControlTalk1004.LoginChannel, "NEW_ROLE"));
                    break;
                }

                // The existing-character bootstrap that follows on the same socket.
                byte[] characterWire = CreateCharacterInfoWire(invalidType: false, world.PackedAppearance);
                await peer.WritePacketsAsync(token, Talk(GameControlTalk1004.LoginChannel, "ANSWER_OK"),
                    Convert.FromHexString("0A001E08000000000000"), Convert.FromHexString("08001F0801000000"),
                    characterWire[..^8]);
                if (world.ExpectMissionCatalog)
                {
                    await RequireMissionCatalogAsync(peer, token);
                }

                if (script is CreationScript.AcceptAndEnterWorld or CreationScript.AcceptAndEnterWorldWithNativeShortFrames)
                {
                    await WorldEntryPeerAsync(peer, world with
                    {
                        NativeShortFrames = script == CreationScript.AcceptAndEnterWorldWithNativeShortFrames,
                    }, token);
                    byte[] walk = await peer.ReadPacketAsync(GameWalk10005.PacketLength, token);
                    Assert.Equal(GameWalk10005.PacketLength, walk.Length);
                    GameWalk10005 request = GameWalk10005.Read(GameMessages.Load(walk));
                    Assert.Equal((0x11223344u, (byte)6, 0x01020304u, 0x05060708u),
                        (request.EntityId, request.DirectionIndex, request.MovementTick, request.MapState));
                    await peer.WritePacketsAsync(token, walk);

                    if (world.ExpectGroundJump)
                    {
                        byte[] jump = await peer.ReadPacketAsync(GameWorldAction10010.PacketLength, token);
                        Assert.Equal("26001a27443322113e006e00000000003b928505890000003f006a00f2030000ffffffff0000",
                            Convert.ToHexString(jump));
                        await peer.WritePacketsAsync(token, jump);
                    }
                }

                if (world.ExpectHeartbeat)
                {
                    byte[] heartbeat = await peer.ReadPacketAsync(88, token);
                    Assert.Equal((88, 1009, 0x11223344u, 27, 0x0000ABCDu),
                        (BinaryPrimitives.ReadUInt16LittleEndian(heartbeat), BinaryPrimitives.ReadUInt16LittleEndian(heartbeat.AsSpan(2)),
                            BinaryPrimitives.ReadUInt32LittleEndian(heartbeat.AsSpan(4)), BinaryPrimitives.ReadUInt16LittleEndian(heartbeat.AsSpan(12)),
                            BinaryPrimitives.ReadUInt32LittleEndian(heartbeat.AsSpan(16))));
                    Assert.Equal("14000D04000000000AD337032A00000004030201",
                        Convert.ToHexString(await peer.ReadPacketAsync(20, token)));
                }
                else if (script == CreationScript.StallWorldEntry)
                {
                    byte[] request = await peer.ReadPacketAsync(GameWorldAction10010.PacketLength, token);
                    Assert.True(GameWorldAction10010.TryRead(request, out GameWorldAction10010 action));
                    Assert.Equal((GameWorldAction10010.EnterMap, 0x11223344u, 0u),
                        (action.Action, action.EntityId, action.ParameterPair));
                    worldEntryRequested?.SetResult();
                }
                break;
        }

        Assert.Equal(0, await ObserveCloseAsync(peer.Stream, token));
    }

    internal static async Task AcceptedCoordinatorWorldPeerAsync(TcpListener listener, CancellationToken token)
    {
        using SecuredTestPeer peer = await AcceptSecuredPeerAsync(listener, token);
        byte[] proof = await peer.ReadPacketAsync(28, token);
        Assert.Equal(0x6E45, BinaryPrimitives.ReadUInt16LittleEndian(proof.AsSpan(14)));
        Assert.All(proof.AsSpan(16, 6).ToArray(), value => Assert.Equal(0, value));

        byte[] character = CreateCharacterInfoWire(invalidType: false);
        await peer.WritePacketsAsync(token, Talk(GameControlTalk1004.LoginChannel, "ANSWER_OK"),
            Convert.FromHexString("0A001E08000000000000"), Convert.FromHexString("08001F0801000000"),
            character[..^8]);
        await RequireMissionCatalogAsync(peer, token);
        await WorldEntryPeerAsync(peer, new WorldEntryOptions(), token);
        Assert.Equal(0, await ObserveCloseAsync(peer.Stream, token));
    }

    private static async Task RequireMissionCatalogAsync(SecuredTestPeer peer, CancellationToken token) =>
        Assert.Equal(Convert.FromHexString("20006E0403000200580200000000000000000000590200000000000000000000"),
            await peer.ReadPacketAsync(32, token));

    private static async Task WorldEntryPeerAsync(SecuredTestPeer peer, WorldEntryOptions options, CancellationToken token)
    {
        const uint hero = 0x11223344;
        bool nativeShortFrames = options.NativeShortFrames;
        await RequireWorldRequestAsync(GameWorldAction10010.EnterMap, hero);
        if (options.PackedAppearance / 1000 % 10 == 2)
        {
            byte[] flower = await peer.ReadPacketAsync(GameFlowerRankRequest1151.PacketLength, token);
            byte[] expected = new byte[GameFlowerRankRequest1151.PacketLength];
            BinaryPrimitives.WriteUInt16LittleEndian(expected, 80);
            BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 1151);
            expected[4] = 2;
            Assert.Equal(Convert.ToHexString(expected), Convert.ToHexString(flower));
        }

        byte[] map = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(map, 20);
        BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(2), 1110);
        BinaryPrimitives.WriteUInt32LittleEndian(map.AsSpan(4), 1002);
        BinaryPrimitives.WriteUInt32LittleEndian(map.AsSpan(8), 1015);
        if (options.ClientStateAppliedMap)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(map.AsSpan(12), 1UL << 38);
        }

        byte[] weather = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(weather, 20);
        BinaryPrimitives.WriteUInt16LittleEndian(weather.AsSpan(2), 1016);
        BinaryPrimitives.WriteUInt32LittleEndian(weather.AsSpan(4), 1);
        byte[] entered = WorldAction(GameWorldAction10010.EnterMap, hero, 1015);
        BinaryPrimitives.WriteUInt16LittleEndian(entered.AsSpan(22), 13);
        BinaryPrimitives.WriteUInt16LittleEndian(entered.AsSpan(24), 61);
        BinaryPrimitives.WriteUInt16LittleEndian(entered.AsSpan(26), 101);
        if (nativeShortFrames)
        {
            // Map flags, weather intensity/direction/parameter and EnterMap data past Y are omitted and read as zero.
            // A bit38 map keeps its flag word: truncating it would read the flags as zero.
            (map, weather, entered) = (options.ClientStateAppliedMap ? GameMessages.Truncate(map, 18) : GameMessages.Truncate(map, 12),
                GameMessages.Truncate(weather, 8), GameMessages.Truncate(entered, 28));
        }

        if (options.Scrambled)
        {
            // Native has no order gate: weather kind 6, an unknown frame, an acknowledgement for another entity, an
            // EnterMap reply with entity 0 and a different map key, and 1110 last.
            BinaryPrimitives.WriteUInt32LittleEndian(weather.AsSpan(4), 6);
            BinaryPrimitives.WriteUInt32LittleEndian(entered.AsSpan(4), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(entered.AsSpan(8), 1016);
            byte[] unknown = GameMessages.Header(8, 2030);
            await peer.WritePacketsAsync(token, weather, unknown, WorldAction(GameWorldAction10010.GetItems, hero + 1), entered, map);
        }
        else
        {
            await peer.WritePacketsAsync(token, map, weather, entered);
        }

        if (options.ClientStateAppliedMap)
        {
            await RequireWorldRequestAsync(GameWorldAction10010.ClientStateApplied, 0);
        }

        await RequireWorldRequestAsync(GameWorldAction10010.BoothClose, hero);
        foreach (ushort action in new ushort[]
                 {
                     GameWorldAction10010.GetItems, GameWorldAction10010.GetFriends,
                     GameWorldAction10010.GetWeaponSkills, GameWorldAction10010.GetMagic,
                     GameWorldAction10010.GetSyndicate, GameWorldAction10010.ReportSilentInfo,
                 })
        {
            await RequireWorldRequestAsync(action,
                action == GameWorldAction10010.ReportSilentInfo ? 0u : hero);
            if (action == GameWorldAction10010.GetFriends)
            {
                // 75-ack handler: 76, then main msg 0x401 -> PK mode 3 (0x6FF3CD, 0x4BDD30).
                await RequireWorldRequestAsync(GameWorldAction10010.SetPkMode, hero, parameter: 3);
            }

            // The acknowledgement parameter is not read (CMsgAction__Process consumes only +4 and +20).
            byte[] acknowledgement = WorldAction(action, hero, options.Scrambled ? 0x55u : 0u);
            if (nativeShortFrames)
            {
                // A nonzero +37 string count with trailing bytes: Parse 0x6FEB93 does not read the string list.
                acknowledgement = GameMessages.Extend(acknowledgement, 6);
                (acknowledgement[37], acknowledgement[38]) = (1, 4);
            }

            await peer.WritePacketsAsync(token, acknowledgement);
        }
        await RequireWorldRequestAsync(GameWorldAction10010.GetStatistics, hero);

        async Task RequireWorldRequestAsync(ushort expected, uint expectedEntity, uint parameter = 0)
        {
            byte[] request = await peer.ReadPacketAsync(GameWorldAction10010.PacketLength, token);
            Assert.True(GameWorldAction10010.TryRead(request, out GameWorldAction10010 action));
            Assert.Equal((expected, expectedEntity, parameter, (ushort)0, (ushort)0),
                (action.Action, action.EntityId, action.ParameterPair, action.X, action.Y));

            // CMsgAction__Create stamps timeGetTime (0x6FE99C); BuildType10010 (408) writes nothing there, and the 251
            // report carries the silent-data version: 1 with no ini/SlientVersion.ini (0x6BB3AC).
            if (expected == GameWorldAction10010.ReportSilentInfo)
            {
                Assert.Equal(1u, action.Timestamp);
            }
            else if (expected == GameWorldAction10010.ClientStateApplied)
            {
                Assert.Equal(0u, action.Timestamp);
            }
            else if (options.ClientTimestamp is uint timestamp)
            {
                Assert.Equal(timestamp, action.Timestamp);
            }
            else
            {
                Assert.NotEqual(0u, action.Timestamp);
            }
        }
    }

    private static byte[] WorldAction(ushort action, uint entityId, uint parameterPair = 0)
    {
        byte[] packet = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(packet, action, entityId, parameterPair);
        return packet;
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
