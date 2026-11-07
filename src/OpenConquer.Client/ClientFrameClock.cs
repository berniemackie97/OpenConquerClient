using OpenConquer.Networking.Game;

namespace OpenConquer.Client;

/// <summary>Samples monotonic frame time once for world update and presentation.</summary>
internal sealed class ClientFrameClock(TimeProvider clock)
{
    private long? _previousTimestamp;

    internal (TimeSpan Elapsed, uint Milliseconds) Next()
    {
        long timestamp = clock.GetTimestamp();
        TimeSpan elapsed = _previousTimestamp is long previous
            ? clock.GetElapsedTime(previous, timestamp)
            : TimeSpan.Zero;
        _previousTimestamp = timestamp;
        return (elapsed, GameClientClock.Milliseconds(clock, timestamp));
    }
}
