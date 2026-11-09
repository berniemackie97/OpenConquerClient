using OpenConquer.Client.World;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldRolePlaybackTests
{
    [Fact]
    public void WalkingSelectsTheGroundPairAndAdvancesAtTheRecordedInterval()
    {
        WorldRolePlayback playback = new();
        playback.BeginMove(GameWalk10005.WalkSelector, 1000);

        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
        Assert.True(playback.TryGetFrame(1000, 3, out int first));
        Assert.Equal(0, first);
        Assert.True(playback.TryGetFrame(1024, 3, out int stillFirst));
        Assert.Equal(0, stillFirst);
        Assert.True(playback.TryGetFrame(1025, 3, out int second));
        Assert.Equal(1, second);
        Assert.True(playback.TryGetFrame(1074, 3, out int last));
        Assert.Equal(2, last);
        Assert.False(playback.TryGetFrame(1075, 3, out _));
        Assert.Equal(0, playback.Action);
    }

    [Fact]
    public void ConsecutiveWalksAlternateTheirPairAndRestartTheFrameClock()
    {
        WorldRolePlayback playback = new();
        playback.BeginMove(GameWalk10005.WalkSelector, 1000);
        Assert.True(playback.TryGetFrame(1050, 8, out int previous));
        Assert.Equal(2, previous);

        playback.BeginMove(GameWalk10005.WalkSelector, 1060);
        Assert.Equal(WorldRolePlayback.GroundWalkSecond, playback.Action);
        Assert.True(playback.TryGetFrame(1060, 8, out int current));
        Assert.Equal(0, current);

        playback.BeginMove(GameWalk10005.WalkSelector, 1070);
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
    }

    [Fact]
    public void RunningSelectsAndAlternatesTheSeparateStridePair()
    {
        WorldRolePlayback playback = new();
        playback.BeginMove(GameWalk10005.RunSelector, 1000);
        Assert.Equal(WorldRolePlayback.GroundRunFirst, playback.Action);
        Assert.True(playback.TryGetFrame(1025, 4, out int frame));
        Assert.Equal(1, frame);

        playback.BeginMove(GameWalk10005.RunSelector, 1030);
        Assert.Equal(WorldRolePlayback.GroundRunSecond, playback.Action);
        Assert.True(playback.TryGetFrame(1030, 4, out frame));
        Assert.Equal(0, frame);

        playback.BeginMove(GameWalk10005.RunSelector, 1040);
        Assert.Equal(WorldRolePlayback.GroundRunFirst, playback.Action);
    }

    [Fact]
    public void SwitchingFromFirstRunStrideSelectsTheSecondWalkStride()
    {
        WorldRolePlayback playback = new();
        playback.BeginMove(GameWalk10005.RunSelector, 1000);
        playback.BeginMove(GameWalk10005.WalkSelector, 1010);
        Assert.Equal(WorldRolePlayback.GroundWalkSecond, playback.Action);

        playback.BeginMove(GameWalk10005.RunSelector, 1020);
        Assert.Equal(WorldRolePlayback.GroundRunFirst, playback.Action);
    }

    [Fact]
    public void ExpiredOrMissingMotionReturnsToIdle()
    {
        WorldRolePlayback playback = new();
        playback.BeginMove(GameWalk10005.WalkSelector, 200);
        Assert.False(playback.TryGetFrame(201, 0, out _));
        Assert.Equal(0, playback.Action);

        playback.BeginMove(GameWalk10005.RunSelector, 300);
        Assert.Equal(WorldRolePlayback.GroundRunFirst, playback.Action);
        Assert.False(playback.TryGetFrame(325, 1, out _));

        playback.BeginMove(GameWalk10005.WalkSelector, 400);
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
    }

    [Fact]
    public void ExactMountedSelectorStopsGroundPlayback()
    {
        WorldRolePlayback playback = new();
        playback.BeginMove(GameWalk10005.WalkSelector, 1000);
        playback.BeginMove(GameWalk10005.MountedSelector, 1001);

        Assert.Equal(0, playback.Action);
        Assert.False(playback.TryGetFrame(1002, 4, out _));
    }

    [Theory]
    [InlineData(0x0000_0100u, (ushort)110)]
    [InlineData(0x0000_0101u, (ushort)120)]
    [InlineData(0x0000_0109u, (ushort)110)]
    [InlineData(2u, (ushort)110)]
    [InlineData(0xFFFF_FF01u, (ushort)120)]
    public void UnmountedSelectorUsesItsLowByteToChooseTheMoveFamily(uint selector, ushort action)
    {
        WorldRolePlayback playback = new();
        playback.BeginMove(selector, 1000);

        Assert.Equal(action, playback.Action);
    }

    [Fact]
    public void SampleTimeHandlesUnsignedCounterRollover()
    {
        WorldRolePlayback playback = new();
        playback.BeginMove(GameWalk10005.WalkSelector, uint.MaxValue - 9);
        Assert.True(playback.TryGetFrame(15, 3, out int frame));
        Assert.Equal(1, frame);
        Assert.False(playback.TryGetFrame(65, 3, out _));
    }
}
