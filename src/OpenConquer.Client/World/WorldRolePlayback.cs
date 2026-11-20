using OpenConquer.Networking.Game.World;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.World;

/// <summary>
/// One remote role's step presentation: the queued 10005 walk/run steps and MsgAction 137 jumps, the active
/// step's projected start and end, and the interpolated screen cursor.
/// </summary>
/// <remarks>
/// Native anchors (5517 <c>Conquer.exe</c>): ingress <c>Role_HandleIncomingActionStateBatch</c>
/// (0x6720EE, label 0x672276) commits the step in progress and queues the new one; the tick
/// <c>Role_TickCommandStateAndDispatchPlayback</c> (0x672662..0x6726E0) starts one queued step when the
/// current motion is complete; <c>Role_ApplyGroundMoveActionAndPoseFromPathCursor</c> (0x73631C) and
/// <c>Role_CommandType14_PrepareJumpFlagZeroPoseAndComplete</c> (0x73597B) stage start = cursor and
/// end = projected destination tile; <c>Role_AdvanceMotionInterpolationAndRefreshPosition</c> (0x64EAB2)
/// interpolates linearly because no ActionCtrl.ini record matches actions 110/111/120/121/130. See 20_notes
/// Network/15_msgwalk_10005_direction_index_order.md, "Remote step presentation" and "Remote jump presentation".
/// </remarks>
internal sealed class WorldRolePlayback
{
    internal const ushort GroundWalkFirst = 110;
    internal const ushort GroundWalkSecond = 111;
    internal const ushort GroundRunFirst = 120;
    internal const ushort GroundRunSecond = 121;
    internal const ushort GroundJump = 130;

    /// <summary>Standby action dispatched by Role_CommandType05 (0x73351E).</summary>
    internal const ushort Standby = 100;

    /// <summary>Role_ComputeFrameIntervalFromAction fallback interval for actions 100..103.</summary>
    internal const uint StandbyFrameIntervalMilliseconds = 66;

    /// <summary>Role_ComputeFrameIntervalFromAction base interval for actions 110/111/120/121.</summary>
    internal const uint GroundFrameIntervalMilliseconds = 25;

    /// <summary>Role_ComputeFrameIntervalFromAction fallback base (and minimum) for the jump action.</summary>
    internal const uint JumpBaseFrameIntervalMilliseconds = 33;

    /// <summary>Unmounted jump frame cap (0x64E6F8); shorter clips are stretched to this duration.</summary>
    internal const int JumpFrameCap = 20;

    /// <summary>
    /// Modern bound on queued steps. The native deque is unbounded; a deeper backlog is committed at
    /// once so a flooding peer cannot grow client memory.
    /// </summary>
    internal const int MaximumPendingSteps = 64;

    private readonly Queue<PendingStep> _pending = new();
    private int _tileX;
    private int _tileY;
    private int _startTileX;
    private int _startTileY;
    private int _endTileX;
    private int _endTileY;
    private int _startX;
    private int _startY;
    private int _endX;
    private int _endY;
    private int _cursorX;
    private int _cursorY;
    private bool _projected;
    private ushort _lastPhysicalAction;
    private uint _startedAt;
    private uint _interval = GroundFrameIntervalMilliseconds;
    private uint _frameCount;
    private uint _frame;
    private int _cursorTileX;
    private int _cursorTileY;
    private bool _standbyActive;
    private uint _standbyStartedAt;
    private uint _standbyFrameCount;
    private uint _standbyFrame;

    internal WorldRolePlayback(int tileX, int tileY, byte direction)
    {
        _tileX = _startTileX = _endTileX = tileX;
        _tileY = _startTileY = _endTileY = tileY;
        Direction = direction;
    }

    /// <summary>Current presented action, or 0 while standing.</summary>
    internal ushort Action
    {
        get; private set;
    }

    internal byte Direction
    {
        get; private set;
    }

    internal int PendingCount => _pending.Count;

    /// <summary>Frame interval of the current step in milliseconds.</summary>
    internal uint FrameInterval => _interval;

    /// <summary>False until the first <see cref="Advance"/> projects the starting tile.</summary>
    internal bool IsProjected => _projected;

    /// <summary>Interpolated world-pixel position (native CRole+0x350/+0x354).</summary>
    internal (int X, int Y) Cursor => (_cursorX, _cursorY);

