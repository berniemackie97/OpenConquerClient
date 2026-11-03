namespace OpenConquer.Client.World;

internal sealed class WorldRoleFrameClock
{
    private const uint IdleFrameIntervalMilliseconds = 66;
    private uint? _start;

    internal int GetFrame(uint nowMilliseconds, uint frameCount)
    {
        if (frameCount == 0)
        {
            return 0;
        }

        _start ??= nowMilliseconds;
        uint elapsed = unchecked(nowMilliseconds - _start.Value);
        return (int)((elapsed / IdleFrameIntervalMilliseconds) % frameCount);
    }
}
