namespace OpenConquer.Platform.Internal;

internal sealed class DesktopFramePacer
{
    /// <summary>
    /// Largest whole-millisecond count <see cref="Thread.Sleep(TimeSpan)"/> accepts.
    /// </summary>
    /// <remarks>
    /// <see cref="Thread.Sleep(TimeSpan)"/> truncates to whole milliseconds and rejects the result
    /// only when it exceeds <see cref="int.MaxValue"/>, so an interval carrying a sub-millisecond
    /// fraction above that boundary is still accepted. The guard below reproduces that comparison
    /// exactly rather than approximating it with a <see cref="TimeSpan"/> bound, which would reject
    /// intervals the sleep itself would have honoured.
    /// </remarks>
    private const long MaximumSleepWholeMilliseconds = int.MaxValue;

    private readonly TimeSpan _frameInterval;
    private readonly TimeProvider _timeProvider;
    private readonly Action<TimeSpan> _sleep;

    private long _lastFrameStartTimestamp;
    private bool _started;

    public DesktopFramePacer(TimeSpan frameInterval) : this(frameInterval, TimeProvider.System, Thread.Sleep) { }

    internal DesktopFramePacer(TimeSpan frameInterval, TimeProvider timeProvider, Action<TimeSpan> sleep)
    {
        if (frameInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(frameInterval), frameInterval, "Frame interval must be greater than zero.");
        }

        if ((long)frameInterval.TotalMilliseconds > MaximumSleepWholeMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(frameInterval), frameInterval, "Frame interval must not exceed Int32.MaxValue whole milliseconds.");
        }

        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(sleep);

        _frameInterval = frameInterval;
        _timeProvider = timeProvider;
        _sleep = sleep;
    }

    public void Start()
    {
        _lastFrameStartTimestamp = _timeProvider.GetTimestamp();
        _started = true;
    }

    public void WaitForNextFrame()
    {
        if (!_started)
        {
            throw new InvalidOperationException("Frame pacing must be started before waiting for a frame.");
        }

        while (true)
        {
            long currentTimestamp = _timeProvider.GetTimestamp();

            TimeSpan elapsed = _timeProvider.GetElapsedTime(_lastFrameStartTimestamp, currentTimestamp);

            if (elapsed < TimeSpan.Zero)
            {
                throw new InvalidOperationException("The frame-pacing time source moved backwards.");
            }

            if (elapsed >= _frameInterval)
            {
                _lastFrameStartTimestamp = currentTimestamp;
                return;
            }

            _sleep(_frameInterval - elapsed);
        }
    }
}
