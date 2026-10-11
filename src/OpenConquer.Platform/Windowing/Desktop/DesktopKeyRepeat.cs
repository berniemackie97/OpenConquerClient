using System.Diagnostics;

namespace OpenConquer.Platform.Windowing.Desktop;

/// <summary>
/// Supplies bounded command-key repeat because Silk.NET GLFW discards non-text repeat events.
/// Printable-key repetition is supplied by the backend's character callback instead.
/// </summary>
internal sealed class DesktopKeyRepeat
{
    internal static readonly long InitialDelayTicks = Math.Max(1, (long)(Stopwatch.Frequency * 0.45));
    internal static readonly long IntervalTicks = Math.Max(1, (long)(Stopwatch.Frequency * 0.05));

    private DesktopKey _key;
    private long _nextTick;

    public void Press(DesktopKey key, long now)
    {
        if (!IsRepeatable(key))
        {
            Reset();
            return;
        }

        _key = key;
        _nextTick = checked(now + InitialDelayTicks);
    }

    public void Release(DesktopKey key)
    {
        if (_key == key)
        {
            Reset();
        }
    }

    public bool TryRepeat(long now, out DesktopKey key)
    {
        key = _key;

        if (key == DesktopKey.Unknown || now < _nextTick)
        {
            return false;
        }

        // At most one event per frame, including after a long pause or debugger stop.
        _nextTick = checked(now + IntervalTicks);
        return true;
    }

    public void Reset()
    {
        _key = DesktopKey.Unknown;
        _nextTick = 0;
    }

    private static bool IsRepeatable(DesktopKey key) => key is DesktopKey.Backspace or DesktopKey.Delete or
        DesktopKey.Left or DesktopKey.Right or DesktopKey.Up or DesktopKey.Down or DesktopKey.Home or DesktopKey.End or DesktopKey.PageUp or DesktopKey.PageDown;
}
