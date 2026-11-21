using OpenConquer.Client.World;
using OpenConquer.Networking.Game.World;
using OpenConquer.Rendering.World;

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

    private static (int X, int Y) Unproject(int x, int y) => WorldJumpArc.ScreenToTile(MapWidth, x, y);

    private static GameWalk10005 Walk(byte direction, uint selector = GameWalk10005.WalkSelector) =>
        new(direction, 1_000_001, selector, 0, 1010);

    private static WorldRolePlayback Placed(uint now = 0)
    {
        WorldRolePlayback playback = new(60, 108, 0);
        playback.Advance(now, Frames, Project, Unproject);
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

        playback.Advance(1000, Frames, Project, Unproject);

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
        playback.Advance(1000, Frames, Project, Unproject);

        playback.Advance(now, Frames, Project, Unproject);

        Assert.Equal((x, y), playback.Cursor);
    }

    [Fact]
    public void ArrivalCommitsTheUnfinishedStepBeforeQueueingTheNext()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(1000, Frames, Project, Unproject);
        playback.Advance(1100, Frames, Project, Unproject);
        Assert.Equal((1670, 2707), playback.Cursor);

        playback.Enqueue(Walk(6));
        Assert.Equal((1696, 2720), playback.Cursor);

        playback.Advance(1110, Frames, Project, Unproject);
        Assert.Equal(WorldRolePlayback.GroundWalkSecond, playback.Action);
        Assert.Equal((1696, 2720), playback.Cursor);
        playback.Advance(1360, Frames, Project, Unproject);
        Assert.Equal((1712, 2728), playback.Cursor);
    }

    [Fact]
    public void QueuedStepsStartOneAtATimeAfterEachCompletedMotion()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Enqueue(Walk(6));
        Assert.Equal(2, playback.PendingCount);

        playback.Advance(1000, Frames, Project, Unproject);
        Assert.Equal(1, playback.PendingCount);
        playback.Advance(1300, Frames, Project, Unproject);
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
        Assert.Equal(1, playback.PendingCount);

        playback.Advance(1488, Frames, Project, Unproject);
        Assert.Equal((1696, 2720), playback.Cursor);
        Assert.Equal(1, playback.PendingCount);

        playback.Advance(1500, Frames, Project, Unproject);
        Assert.Equal(WorldRolePlayback.GroundWalkSecond, playback.Action);
        Assert.Equal(0, playback.PendingCount);
    }

    [Fact]
    public void DrainedQueueStandsAtTheCursor()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(1000, Frames, Project, Unproject);
        playback.Advance(1500, Frames, Project, Unproject);
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);

        playback.Advance(1516, Frames, Project, Unproject);

        Assert.Equal(0, playback.Action);
        Assert.Equal((1696, 2720), playback.Cursor);
        Assert.Equal((61, 108), playback.DepthTile);
        Assert.False(playback.TryGetFrame(Clip, out _));
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
            playback.Advance(now, Frames, Project, Unproject);
            Assert.Equal(expected[index], playback.Action);

            // Finish and stand between steps; standing does not reset the selection source.
            playback.Advance(now + 500, Frames, Project, Unproject);
            playback.Advance(now + 516, Frames, Project, Unproject);
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
        playback.Advance(1000, Frames, Project, Unproject);

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
        playback.Advance(1000, Frames, Project, Unproject);

        Assert.Equal((x, y), playback.DepthTile);
    }

    [Fact]
    public void UnpreparedClipRunsOneFrameOfTheGroundInterval()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(1000, static _ => 0, Project, Unproject);

        // total = 25 ms; 12 ms rounds to frame 0: 32*12/25 = 15.36 -> 15, 16*12/25 = 7.68 -> 8 (0x768AA1).
        playback.Advance(1012, static _ => 0, Project, Unproject);
        Assert.Equal((1679, 2712), playback.Cursor);
        playback.Advance(1013, static _ => 0, Project, Unproject);
        Assert.Equal((1696, 2720), playback.Cursor);
    }

    [Fact]
    public void FrameIsTheRoundedMotionFrameAcrossCounterRollover()
    {
        // Role_TickAndRender pushes rounded +0x1A8 = (elapsed + 12) / 25 (0x64EB0C, 0x64FA33).
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(uint.MaxValue - 9, Frames, Project, Unproject);
        Assert.True(playback.TryGetFrame(Clip, out int first));
        Assert.Equal(0, first);

        playback.Advance(2, Frames, Project, Unproject);
        Assert.True(playback.TryGetFrame(Clip, out int stillFirst));
        Assert.Equal(0, stillFirst);

        playback.Advance(3, Frames, Project, Unproject);
        Assert.True(playback.TryGetFrame(Clip, out int second));
        Assert.Equal(1, second);

        playback.Advance(470, Frames, Project, Unproject);
        Assert.True(playback.TryGetFrame(Clip, out int last));
        Assert.Equal(Clip - 1, last);
    }

    [Fact]
    public void StandbyStartsAtFrameZeroAndRestartsWhenTheMotionCompletes()
    {
        // Role_CommandType05 dispatches 100 from frame 0; 66 ms interval, rounded frame, re-dispatch on completion.
        WorldRolePlayback playback = Placed(1000);
        Assert.Equal(0, playback.StandbyFrame);

        playback.Advance(1032, Frames, Project, Unproject);
        Assert.Equal(0, playback.StandbyFrame);
        playback.Advance(1033, Frames, Project, Unproject);
        Assert.Equal(1, playback.StandbyFrame);

        // (1287 + 33) / 66 = 20 = clip: complete, held at the last frame, restarted on the next tick.
        playback.Advance(2287, Frames, Project, Unproject);
        Assert.Equal(Clip - 1, playback.StandbyFrame);
        playback.Advance(2300, Frames, Project, Unproject);
        Assert.Equal(0, playback.StandbyFrame);
        playback.Advance(2333, Frames, Project, Unproject);
        Assert.Equal(1, playback.StandbyFrame);
    }

    [Fact]
    public void StandbyRestartsFromFrameZeroWhenAStepEnds()
    {
        WorldRolePlayback playback = Placed();
        playback.Advance(700, Frames, Project, Unproject);
        Assert.NotEqual(0, playback.StandbyFrame);

        playback.Enqueue(Walk(6));
        playback.Advance(1000, Frames, Project, Unproject);
        playback.Advance(1500, Frames, Project, Unproject);
        playback.Advance(1516, Frames, Project, Unproject);

        Assert.Equal(0, playback.Action);
        Assert.Equal(0, playback.StandbyFrame);
        playback.Advance(1549, Frames, Project, Unproject);
        Assert.Equal(1, playback.StandbyFrame);
    }

    [Fact]
    public void MountedStepMovesTheTileWithoutAStride()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Enqueue(new GameWalk10005(0x17, 1_000_001, GameWalk10005.MountedSelector, 0, 1010));

        Assert.Equal(0, playback.PendingCount);
        Assert.False(playback.IsProjected);
        playback.Advance(1000, Frames, Project, Unproject);
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
        playback.Advance(1000, Frames, Project, Unproject);
        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
        Assert.Equal((byte)0, playback.Direction);
        Assert.Equal(Project(60 + WorldRolePlayback.MaximumPendingSteps, 108), playback.Cursor);
    }

    [Fact]
    public void MoveToDropsQueuedStepsAndPlacesTheRole()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(1000, Frames, Project, Unproject);
        playback.Enqueue(Walk(6));

        playback.MoveTo(62, 110, 6);

        Assert.Equal(0, playback.Action);
        Assert.Equal(0, playback.PendingCount);
        playback.Advance(1010, Frames, Project, Unproject);
        Assert.Equal(Project(62, 110), playback.Cursor);
        Assert.Equal(0, playback.Action);
    }

    [Fact]
    public void LinearOffsetsUseTheNativeHalfStepRoundingInBothDirections()
    {
        // (60,108) -> (59,108): delta (-32,-16). At 100 ms: -6.4 -> -5 and -3.2 -> -2 (0x768AA1).
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(2));
        playback.Advance(1000, Frames, Project, Unproject);

        playback.Advance(1100, Frames, Project, Unproject);

        Assert.Equal((1664 - 5, 2704 - 2), playback.Cursor);
    }

    [Fact]
    public void JumpStagesActionDirectionDistanceIntervalAndTarget()
    {
        WorldRolePlayback playback = Placed();
        playback.EnqueueJump(62, 110);
        Assert.Equal(0, playback.Action);

        playback.Advance(1000, Frames, Project, Unproject);

        // Distance 64 px: trunc(33 * 64 / 300 + 1) = 8, floored to 33; 20 frames need no stretch.
        Assert.Equal(WorldRolePlayback.GroundJump, playback.Action);
        Assert.Equal((byte)7, playback.Direction);
        Assert.Equal(33u, playback.FrameInterval);
        Assert.Equal(((1664, 2704), (1664, 2768)), playback.Endpoints);
        Assert.Equal((60, 108), playback.DepthTile);
    }

    [Theory]
    [InlineData(1330u, 1664, 2736, 61, 109)]
    [InlineData(1643u, 1664, 2766, 62, 110)]
    [InlineData(1644u, 1664, 2768, 62, 110)]
    public void JumpSlidesLinearlyAndDepthFollowsTheCursorTile(uint now, int x, int y, int tileX, int tileY)
    {
        WorldRolePlayback playback = Placed();
        playback.EnqueueJump(62, 110);
        playback.Advance(1000, Frames, Project, Unproject);

        playback.Advance(now, Frames, Project, Unproject);

        Assert.Equal((x, y), playback.Cursor);
        Assert.Equal((tileX, tileY), playback.DepthTile);
    }

    [Theory]
    [InlineData(10, 68u)]
    [InlineData(19, 35u)]
    [InlineData(20, 33u)]
    [InlineData(25, 33u)]
    public void ShortJumpClipsStretchToTheTwentyFrameDuration(int clip, uint interval)
    {
        // ceil(33 * 19.5 / (clip - 0.5)) when clip < 20 (0x64E7A1..0x64E7E2).
        WorldRolePlayback playback = Placed();
        playback.EnqueueJump(62, 110);

        playback.Advance(1000, _ => clip, Project, Unproject);

        Assert.Equal(interval, playback.FrameInterval);
    }

    [Fact]
    public void LongJumpsUseTheDistanceInterval()
    {
        // (60,108) -> (60,124): 16 tiles, delta (-512,+256), distance 572.43: trunc(33 * 572.43 / 300 + 1) = 63.
        WorldRolePlayback playback = Placed();
        playback.EnqueueJump(60, 124);

        playback.Advance(1000, Frames, Project, Unproject);

        Assert.Equal(63u, playback.FrameInterval);
        Assert.Equal((byte)0, playback.Direction);
    }

    [Fact]
    public void JumpPoseUsesPathFacingAndTheArcAnchoredRoleViewTransform()
    {
        WorldRolePlayback playback = Placed();
        playback.EnqueueJump(62, 110);
        playback.Advance(1000, Frames, Project, Unproject);
        playback.Advance(1330, Frames, Project, Unproject);

        (int x, int y, float rotation, float elevation) = playback.GetPose(
            static (_, _, _) => 50, static (_, _) => 10);

        // terrainDelta = 10 - 50 = -40; y = 2736 + 40 - 50; elevation 2 * -40; facing -270 - trunc(90).
        Assert.Equal((1664, 2726, -360f, -80f), (x, y, rotation, elevation));
    }

    [Fact]
    public void StandingPoseUsesTheLatchedDirectionWithoutElevation()
    {
        WorldRolePlayback playback = Placed();
        playback.EnqueueJump(62, 110);
        playback.Advance(1000, Frames, Project, Unproject);
        playback.Advance(1700, Frames, Project, Unproject);
        playback.Advance(1716, Frames, Project, Unproject);

        (int x, int y, float rotation, float elevation) = playback.GetPose(
            static (_, _, _) => throw new InvalidOperationException("No arc while standing."), static (_, _) => 99);

        Assert.Equal(0, playback.Action);
        Assert.Equal((1664, 2768, (-1 - 7) * 45f, 0f), (x, y, rotation, elevation));
    }

    [Fact]
    public void WalkAfterAJumpStartsTheFirstStrideBecauseTheJumpIsTheLastPhysicalAction()
    {
        WorldRolePlayback playback = Placed();
        playback.Enqueue(Walk(6));
        playback.Advance(1000, Frames, Project, Unproject);
        playback.EnqueueJump(63, 110);
        playback.Advance(1010, Frames, Project, Unproject);
        Assert.Equal(WorldRolePlayback.GroundJump, playback.Action);

        playback.Enqueue(Walk(6));
        playback.Advance(1020, Frames, Project, Unproject);

        Assert.Equal(WorldRolePlayback.GroundWalkFirst, playback.Action);
    }

    [Theory]
    [InlineData(2, 2, 7)]
    [InlineData(-1, 4, 0)]
    [InlineData(1, 0, 6)]
    [InlineData(0, -3, 4)]
    [InlineData(-2, -1, 3)]
    [InlineData(-5, 1, 2)]
    [InlineData(0, 0, 4)]
    public void JumpDirectionMatchesTheNativeEightWaySelector(int dx, int dy, byte direction) =>
        Assert.Equal(direction, WorldRolePlayback.SelectDirectionTowardTile(dx, dy));

    [Theory]
    [InlineData(new[] { 1, 0 }, (ushort)101)]
    [InlineData(new[] { 1, 1 }, (ushort)102)]
    [InlineData(new[] { 1, 2 }, (ushort)103)]
    [InlineData(new[] { 0 }, (ushort)100)]
    [InlineData(new[] { 9 }, (ushort)100)]
    public void NeverCommandedRolesMayPickAnIdleVariant(int[] rolls, ushort action)
    {
        // Role_CommandType05: command type 5 and rand() % 10 == 1 -> 101 + rand() % 3 (0x73352A..0x733565).
        Queue<int> sequence = new(rolls);
        List<int> moduli = [];
        WorldRolePlayback playback = new(60, 108, 0, modulus =>
        {
            moduli.Add(modulus);
            return sequence.Dequeue();
        });

        playback.Advance(1000, Frames, Project, Unproject);

        Assert.Equal(action, playback.StandbyAction);
        Assert.Equal(rolls.Length == 2 ? [10, 3] : [10], moduli);
    }

    [Fact]
    public void RolesThatReceivedACommandStandWithoutVariants()
    {
        int rolls = 0;
        WorldRolePlayback playback = new(60, 108, 0, _ =>
        {
            rolls++;
            return 1;
        });
        playback.Enqueue(Walk(6));
        playback.Advance(1000, Frames, Project, Unproject);
        playback.Advance(1500, Frames, Project, Unproject);

        playback.Advance(1516, Frames, Project, Unproject);

        Assert.Equal(0, playback.Action);
        Assert.Equal(WorldRolePlayback.Standby, playback.StandbyAction);
        Assert.Equal(0, rolls);
    }

    [Fact]
    public void EachStandbyRedispatchRollsAgainWhileUncommanded()
    {
        Queue<int> sequence = new([1, 0, 0]);
        WorldRolePlayback playback = new(60, 108, 0, _ => sequence.Dequeue());
        playback.Advance(1000, Frames, Project, Unproject);
        Assert.Equal((ushort)101, playback.StandbyAction);

        playback.Advance(2287, Frames, Project, Unproject);
        playback.Advance(2300, Frames, Project, Unproject);

        Assert.Equal(WorldRolePlayback.Standby, playback.StandbyAction);
        Assert.Empty(sequence);
    }
}
