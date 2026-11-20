using OpenConquer.Client.World;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.World;

/// <summary>
/// Remote ground-step presentation against the 5517 contract in 20_notes
/// Network/15_msgwalk_10005_direction_index_order.md, "Remote step presentation".
/// </summary>
public sealed class WorldRolePlaybackTests
{
    private const int MapWidth = 100;
    private const int Clip = 20;

    // Map_ProjectTileToScreenCoordinates (0x61D85C) for a 100-cell-wide map.
    private static (int X, int Y) Project(int tileX, int tileY) =>
        (((tileX - tileY) << 6) / 2 + MapWidth * 32, ((tileX + tileY) << 5) / 2 + 16);

    private static int Frames(ushort action) => Clip;

    private static GameWalk10005 Walk(byte direction, uint selector = GameWalk10005.WalkSelector) =>
        new(direction, 1_000_001, selector, 0, 1010);

    private static WorldRolePlayback Placed(uint now = 0)
    {
        WorldRolePlayback playback = new(60, 108, 0);
        playback.Advance(now, Frames, Project);
        return playback;
    }

    [Fact]
    public void ArrivalQueuesTheStepAndTheNextTickStagesItFromTheCursor()
    {
        WorldRolePlayback playback = Placed();
        Assert.Equal((1664, 2704), playback.Cursor);

        playback.Enqueue(Walk(6));
        Assert.Equal(0, playback.Action);
        Assert.Equal(1, playback.PendingCount);

        playback.Advance(1000, Frames, Project);

        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
        Assert.Equal((byte)6, playback.Direction);
        Assert.Equal(0, playback.PendingCount);
        Assert.Equal((1664, 2704), playback.Cursor);
    }

    [Theory]
    [InlineData(1000u, 1664, 2704)]
    [InlineData(1100u, 1670, 2707)]
    [InlineData(1250u, 1680, 2712)]
    [InlineData(1487u, 1695, 2720)]
    [InlineData(1488u, 1696, 2720)]
    [InlineData(1600u, 1696, 2720)]
    public void GroundStepInterpolatesLinearlyAndSnapsWhenTheRoundedFrameReachesTheClip(
        uint now, int x, int y)
    {
        // 20 frames x 25 ms = 500 ms from (60,108) to (61,108): delta (+32,+16).
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(1000, Frames, Project);

        playback.Advance(now, Frames, Project);

        Assert.Equal((x, y), playback.Cursor);
    }

    [Fact]
    public void ArrivalCommitsTheUnfinishedStepBeforeQueueingTheNext()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(1000, Frames, Project);
        playback.Advance(1100, Frames, Project);
        Assert.Equal((1670, 2707), playback.Cursor);

        playback.Enqueue(Walk(6));
        Assert.Equal((1696, 2720), playback.Cursor);

