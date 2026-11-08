namespace OpenConquer.Client.World;

/// <summary>
/// Sends the client's own world frames on the session's single outbound lane, one write at a time. Native sends the
/// heartbeat synchronously from the frame tick; here a due timer waits while a previous write is still in flight, so
/// the native timer rule decides only once the lane is idle. A failed write is rethrown on the next pump.
/// </summary>
internal sealed class WorldFrameEgress : IAsyncDisposable
{
    internal const int MaximumQueuedCorrections = 4096;

    private readonly Func<uint, CancellationToken, Task> _sendLatencyTick;
    private readonly Func<uint, uint, uint, CancellationToken, Task> _sendLatencyReport;
    private readonly Func<WorldCorrectionReply, uint, CancellationToken, Task> _sendCorrection;
    private readonly Queue<WorldCorrectionReply> _corrections = new();
    private readonly CancellationTokenSource _stopping = new();
    private Task? _pending;
    private Task? _disposeTask;
    private bool _checkHeartbeatBeforeNextCorrection;

    internal WorldFrameEgress(Func<uint, CancellationToken, Task> sendLatencyTick,
        Func<uint, uint, uint, CancellationToken, Task> sendLatencyReport,
        Func<WorldCorrectionReply, uint, CancellationToken, Task> sendCorrection)
    {
        _sendLatencyTick = sendLatencyTick ?? throw new ArgumentNullException(nameof(sendLatencyTick));
        _sendLatencyReport = sendLatencyReport ?? throw new ArgumentNullException(nameof(sendLatencyReport));
        _sendCorrection = sendCorrection ?? throw new ArgumentNullException(nameof(sendCorrection));
    }

    internal void QueuePositionCorrection(WorldCorrectionReply correction)
    {
        ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
        if (_corrections.Count == MaximumQueuedCorrections)
        {
            throw new WorldCorrectionQueueFullException();
        }

        _corrections.Enqueue(correction);
    }

    internal void Pump(WorldHeartbeat heartbeat, uint now)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
        if (_pending is { IsCompleted: true } completed)
        {
            _pending = null;
            completed.GetAwaiter().GetResult();
        }

        if (_pending is not null)
        {
            return;
        }

        if (_checkHeartbeatBeforeNextCorrection || _corrections.Count == 0)
        {
            _checkHeartbeatBeforeNextCorrection = false;
            WorldHeartbeatSend send = heartbeat.Tick(now);
            if (send != WorldHeartbeatSend.None)
            {
                _pending = SendAsync(send, heartbeat.LocalAddress, heartbeat.Latency, now, _stopping.Token);
                return;
            }
        }

        if (_corrections.TryDequeue(out WorldCorrectionReply correction))
        {
            _checkHeartbeatBeforeNextCorrection = true;
            _pending = _sendCorrection(correction, now, _stopping.Token);
        }
    }

    public ValueTask DisposeAsync() => new(_disposeTask ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync()
    {
        Task? pending = _pending;
        _pending = null;
        _corrections.Clear();
        await ClientShutdown.CancelAndDrainAsync(_stopping, async () =>
        {
            if (pending is not null)
            {
                await pending.ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    private async Task SendAsync(WorldHeartbeatSend send, uint localAddress, uint latency, uint now,
        CancellationToken cancellationToken)
    {
        await _sendLatencyTick(now, cancellationToken).ConfigureAwait(false);
        if (send == WorldHeartbeatSend.LatencyTickAndReport)
        {
            await _sendLatencyReport(localAddress, latency, now, cancellationToken).ConfigureAwait(false);
        }
    }
}

internal readonly record struct WorldCorrectionReply(ushort X, ushort Y, byte DirectionIndex);

internal sealed class WorldCorrectionQueueFullException : IOException
{
    internal WorldCorrectionQueueFullException() : base("World correction reply queue exceeded its bounded capacity.")
    {
    }
}