    /// <summary>Projected start and end of the current step (native CRole+0x194..+0x1A0).</summary>
    internal ((int X, int Y) Start, (int X, int Y) End) Endpoints => ((_startX, _startY), (_endX, _endY));

    /// <summary>
    /// Tile used for depth ordering: mid-step for actions 110/111/120/121 the start tile when the direction
    /// index is at most 3, otherwise the end tile (CNpc tick 0x672784); during a jump the tile under the
    /// cursor, which Advance re-derives every tick (0x64EEFD); the committed tile while standing.
    /// </summary>
    internal (int X, int Y) DepthTile => Action switch
    {
        GroundWalkFirst or GroundWalkSecond or GroundRunFirst or GroundRunSecond =>
            Direction <= 3 ? (_startTileX, _startTileY) : (_endTileX, _endTileY),
        GroundJump => (_cursorTileX, _cursorTileY),
        _ => (_tileX, _tileY),
    };

    /// <summary>Queues a relayed ground step after committing the step in progress (0x672276).</summary>
    internal void Enqueue(GameWalk10005 walk)
    {
        CommitActiveStep();
        (int dx, int dy) = walk.GetDelta();
        if (walk.IsMounted)
        {
            // Mounted command 0x18 presentation is not reconstructed; keep the tile without a stride.
            FlushPending();
            MoveTo(unchecked(_tileX + dx), unchecked(_tileY + dy), (byte)(walk.DirectionIndex % 8));
            return;
        }

        Push(new PendingStep((byte)walk.Selector == GameWalk10005.RunSelector ? StepKind.Run : StepKind.Walk,
            (byte)(walk.DirectionIndex % 8), dx, dy, 0, 0));
    }

    /// <summary>Queues a relayed ground jump (MsgAction 137, command 0x14) after committing the step in progress.</summary>
    internal void EnqueueJump(int targetX, int targetY)
    {
        CommitActiveStep();
        Push(new PendingStep(StepKind.Jump, 0, 0, 0, targetX, targetY));
    }

    /// <summary>
    /// Drops queued steps and places the role on a tile without a stride. No action is staged, so the last
    /// physical action used for walk/run selection is unchanged.
    /// </summary>
    internal void MoveTo(int tileX, int tileY, byte direction)
    {
        _pending.Clear();
        Action = 0;
        _standbyActive = false;
        _tileX = _startTileX = _endTileX = tileX;
        _tileY = _startTileY = _endTileY = tileY;
        Direction = direction;
        _projected = false;
    }

    /// <summary>
    /// One role tick: interpolate the running step, start the next queued step once the current one is
    /// complete, or stand.
    /// </summary>
    internal void Advance(uint nowMilliseconds, Func<ushort, int> frameCount, Func<int, int, (int X, int Y)> project,
        Func<int, int, (int X, int Y)> unproject)
    {
        ArgumentNullException.ThrowIfNull(frameCount);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(unproject);
        if (!_projected)
        {
            (_cursorX, _cursorY) = project(_tileX, _tileY);
            (_startX, _startY) = (_cursorX, _cursorY);
            (_endX, _endY) = (_cursorX, _cursorY);
            _projected = true;
        }

        if (Action != 0 && _frame < _frameCount)
        {
            Interpolate(nowMilliseconds);
            (_cursorTileX, _cursorTileY) = unproject(_cursorX, _cursorY);
            return;
        }

        if (_pending.TryDequeue(out PendingStep step))
        {
            Stage(step, nowMilliseconds, frameCount, project);
            return;
        }

        if (Action != 0)
        {
            // Role_CommandType05 (0x7335BA..0x7335D6): standby with both endpoints at the cursor.
            Action = 0;
            (_startX, _startY) = (_cursorX, _cursorY);
            (_endX, _endY) = (_cursorX, _cursorY);
            (_startTileX, _startTileY) = (_tileX, _tileY);
            (_endTileX, _endTileY) = (_tileX, _tileY);
            _standbyActive = false;
        }

        AdvanceStandby(nowMilliseconds, frameCount);
    }

    /// <summary>
    /// Standby frame while standing. Native re-dispatches action 100 from frame 0 each time the previous
    /// standby motion completes (vtable+0x58 returns 1, +0x84 selects Role_CommandType05), with the same
    /// rounded frame index as other motions (0x64EB0C).
    /// </summary>
    internal int StandbyFrame => (int)Math.Min(_standbyFrame, Math.Max(_standbyFrameCount, 1) - 1);

