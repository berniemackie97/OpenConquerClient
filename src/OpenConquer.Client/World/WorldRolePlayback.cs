using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal sealed class WorldRolePlayback
{
    internal const ushort GroundWalkFirst = 110;
    internal const ushort GroundWalkSecond = 111;
    internal const ushort GroundRunFirst = 120;
    internal const ushort GroundRunSecond = 121;
    internal const uint GroundFrameIntervalMilliseconds = 25;

    private ushort _action;
    private uint _startedAt;

    internal ushort Action => _action;

    internal void BeginMove(uint selector, uint nowMilliseconds)
    {
        _action = selector switch
        {
            GameWalk10005.WalkSelector =>
                _action is GroundWalkFirst or GroundRunFirst ? GroundWalkSecond : GroundWalkFirst,
            GameWalk10005.RunSelector =>
                _action == GroundRunFirst ? GroundRunSecond : GroundRunFirst,
            _ => 0
        };

        if (_action != 0)
        {
            _startedAt = nowMilliseconds;
        }
    }


    internal bool TryGetFrame(uint nowMilliseconds, uint frameCount, out int frame)
    {
        frame = 0;
        if (_action == 0 || frameCount == 0)
        {
            _action = 0;
            return false;
        }

        uint position = unchecked(nowMilliseconds - _startedAt) / GroundFrameIntervalMilliseconds;
        if (position >= frameCount || position > int.MaxValue)
        {
            _action = 0;
            return false;
        }

        frame = (int)position;
        return true;
    }
}
