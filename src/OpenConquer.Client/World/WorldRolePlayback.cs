using OpenConquer.Networking.Game.World;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.World;

internal sealed class WorldRolePlayback
{
    internal const ushort GroundWalkFirst = 110;
    internal const ushort GroundWalkSecond = 111;
    internal const ushort GroundRunFirst = 120;
    internal const ushort GroundRunSecond = 121;
    internal const ushort GroundJump = 130;

    internal const ushort Standby = 100;

    internal const uint StandbyFrameIntervalMilliseconds = 66;
    internal const uint GroundFrameIntervalMilliseconds = 25;
    internal const uint JumpBaseFrameIntervalMilliseconds = 33;

    internal const int JumpFrameCap = 20;

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
    private ushort _lastPhysicalAction;
    private uint _startedAt;
    private uint _frameCount;
    private uint _frame;
    private int _cursorTileX;
    private int _cursorTileY;
    private bool _standbyActive;
    private uint _standbyStartedAt;
    private uint _standbyFrameCount;
    private uint _standbyFrame;

    private readonly Func<int, int> _random;
    private bool _hasCommand;

    internal WorldRolePlayback(int tileX, int tileY, byte direction, Func<int, int>? random = null)
    {
        _random = random ?? Random.Shared.Next;
        _tileX = _startTileX = _endTileX = tileX;
        _tileY = _startTileY = _endTileY = tileY;
        Direction = direction;
    }

    internal ushort Action
    {
        get; private set;
    }

    internal byte Direction
    {
        get; private set;
    }

    internal int PendingCount => _pending.Count;

    internal uint FrameInterval { get; private set; } = GroundFrameIntervalMilliseconds;

    internal bool IsProjected
    {
        get; private set;
    }

    /// <summary>Interpolated world pixel position.</summary>
    internal (int X, int Y) Cursor => (_cursorX, _cursorY);

    /// <summary>Projected start and end of the current step.</summary>
    internal ((int X, int Y) Start, (int X, int Y) End) Endpoints => ((_startX, _startY), (_endX, _endY));

    internal (int X, int Y) DepthTile => Action switch
    {
        GroundWalkFirst or GroundWalkSecond or GroundRunFirst or GroundRunSecond => Direction <= 3 ? (_startTileX, _startTileY) : (_endTileX, _endTileY),
        GroundJump => (_cursorTileX, _cursorTileY),
        _ => (_tileX, _tileY),
    };

    internal void Enqueue(GameWalk10005 walk)
    {
        CommitActiveStep();
        (int dx, int dy) = walk.GetDelta();

        if (walk.IsMounted)
        {
            // Mounted command presentation is not reconstructed; keep the tile without a stride.
            FlushPending();
            MoveTo(unchecked(_tileX + dx), unchecked(_tileY + dy), (byte)(walk.DirectionIndex % 8));
            return;
        }

        Push(new PendingStep((byte)walk.Selector == GameWalk10005.RunSelector ? StepKind.Run : StepKind.Walk, (byte)(walk.DirectionIndex % 8), dx, dy, 0, 0));
    }

    internal void EnqueueJump(int targetX, int targetY)
    {
        CommitActiveStep();
        Push(new PendingStep(StepKind.Jump, 0, 0, 0, targetX, targetY));
    }

    internal void MoveTo(int tileX, int tileY, byte direction)
    {
        _pending.Clear();
        Action = 0;
        _standbyActive = false;
        _tileX = _startTileX = _endTileX = tileX;
        _tileY = _startTileY = _endTileY = tileY;
        Direction = direction;
        IsProjected = false;
    }

    internal void Advance(uint nowMilliseconds, Func<ushort, int> frameCount, Func<int, int, (int X, int Y)> project, Func<int, int, (int X, int Y)> unproject)
    {
        ArgumentNullException.ThrowIfNull(frameCount);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(unproject);

        if (!IsProjected)
        {
            (_cursorX, _cursorY) = project(_tileX, _tileY);
            (_startX, _startY) = (_cursorX, _cursorY);
            (_endX, _endY) = (_cursorX, _cursorY);
            IsProjected = true;
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
            Action = 0;
            (_startX, _startY) = (_cursorX, _cursorY);
            (_endX, _endY) = (_cursorX, _cursorY);
            (_startTileX, _startTileY) = (_tileX, _tileY);
            (_endTileX, _endTileY) = (_tileX, _tileY);
            _standbyActive = false;
        }

        AdvanceStandby(nowMilliseconds, frameCount);
    }

    internal int StandbyFrame => (int)Math.Min(_standbyFrame, Math.Max(_standbyFrameCount, 1) - 1);

    internal ushort StandbyAction { get; private set; } = Standby;

