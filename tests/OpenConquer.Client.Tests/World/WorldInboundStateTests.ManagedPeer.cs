using OpenConquer.Client.World;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.World;

public sealed partial class WorldInboundStateTests
{
    [Fact]
    public void CapturedManagedPeerFramesApplySpawnWalkRunAndRemovalInOrder()
    {
        byte[][] frames = ManagedPeerFrames.Load();
        WorldInboundState state = CreateState(heroId: 1_000_000);
        Assert.True(state.Apply(Correction(1_000_000, 60, 108)));

        Assert.False(state.Apply(frames[0]));
        Assert.True(state.RemoteRoles.TryGet(1_000_001, out GamePlayerSpawn10014? spawned));
        Assert.Equal(((ushort)60, (ushort)108), (spawned!.X, spawned.Y));
        Assert.Equal(0, state.RemoteRoles.GetPlayback(1_000_001)!.Action);

        Assert.False(state.Apply(frames[1]));
        Assert.True(state.RemoteRoles.TryGet(1_000_001, out GamePlayerSpawn10014? walked));
        Assert.Equal(((ushort)61, (ushort)108, (byte)6),
            (walked!.X, walked.Y, walked.Direction));
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, state.RemoteRoles.GetPlayback(1_000_001)!.Action);

        Assert.False(state.Apply(frames[2]));
        Assert.True(state.RemoteRoles.TryGet(1_000_001, out GamePlayerSpawn10014? ran));
        Assert.Equal(((ushort)60, (ushort)108, (byte)2), (ran!.X, ran.Y, ran.Direction));
        Assert.Equal(WorldRolePlayback.GroundRunFirst, state.RemoteRoles.GetPlayback(1_000_001)!.Action);

        Assert.False(state.Apply(frames[3]));
        Assert.False(state.RemoteRoles.Contains(1_000_001));
        Assert.Null(state.RemoteRoles.GetPlayback(1_000_001));
        Assert.Equal(0, state.RemoteRoles.Count);
        Assert.Equal((60, 108, (byte)3), (state.X, state.Y, state.DirectionIndex));
    }

}
