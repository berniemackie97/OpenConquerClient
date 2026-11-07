namespace OpenConquer.Client.World;

/// <summary>
/// Sends the client's own world frames on the session's single outbound lane, one write at a time. Native sends the
/// heartbeat synchronously from the frame tick; here a due timer waits while a previous write is still in flight, so
/// the native timer rule decides only once the lane is idle. A failed write is rethrown on the next pump.
/// </summary>
internal sealed class WorldFrameEgress : IAsyncDisposable
{
    private readonly Func<uint, CancellationToken, Task> _sendLatencyTick;
    private readonly Func<uint, uint, uint, CancellationToken, Task> _sendLatencyReport;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _pending;
    private bool _disposed;

    internal WorldFrameEgress(Func<uint, CancellationToken, Task> sendLatencyTick,
        Func<uint, uint, uint, CancellationToken, Task> sendLatencyReport)
    {
        _sendLatencyTick = sendLatencyTick ?? throw new ArgumentNullException(nameof(sendLatencyTick));
        _sendLatencyReport = sendLatencyReport ?? throw new ArgumentNullException(nameof(sendLatencyReport));
    }

    internal void Pump(WorldHeartbeat heartbeat, uint now)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pending is { IsCompleted: true } completed)
        {
            _pending = null;
            completed.GetAwaiter().GetResult();
        }

        if (_pending is not null)
        {
            return;
        }

        WorldHeartbeatSend send = heartbeat.Tick(now);
        if (send != WorldHeartbeatSend.None)
        {
            _pending = SendAsync(send, heartbeat.LocalAddress, heartbeat.Latency, now, _stopping.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            if (_pending is { } pending)
            {
                await pending.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        finally
        {
            _pending = null;
            _stopping.Dispose();
        }
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