    private void AdvanceStandby(uint now, Func<ushort, int> frameCount)
    {
        if (!_standbyActive || _standbyFrame >= _standbyFrameCount)
        {
            _standbyActive = true;
            _standbyStartedAt = now;
            _standbyFrameCount = (uint)Math.Max(frameCount(Standby), 1);
            _standbyFrame = 0;
            return;
        }

        uint elapsed = unchecked(now - _standbyStartedAt);
        _standbyFrame = Math.Min(
            unchecked(elapsed + (StandbyFrameIntervalMilliseconds / 2)) / StandbyFrameIntervalMilliseconds,
            _standbyFrameCount);
    }

    /// <summary>Rounded, capped animation frame of the current action (native CRole+0x1A8).</summary>
    internal bool TryGetFrame(uint clipFrames, out int frame)
    {
        frame = 0;
        if (Action == 0 || clipFrames == 0)
        {
            return false;
        }

        // Role_TickAndRender pushes the rounded, capped +0x1A8 (0x64FA33..0x64FA4B); the C3 sampler clamps.
        frame = (int)Math.Min(_frame, clipFrames - 1);
        return true;
    }

    /// <summary>
    /// RoleView pose (0x64F6BB..0x64F854): anchor, rotation in degrees and stored elevation. For the jump
    /// action the projected-path facing and terrain-aware arc apply; otherwise the latched direction.
    /// </summary>
    internal (int X, int Y, float RotationDegrees, float Elevation) GetPose(
        Func<(int X, int Y), (int X, int Y), (int X, int Y), int> arcHeight, Func<int, int, int> adjustedElevation)
    {
        ArgumentNullException.ThrowIfNull(arcHeight);
        ArgumentNullException.ThrowIfNull(adjustedElevation);
        if (Action != GroundJump)
        {
            return (_cursorX, _cursorY, (-1 - Direction) * 45f, 0f);
        }

        int arc = arcHeight((_startX, _startY), (_endX, _endY), (_cursorX, _cursorY));
        int terrainDelta = adjustedElevation(_cursorX, _cursorY) - arc;
        double radians = Math.Atan2(_endY - _startY, _endX - _startX);
        int facing = -270 - (int)(radians * 57.29578049044297);

        // RoleView+0x10 y = projY - terrainDelta - arc; non-project camera stores 2 * terrainDelta (0x1000329A).
        return (_cursorX, _cursorY - terrainDelta - arc, facing, 2f * terrainDelta);
    }

    /// <summary>Role_SelectDirectionIndexTowardTile (0x6E4ACC) over Direction_Select8WayFromDelta (0x61E478).</summary>
    internal static byte SelectDirectionTowardTile(int deltaX, int deltaY)
    {
        if (deltaX == 0)
        {
            return deltaY > 0 ? (byte)0 : (byte)4;
        }

        if (deltaY == 0)
        {
            return deltaX > 0 ? (byte)6 : (byte)2;
        }

        int magnitude = Math.Abs(deltaX);
        int slope = unchecked(magnitude / deltaX * deltaY * 100);
        ReadOnlySpan<int> bounds = [-241, -41, 41, 241];
        for (int bucket = 0; bucket < 3; bucket++)
        {
            if (slope >= unchecked(bounds[bucket] * magnitude) && slope < unchecked(bounds[bucket + 1] * magnitude))
            {
                return (byte)(deltaX > 0 ? 5 + bucket : 1 + bucket);
            }
        }

        return deltaY > 0 ? (byte)0 : (byte)4;
    }

    private void Push(PendingStep step)
    {
        if (_pending.Count >= MaximumPendingSteps)
        {
            FlushPending();
        }

        _pending.Enqueue(step);
    }