        playback.Advance(1110, Frames, Project);
        Assert.Equal(WorldRolePlayback.GroundWalkSecond, playback.Action);
        Assert.Equal((1696, 2720), playback.Cursor);
        playback.Advance(1360, Frames, Project);
        Assert.Equal((1712, 2728), playback.Cursor);
    }

    [Fact]
    public void QueuedStepsStartOneAtATimeAfterEachCompletedMotion()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Enqueue(Walk(6));
        Assert.Equal(2, playback.PendingCount);

        playback.Advance(1000, Frames, Project);
        Assert.Equal(1, playback.PendingCount);
        playback.Advance(1300, Frames, Project);
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
        Assert.Equal(1, playback.PendingCount);

        playback.Advance(1488, Frames, Project);
        Assert.Equal((1696, 2720), playback.Cursor);
        Assert.Equal(1, playback.PendingCount);

        playback.Advance(1500, Frames, Project);
        Assert.Equal(WorldRolePlayback.GroundWalkSecond, playback.Action);
        Assert.Equal(0, playback.PendingCount);
    }

    [Fact]
    public void DrainedQueueStandsAtTheCursor()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(1000, Frames, Project);
        playback.Advance(1500, Frames, Project);
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);

        playback.Advance(1516, Frames, Project);

        Assert.Equal(0, playback.Action);
        Assert.Equal((1696, 2720), playback.Cursor);
        Assert.Equal((61, 108), playback.DepthTile);
        Assert.False(playback.TryGetFrame(1516, Clip, out _));
    }

    [Fact]
    public void WalkAndRunSelectFromTheLastPhysicalActionAcrossStanding()
    {
        // 0x736329: last in {110,120} -> 111 else 110. 0x736AEC: last 120 -> 121, else 120.
        ushort[] expected =
        [
            WorldRolePlayback.GroundWalkFirst, WorldRolePlayback.GroundWalkSecond, WorldRolePlayback.GroundRunFirst,
            WorldRolePlayback.GroundRunSecond, WorldRolePlayback.GroundRunFirst, WorldRolePlayback.GroundWalkSecond,
            WorldRolePlayback.GroundWalkFirst
        ];
        uint[] selectors =
        [
            GameWalk10005.WalkSelector, GameWalk10005.WalkSelector, GameWalk10005.RunSelector,
            GameWalk10005.RunSelector, GameWalk10005.RunSelector, GameWalk10005.WalkSelector,
            GameWalk10005.WalkSelector
        ];
        WorldRolePlayback playback = Placed();
        uint now = 1000;
        for (int index = 0; index < selectors.Length; index++)
        {
            playback.Enqueue(Walk(6, selectors[index]));
            playback.Advance(now, Frames, Project);
            Assert.Equal(expected[index], playback.Action);

            // Finish and stand between steps; standing does not reset the selection source.
            playback.Advance(now + 500, Frames, Project);
            playback.Advance(now + 516, Frames, Project);
            Assert.Equal(0, playback.Action);
            now += 1000;
        }
    }

    [Theory]
    [InlineData(0x0000_0100u, (ushort)110)]
    [InlineData(0x0000_0101u, (ushort)120)]
    [InlineData(2u, (ushort)110)]
    [InlineData(0xFFFF_FF01u, (ushort)120)]
    public void UnmountedSelectorUsesItsLowByteToChooseTheMoveFamily(uint selector, ushort action)
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6, selector));
        playback.Advance(1000, Frames, Project);

        Assert.Equal(action, playback.Action);
    }

    [Theory]
    [InlineData(0, 60, 108)]
    [InlineData(3, 60, 108)]
    [InlineData(4, 60, 107)]
    [InlineData(7, 61, 109)]
    public void MidStepDepthUsesTheStartTileUpToDirectionThreeAndTheEndTileAfter(
        byte direction, int x, int y)
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(direction));
        playback.Advance(1000, Frames, Project);

        Assert.Equal((x, y), playback.DepthTile);
    }

    [Fact]
    public void UnpreparedClipRunsOneFrameOfTheGroundInterval()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(1000, static _ => 0, Project);

        // total = 25 ms; 12 ms rounds to frame 0: 32*12/25 = 15.36 -> 15, 16*12/25 = 7.68 -> 8 (0x768AA1).
        playback.Advance(1012, static _ => 0, Project);
        Assert.Equal((1679, 2712), playback.Cursor);
        playback.Advance(1013, static _ => 0, Project);
        Assert.Equal((1696, 2720), playback.Cursor);
    }

    [Fact]
    public void LinearOffsetsUseTheNativeHalfStepRoundingInBothDirections()
    {
        // (60,108) -> (59,108): delta (-32,-16). At 100 ms: -6.4 -> -5 and -3.2 -> -2 (0x768AA1).
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(2));
        playback.Advance(1000, Frames, Project);

        playback.Advance(1100, Frames, Project);

        Assert.Equal((1664 - 5, 2704 - 2), playback.Cursor);
    }

    [Fact]
    public void FrameSampleUsesTheGroundIntervalAndHandlesCounterRollover()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(uint.MaxValue - 9, Frames, Project);

        Assert.True(playback.TryGetFrame(uint.MaxValue - 9, Clip, out int first));
        Assert.Equal(0, first);
        Assert.True(playback.TryGetFrame(15, Clip, out int second));
        Assert.Equal(1, second);
        Assert.True(playback.TryGetFrame(2000, Clip, out int last));
        Assert.Equal(Clip - 1, last);
    }

    [Fact]
    public void MountedStepMovesTheTileWithoutAStride()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Enqueue(new GameWalk10005(0x17, 1_000_001, GameWalk10005.MountedSelector, 0, 1010));

        Assert.Equal(0, playback.PendingCount);
        Assert.False(playback.IsProjected);
        playback.Advance(1000, Frames, Project);
        Assert.Equal(0, playback.Action);
        Assert.Equal((62, 110), playback.DepthTile);
        Assert.Equal(Project(62, 110), playback.Cursor);
    }

    [Fact]
    public void BacklogBeyondTheBoundIsCommittedBeforeQueueing()
    {
        WorldRolePlayback playback = Placed();
        for (int index = 0; index < WorldRolePlayback.MaximumPendingSteps; index++)
        {
            playback.Enqueue(Walk(6));
        }

        playback.Enqueue(Walk(0));

        Assert.Equal(1, playback.PendingCount);
        playback.Advance(1000, Frames, Project);
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
        Assert.Equal((byte)0, playback.Direction);
        Assert.Equal(Project(60 + WorldRolePlayback.MaximumPendingSteps, 108), playback.Cursor);
    }

    [Fact]
    public void MoveToDropsQueuedStepsAndPlacesTheRole()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(1000, Frames, Project);
        playback.Enqueue(Walk(6));

        playback.MoveTo(62, 110, 6);

        Assert.Equal(0, playback.Action);
        Assert.Equal(0, playback.PendingCount);
        playback.Advance(1010, Frames, Project);
        Assert.Equal(Project(62, 110), playback.Cursor);
        Assert.Equal(0, playback.Action);
    }
}
