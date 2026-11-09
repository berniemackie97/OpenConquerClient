using OpenConquer.Client.Tests.Networking;
using OpenConquer.Client.World;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;
using static OpenConquer.Client.Tests.Networking.WireGoldenObservedClientFrames;

namespace OpenConquer.Client.Tests.World;

public sealed partial class WorldInboundStateTests
{
    [Fact]
    public void CapturedGroundJumpAcknowledgementsSetTheHeroTargetWithoutChangingFacing()
    {
        Sample[] samples = Read("10010-ground-jump.observed.json",
            GameWorldAction10010.PacketType, ConditionalEvidence);
        (ushort X, ushort Y)[] targets =
        [
            (62, 110), (59, 109), (64, 109), (60, 111),
            (62, 106), (60, 112), (66, 110), (56, 109),
            (50, 104), (49, 95), (58, 104), (63, 106)
        ];
        Assert.Equal(targets.Length, samples.Length);
        WorldInboundState state = CreateState(heroId: 1_000_000);

        for (int index = 0; index < samples.Length; index++)
        {
            Sample sample = samples[index];
            Assert.Equal((ushort)GameWorldAction10010.GroundJump, sample.Subtype);
            Assert.True(state.Apply(sample.Frame));
            Assert.Equal(targets[index], (state.X, state.Y));
            Assert.Equal((byte)3, state.DirectionIndex);
        }

        Assert.False(state.Apply(samples[^1].Frame));
        Assert.Equal(targets[^1], (state.X, state.Y));
    }

    [Fact]
    public void CapturedGroundJumpRelaysUpdateKnownPeerAndCancelPreviousGait()
    {
        Sample sample = Read("10010-ground-jump.observed.json",
            GameWorldAction10010.PacketType, ConditionalEvidence)[0];
        WorldInboundState state = CreateState(heroId: 1_000_001);
        Assert.False(state.Apply(WorldSpawnPacketFixture.Create(1_000_000, 62, 106, name: "Peer")));

        byte[] walk = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(walk, 1_000_000, 6, movementTick: 100, mapState: 1010);
        Assert.False(state.Apply(walk));
        WorldRolePlayback playback = state.RemoteRoles.GetPlayback(1_000_000)!;
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
        int previousRevision = state.RemoteRoles.Revision;

        Assert.False(state.Apply(sample.Frame));

        Assert.True(state.RemoteRoles.TryGet(1_000_000, out GamePlayerSpawn10014? peer));
        Assert.Equal(((ushort)62, (ushort)110), (peer!.X, peer.Y));
        Assert.Equal("Peer", System.Text.Encoding.ASCII.GetString(peer.NameBytes.Span));
        Assert.Equal(1003u, peer.PackedAppearance);
        Assert.Equal((byte)3, peer.Direction);
        Assert.Same(playback, state.RemoteRoles.GetPlayback(1_000_000));
        Assert.Equal(0, playback.Action);
        Assert.Equal(previousRevision + 1, state.RemoteRoles.Revision);
        Assert.Equal((61, 101, (byte)3), (state.X, state.Y, state.DirectionIndex));

        Assert.False(state.Apply(sample.Frame));
        Assert.Equal(previousRevision + 1, state.RemoteRoles.Revision);

        GameWalk10005.WriteOrdinary(walk, 1_000_000, 6, movementTick: 101, mapState: 1010);
        Assert.False(state.Apply(walk));
        Assert.True(state.RemoteRoles.TryGet(1_000_000, out peer));
        Assert.Equal(((ushort)63, (ushort)110, (byte)6), (peer!.X, peer.Y, peer.Direction));
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
    }

    [Fact]
    public void UnknownGroundJumperRequestsIdentityOnlyOnceBeforeSpawn()
    {
        Sample sample = Read("10010-ground-jump.observed.json",
            GameWorldAction10010.PacketType, ConditionalEvidence)[0];
        List<GameWorldActionResponse> replies = [];
        WorldInboundState state = CreateState(heroId: 1_000_001, onActionReply: replies.Add);

        Assert.False(state.Apply(sample.Frame));
        Assert.False(state.Apply(sample.Frame));
        Assert.Equal([new GameWorldActionResponse(GameWorldAction10010.QueryPlayer, 1_000_001, 1_000_000)], replies);
        Assert.Equal(0, state.RemoteRoles.Count);
        Assert.Equal((61, 101), (state.X, state.Y));

        Assert.False(state.Apply(WorldSpawnPacketFixture.Create(1_000_000, 63, 106, name: "Peer")));
        Assert.False(state.Apply(sample.Frame));
        Assert.True(state.RemoteRoles.TryGet(1_000_000, out GamePlayerSpawn10014? peer));
        Assert.Equal(((ushort)62, (ushort)110), (peer!.X, peer.Y));
        Assert.Single(replies);
    }

    [Fact]
    public void UnsupportedChargeFlagDoesNotAdvanceHeroGroundPosition()
    {
        Sample sample = Read("10010-ground-jump.observed.json",
            GameWorldAction10010.PacketType, ConditionalEvidence)[0];
        byte[] charge = (byte[])sample.Frame.Clone();
        charge[36] = 1;
        WorldInboundState state = CreateState(heroId: 1_000_000);

        Assert.False(state.Apply(charge));
        Assert.Equal((61, 101, (byte)3), (state.X, state.Y, state.DirectionIndex));
        Assert.True(state.Apply(sample.Frame));
        Assert.Equal((62, 110), (state.X, state.Y));
    }
}