    private void Stage(PendingStep step, uint now, Func<ushort, int> frameCount, Func<int, int, (int X, int Y)> project)
    {
        (_startTileX, _startTileY) = (_tileX, _tileY);
        (_startX, _startY) = (_cursorX, _cursorY);
        if (step.Kind == StepKind.Jump)
        {
            // Role_CommandType14 (0x73597B): action 0x82, direction toward the target, projected target end.
            Action = GroundJump;
            Direction = SelectDirectionTowardTile(unchecked(step.TargetX - _tileX), unchecked(step.TargetY - _tileY));
            (_endTileX, _endTileY) = (step.TargetX, step.TargetY);
        }
        else
        {
            // Role_CommandType16 (0x736329) and Role_CommandType17 (0x736AEC) select from the last physical action.
            Action = step.Kind == StepKind.Run
                ? _lastPhysicalAction == GroundRunFirst ? GroundRunSecond : GroundRunFirst
                : _lastPhysicalAction is GroundWalkFirst or GroundRunFirst ? GroundWalkSecond : GroundWalkFirst;
            Direction = step.Direction;
            (_endTileX, _endTileY) = (unchecked(_tileX + step.DeltaX), unchecked(_tileY + step.DeltaY));
        }

        _lastPhysicalAction = Action;
        (_endX, _endY) = project(_endTileX, _endTileY);
        _frameCount = (uint)Math.Max(frameCount(Action), 1);
        _interval = Action == GroundJump ? JumpInterval() : GroundFrameIntervalMilliseconds;
        _frame = 0;
        _startedAt = now;
        (_cursorTileX, _cursorTileY) = (_tileX, _tileY);
    }

    private uint JumpInterval()
    {
        // Role_ComputeFrameIntervalFromAction (0x64DEF4..0x64DF50): distance over the projected endpoints.
        int spanX = unchecked(_startX - _endX);
        int spanY = unchecked(_startY - _endY);
        double distance = Math.Sqrt(unchecked(spanX * spanX + spanY * spanY));
        uint interval = unchecked((uint)(int)(JumpBaseFrameIntervalMilliseconds * distance * (1.0 / 300) + 1.0));
        if (interval < JumpBaseFrameIntervalMilliseconds)
        {
            interval = JumpBaseFrameIntervalMilliseconds;
        }

        // 0x64E727..0x64E7E2: a clip shorter than the cap lasts as long as a cap-frame clip.
        if (_frameCount < JumpFrameCap)
        {
            interval = unchecked((uint)(int)Math.Ceiling(interval * (JumpFrameCap - 0.5) / (_frameCount - 0.5)));
        }

        return Math.Max(interval, 1);
    }

    private void Interpolate(uint now)
    {
        // 0x64EB0C rounds the frame index; 0x64EB70 caps it at the frame count.
        uint elapsed = unchecked(now - _startedAt);
        _frame = Math.Min(unchecked(elapsed + (_interval / 2)) / _interval, _frameCount);
        if (_frame >= _frameCount)
        {
            Complete();
            return;
        }

        // 0x64EE59..0x64EEAE: linear offsets rounded by Math_RoundDoubleByHalfStepCompare (0x768AA1).
        uint total = Math.Max(unchecked(_frameCount * _interval), 1);
        uint clamped = Math.Min(elapsed, total);
        _cursorX = _startX + WorldJumpArc.RoundHalfStep((double)(_endX - _startX) * clamped / total);
        _cursorY = _startY + WorldJumpArc.RoundHalfStep((double)(_endY - _startY) * clamped / total);
    }

    private void CommitActiveStep()
    {
        if (Action != 0 && _frame < _frameCount)
        {
            _frame = _frameCount;
            Complete();
        }
    }

    private void Complete()
    {
        (_cursorX, _cursorY) = (_endX, _endY);
        (_tileX, _tileY) = (_endTileX, _endTileY);
    }

    private void FlushPending()
    {
        while (_pending.TryDequeue(out PendingStep step))
        {
            if (step.Kind == StepKind.Jump)
            {
                (_tileX, _tileY) = (step.TargetX, step.TargetY);
                continue;
            }

            _tileX = unchecked(_tileX + step.DeltaX);
            _tileY = unchecked(_tileY + step.DeltaY);
            Direction = step.Direction;
        }

        Action = 0;
        (_startTileX, _startTileY) = (_tileX, _tileY);
        (_endTileX, _endTileY) = (_tileX, _tileY);
        _projected = false;
    }

    private enum StepKind : byte
    {
        Walk,
        Run,
        Jump,
    }

    private readonly record struct PendingStep(StepKind Kind, byte Direction, int DeltaX, int DeltaY,
        int TargetX, int TargetY);
}
