using OpenConquer.Client.Tests.Networking;
using OpenConquer.Client.World;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.World;

public sealed partial class WorldInboundStateTests
{
    [Fact]
    public void OriginCorrectionCullsOutOfRangePeersAndTheirPlayback()
    {
        WorldInboundState state = CreateState(heroId: 1_000_000);
        Assert.False(state.Apply(WorldSpawnPacketFixture.Create(1_000_001, x: 18, y: 18)));
        Assert.False(state.Apply(WorldSpawnPacketFixture.Create(1_000_002, x: 19, y: 1)));
        Assert.False(state.Apply(WorldSpawnPacketFixture.Create(1_000_003, x: 0, y: 19)));

        byte[] walk = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(walk, 1_000_002, packedDirection: 4,
            movementTick: 100, mapState: 1010);
        Assert.False(state.Apply(walk));
        Assert.Equal(WorldRolePlayback.GroundWalkFirst,
            state.RemoteRoles.GetPlayback(1_000_002)!.Action);

        Assert.True(state.Apply(Correction(1_000_000, 0, 0)));
        int revision = state.RemoteRoles.Revision;
        state.CullRemoteRoles();

        Assert.Equal((0, 0), (state.X, state.Y));
        Assert.Equal(1, state.RemoteRoles.Count);
        Assert.True(state.RemoteRoles.Contains(1_000_001));
        Assert.False(state.RemoteRoles.Contains(1_000_002));
        Assert.False(state.RemoteRoles.Contains(1_000_003));
        Assert.Null(state.RemoteRoles.GetPlayback(1_000_002));
        Assert.Equal(unchecked(revision + 2), state.RemoteRoles.Revision);

        state.CullRemoteRoles();
        Assert.Equal(unchecked(revision + 2), state.RemoteRoles.Revision);
    }

    [Fact]
    public void NegativeHeroTilesRetainOnlyPeersWithinTheVisibilityWindow()
    {
        WorldInboundState state = CreateState(heroId: 1_000_000);
        Assert.False(state.Apply(WorldSpawnPacketFixture.Create(1_000_001, x: 17, y: 17)));
        Assert.False(state.Apply(WorldSpawnPacketFixture.Create(1_000_002, x: 18, y: 0)));
        Assert.False(state.Apply(WorldSpawnPacketFixture.Create(1_000_003, x: 0, y: 18)));

        Assert.True(state.Apply(Correction(1_000_000, 0, 0)));
        byte[] walk = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(walk, 1_000_000, packedDirection: 2,
            movementTick: 100, mapState: 1010);
        Assert.True(state.Apply(walk));
        GameWalk10005.WriteOrdinary(walk, 1_000_000, packedDirection: 4,
            movementTick: 101, mapState: 1010);
        Assert.True(state.Apply(walk));
        Assert.Equal((-1, -1), (state.X, state.Y));

        state.CullRemoteRoles();

        Assert.Equal(1, state.RemoteRoles.Count);
        Assert.True(state.RemoteRoles.Contains(1_000_001));
        Assert.False(state.RemoteRoles.Contains(1_000_002));
        Assert.False(state.RemoteRoles.Contains(1_000_003));
    }

    [Fact]
    public void SignedCoordinateExtremesDoNotOverflowPeerDistanceCalculation()
    {
        WorldRemoteRoles roles = new();
        roles.Upsert(new GamePlayerSpawn10014(1_000_001, "Peer"u8.ToArray(),
            1001, 0, 0, 0, 0, 0, 100, 1, 0, 0, 0, 0, 0, 0));

        roles.Cull(int.MinValue, int.MaxValue);

        Assert.Equal(0, roles.Count);
        Assert.Equal(2, roles.Revision);
    }
}
