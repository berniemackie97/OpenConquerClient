using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

/// <summary>
/// One remote role's ground-step presentation: the queued 10005 steps, the active step's
/// projected start and end, and the interpolated screen cursor.
/// </summary>
/// <remarks>
/// Native anchors (5517 <c>Conquer.exe</c>): ingress <c>Role_HandleIncomingActionStateBatch</c>
/// (0x6720EE, label 0x672276) commits the step in progress and queues the new one; the tick
/// <c>Role_TickCommandStateAndDispatchPlayback</c> (0x672662..0x6726E0) starts one queued step when the
/// current motion is complete; <c>Role_ApplyGroundMoveActionAndPoseFromPathCursor</c> (0x73631C) stages
/// start = cursor and end = projection of the cursor tile plus the direction delta;
/// <c>Role_AdvanceMotionInterpolationAndRefreshPosition</c> (0x64EAB2) interpolates linearly for ground
/// walk and run because no ActionCtrl.ini record matches actions 110/111/120/121. See 20_notes
/// Network/15_msgwalk_10005_direction_index_order.md, "Remote step presentation".
/// </remarks>
internal sealed class WorldRolePlayback
{
    internal const ushort GroundWalkFirst = 110;
    internal const ushort GroundWalkSecond = 111;
    internal const ushort GroundRunFirst = 120;
    internal const ushort GroundRunSecond = 121;

    /// <summary>Role_ComputeFrameIntervalFromAction base interval for actions 110/111/120/121.</summary>
    internal const uint GroundFrameIntervalMilliseconds = 25;

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
    private uint _frameCount;
    private uint _frame;

    internal WorldRolePlayback(int tileX, int tileY, byte direction)
    {
        _tileX = _startTileX = _endTileX = tileX;
        _tileY = _startTileY = _endTileY = tileY;
        Direction = direction;
    }

    /// <summary>Current ground action, or 0 while standing.</summary>
    internal ushort Action
    {
        get; private set;
    }

    internal byte Direction
    {
        get; private set;
    }

    internal int PendingCount => _pending.Count;

    /// <summary>False until the first <see cref="Advance"/> projects the starting tile.</summary>
    internal bool IsProjected => _projected;

    /// <summary>Interpolated world-pixel position (native CRole+0x350/+0x354).</summary>
    internal (int X, int Y) Cursor => (_cursorX, _cursorY);

    /// <summary>
    /// Tile used for depth ordering: mid-step the start tile when the direction index is at most 3,
    /// otherwise the end tile (CNpc tick 0x672784); the cursor tile while standing.
    /// </summary>
    internal (int X, int Y) DepthTile => Action == 0
        ? (_tileX, _tileY)
        : Direction <= 3 ? (_startTileX, _startTileY) : (_endTileX, _endTileY);

    /// <summary>Queues a relayed step after committing the step in progress (0x672276).</summary>
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

        if (_pending.Count >= MaximumPendingSteps)
        {
            FlushPending();
        }

        _pending.Enqueue(new PendingStep((byte)walk.Selector == GameWalk10005.RunSelector,
            (byte)(walk.DirectionIndex % 8), dx, dy));
    }

    /// <summary>
    /// Drops queued steps and places the role on a tile without a stride. No jump or mounted action is
    /// staged here, so the last physical action used for walk/run selection is unchanged; those native
    /// presentations are not reconstructed.
    /// </summary>
    internal void MoveTo(int tileX, int tileY, byte direction)
    {
        _pending.Clear();
        Action = 0;
        _tileX = _startTileX = _endTileX = tileX;
        _tileY = _startTileY = _endTileY = tileY;
        Direction = direction;
        _projected = false;
    }

    /// <summary>
    /// One role tick: interpolate the running step, start the next queued step once the current one is
    /// complete, or stand.
    /// </summary>
    internal void Advance(uint nowMilliseconds, Func<ushort, int> frameCount, Func<int, int, (int X, int Y)> project)
    {
        ArgumentNullException.ThrowIfNull(frameCount);
        ArgumentNullException.ThrowIfNull(project);
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
        }
    }

    /// <summary>Animation frame of the current ground action at <paramref name="nowMilliseconds"/>.</summary>
    internal bool TryGetFrame(uint nowMilliseconds, uint clipFrames, out int frame)
    {
        frame = 0;
        if (Action == 0 || clipFrames == 0)
        {
            return false;
        }

        uint position = unchecked(nowMilliseconds - _startedAt) / GroundFrameIntervalMilliseconds;
        frame = (int)Math.Min(position, clipFrames - 1);
        return true;
    }

    private void Stage(PendingStep step, uint now, Func<ushort, int> frameCount, Func<int, int, (int X, int Y)> project)
    {
        // Role_CommandType16 (0x736329) and Role_CommandType17 (0x736AEC) select from the last physical action.
        Action = step.Run
            ? _lastPhysicalAction == GroundRunFirst ? GroundRunSecond : GroundRunFirst
            : _lastPhysicalAction is GroundWalkFirst or GroundRunFirst ? GroundWalkSecond : GroundWalkFirst;
        _lastPhysicalAction = Action;
        Direction = step.Direction;
        (_startTileX, _startTileY) = (_tileX, _tileY);
        (_endTileX, _endTileY) = (unchecked(_tileX + step.DeltaX), unchecked(_tileY + step.DeltaY));
        (_startX, _startY) = (_cursorX, _cursorY);
        (_endX, _endY) = project(_endTileX, _endTileY);
        _frameCount = (uint)Math.Max(frameCount(Action), 1);
        _frame = 0;
        _startedAt = now;
    }

    private void Interpolate(uint now)
    {
        // 0x64EB0C rounds the frame index; 0x64EB70 caps it at the frame count.
        uint elapsed = unchecked(now - _startedAt);
        _frame = Math.Min(unchecked(elapsed + (GroundFrameIntervalMilliseconds / 2)) / GroundFrameIntervalMilliseconds,
            _frameCount);
        if (_frame >= _frameCount)
        {
            Complete();
            return;
        }

        // 0x64EE59..0x64EEAE: linear fallback, truncated toward zero.
        uint total = Math.Max(unchecked(_frameCount * GroundFrameIntervalMilliseconds), 1);
        uint clamped = Math.Min(elapsed, total);
        _cursorX = _startX + (int)((double)(_endX - _startX) * clamped / total);
        _cursorY = _startY + (int)((double)(_endY - _startY) * clamped / total);
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
            _tileX = unchecked(_tileX + step.DeltaX);
            _tileY = unchecked(_tileY + step.DeltaY);
            Direction = step.Direction;
        }

        Action = 0;
        (_startTileX, _startTileY) = (_tileX, _tileY);
        (_endTileX, _endTileY) = (_tileX, _tileY);
        _projected = false;
    }

    private readonly record struct PendingStep(bool Run, byte Direction, int DeltaX, int DeltaY);
}
