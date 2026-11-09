using OpenConquer.Client.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldRolePlaybackTests
{
    [Fact]
    public void WalkSelectsActionAndAdvancesAtTheRecordedGroundFrameInterval()
    {
        WorldRolePlayback playback = new();
        playback.BeginWalk(1000);

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
    public void ConsecutiveWalksAlternateActionsAndRestartTheirFrameClock()
    {
        WorldRolePlayback playback = new();
        playback.BeginWalk(1000);
        Assert.True(playback.TryGetFrame(1050, 8, out int previous));
        Assert.Equal(2, previous);

        playback.BeginWalk(1060);
        Assert.Equal(WorldRolePlayback.GroundWalkSecond, playback.Action);
        Assert.True(playback.TryGetFrame(1060, 8, out int current));
        Assert.Equal(0, current);
        playback.BeginWalk(1070);
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
    }

    [Fact]
    public void ExpiredOrAbsentMotionReturnsToIdleAndResetsAlternation()
    {
        WorldRolePlayback playback = new();
        playback.BeginWalk(200);
        Assert.False(playback.TryGetFrame(201, 0, out _));
        Assert.Equal(0, playback.Action);

        playback.BeginWalk(300);
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
        Assert.False(playback.TryGetFrame(325, 1, out _));
        playback.BeginWalk(400);
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
    }

    [Fact]
    public void MountedMovementClearsGroundPlayback()
    {
        WorldRolePlayback playback = new();
        playback.BeginWalk(1000);
        playback.Stop();

        Assert.Equal(0, playback.Action);
        Assert.False(playback.TryGetFrame(1001, 4, out _));
    }

    [Fact]
    public void SampleTimeHandlesUnsignedCounterRollo()
    {
        WorldRolePlayback playback = new();
        playback.BeginWalk(uint.MaxValue - 9);
        Assert.True(playback.TryGetFrame(15, 3, out int frame));
        Assert.Equal(1, frame);
        Assert.False(playback.TryGetFrame(65, 3, out _));
    }
}
