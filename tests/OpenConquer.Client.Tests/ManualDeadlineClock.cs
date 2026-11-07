namespace OpenConquer.Client.Tests;

/// <summary>A clock with a single manually triggered deadline for deterministic operation tests.</summary>
internal sealed class ManualDeadlineClock(long timestamp = 0) : TimeProvider, IDisposable
{
    private ManualTimer? _timer;

    public override long TimestampFrequency => 1_000;
    public override long GetTimestamp() => timestamp;
    internal TimeSpan DueTime
    {
        get;
        private set;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Assert.Null(_timer);
        Assert.Equal(Timeout.InfiniteTimeSpan, period);
        DueTime = dueTime;
        _timer = new ManualTimer(callback, state);
        return _timer;
    }

    internal void Fire() => (_timer ?? throw new InvalidOperationException("No deadline was armed.")).Fire();

    public void Dispose() => _timer?.Dispose();

    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        private TimerCallback? _callback = callback;

        internal void Fire() => Interlocked.Exchange(ref _callback, null)?.Invoke(state);

        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

        public void Dispose() => Interlocked.Exchange(ref _callback, null);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
