namespace OpenConquer.Networking.Game;

/// <summary>
/// The client's millisecond counter, the role native <c>timeGetTime</c> plays (helper 0x768FEB): a wrapping 32-bit
/// count of milliseconds from the clock's origin.
/// </summary>
internal static class GameClientClock
{
    internal static uint Milliseconds(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return unchecked((uint)(long)clock.GetElapsedTime(0, clock.GetTimestamp()).TotalMilliseconds);
    }
}