    private void AdvanceStandby(uint now, Func<ushort, int> frameCount)
    {
        if (!_standbyActive || _standbyFrame >= _standbyFrameCount)
        {
            _standbyActive = true;
            _standbyStartedAt = now;
            StandbyAction = !_hasCommand && _random(10) == 1 ? (ushort)(Standby + 1 + _random(3)) : Standby;
            _standbyFrameCount = (uint)Math.Max(frameCount(StandbyAction), 1);
            _standbyFrame = 0;
            return;
        }

        uint elapsed = unchecked(now - _standbyStartedAt);
        _standbyFrame = Math.Min(unchecked(elapsed + (StandbyFrameIntervalMilliseconds / 2)) / StandbyFrameIntervalMilliseconds, _standbyFrameCount);
    }

    internal bool TryGetFrame(uint clipFrames, out int frame)
    {
        frame = 0;
        if (Action == 0 || clipFrames == 0)
        {
            return false;
        }

        frame = (int)Math.Min(_frame, clipFrames - 1);
        return true;
    }

    internal (int X, int Y, float RotationDegrees, float Elevation) GetPose(Func<(int X, int Y), (int X, int Y), (int X, int Y), int> arcHeight, Func<int, int, int> adjustedElevation)
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

        return (_cursorX, _cursorY - terrainDelta - arc, facing, 2f * terrainDelta);
    }

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
        _hasCommand = true;
        (_startTileX, _startTileY) = (_tileX, _tileY);
        (_startX, _startY) = (_cursorX, _cursorY);

        if (step.Kind == StepKind.Jump)
        {
            Action = GroundJump;
            Direction = SelectDirectionTowardTile(unchecked(step.TargetX - _tileX), unchecked(step.TargetY - _tileY));
            (_endTileX, _endTileY) = (step.TargetX, step.TargetY);
        }
        else
        {
            Action = step.Kind == StepKind.Run
                ? _lastPhysicalAction == GroundRunFirst
                    ? GroundRunSecond
                    : GroundRunFirst
                : _lastPhysicalAction is GroundWalkFirst or GroundRunFirst
                    ? GroundWalkSecond
                    : GroundWalkFirst;

            Direction = step.Direction;
            (_endTileX, _endTileY) = (unchecked(_tileX + step.DeltaX), unchecked(_tileY + step.DeltaY));
        }

        _lastPhysicalAction = Action;
        (_endX, _endY) = project(_endTileX, _endTileY);
        _frameCount = (uint)Math.Max(frameCount(Action), 1);
        FrameInterval = Action == GroundJump ? JumpInterval() : GroundFrameIntervalMilliseconds;
        _frame = 0;
        _startedAt = now;
        (_cursorTileX, _cursorTileY) = (_tileX, _tileY);
    }

    private uint JumpInterval()
    {
        int spanX = unchecked(_startX - _endX);
        int spanY = unchecked(_startY - _endY);
        double distance = Math.Sqrt(unchecked(spanX * spanX + spanY * spanY));
        uint interval = unchecked((uint)(int)(JumpBaseFrameIntervalMilliseconds * distance * (1.0 / 300) + 1.0));

        if (interval < JumpBaseFrameIntervalMilliseconds)
        {
            interval = JumpBaseFrameIntervalMilliseconds;
        }

        if (_frameCount < JumpFrameCap)
        {
            interval = unchecked((uint)(int)Math.Ceiling(interval * (JumpFrameCap - 0.5) / (_frameCount - 0.5)));
        }

        return Math.Max(interval, 1);
    }

    private void Interpolate(uint now)
    {
        uint elapsed = unchecked(now - _startedAt);
        _frame = Math.Min(unchecked(elapsed + (FrameInterval / 2)) / FrameInterval, _frameCount);

        if (_frame >= _frameCount)
        {
            Complete();
            return;
        }

        uint total = Math.Max(unchecked(_frameCount * FrameInterval), 1);
        uint clamped = Math.Min(elapsed, total);
        _cursorX = _startX + WorldJumpArc.RoundHalfStep((double)(_endX - _startX) * clamped / total);
        _cursorY = _startY + WorldJumpArc.RoundHalfStep((double)(_endY - _startY) * clamped / total);
    }

    private void CommitActiveStep()
    {
        if (Action == 0 || _frame >= _frameCount)
        {
            return;
        }

        _frame = _frameCount;
        Complete();
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
        IsProjected = false;
    }

    private enum StepKind : byte
    {
        Walk,
        Run,
        Jump,
    }

    private readonly record struct PendingStep(StepKind Kind, byte Direction, int DeltaX, int DeltaY, int TargetX, int TargetY);
}
