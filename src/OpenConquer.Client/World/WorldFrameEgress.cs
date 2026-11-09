using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.World;

/// <summary>
/// Sends the client's own world frames on the session's single outbound lane, one write at a time. Native sends the
/// heartbeat synchronously from the frame tick; here a due timer waits while a previous write is still in flight, so
/// the native timer rule decides only once the lane is idle. A failed write is rethrown on the next pump.
/// </summary>
internal sealed class WorldFrameEgress : IAsyncDisposable
{
    internal const int MaximumQueuedResponses = 4096;

    private readonly Func<uint, CancellationToken, Task> _sendLatencyTick;
    private readonly Func<uint, uint, uint, CancellationToken, Task> _sendLatencyReport;
    private readonly Func<WorldCorrectionReply, uint, CancellationToken, Task> _sendCorrection;
    private readonly Func<GameWorldActionResponse, uint, CancellationToken, Task>? _sendActionReply;
    private readonly Queue<WorldOutboundReply> _responses = new();
    private readonly CancellationTokenSource _stopping = new();
    private Task? _pending;
    private Task? _disposeTask;
    private bool _checkHeartbeatBeforeNextReply;

    internal WorldFrameEgress(Func<uint, CancellationToken, Task> sendLatencyTick,
        Func<uint, uint, uint, CancellationToken, Task> sendLatencyReport,
        Func<WorldCorrectionReply, uint, CancellationToken, Task> sendCorrection,
        Func<GameWorldActionResponse, uint, CancellationToken, Task>? sendActionReply = null)
    {
        _sendLatencyTick = sendLatencyTick ?? throw new ArgumentNullException(nameof(sendLatencyTick));
        _sendLatencyReport = sendLatencyReport ?? throw new ArgumentNullException(nameof(sendLatencyReport));
        _sendCorrection = sendCorrection ?? throw new ArgumentNullException(nameof(sendCorrection));
        _sendActionReply = sendActionReply;
    }

    internal void QueuePositionCorrection(WorldCorrectionReply correction) =>
        Enqueue(new WorldOutboundReply(correction, default));

    internal void QueueWorldActionResponse(GameWorldActionResponse response)
    {
        if (_sendActionReply is null)
        {
            throw new InvalidOperationException("The world action writer is not configured.");
        }

        Enqueue(new WorldOutboundReply(null, response));
    }

    private void Enqueue(WorldOutboundReply reply)
    {
        ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
        if (_responses.Count == MaximumQueuedResponses)
        {
            throw new WorldResponseQueueFullException();
        }

        _responses.Enqueue(reply);
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

        if (_checkHeartbeatBeforeNextReply || _responses.Count == 0)
        {
            _checkHeartbeatBeforeNextReply = false;
            WorldHeartbeatSend send = heartbeat.Tick(now);
            if (send != WorldHeartbeatSend.None)
            {
                _pending = SendAsync(send, heartbeat.LocalAddress, heartbeat.Latency, now, _stopping.Token);
                return;
            }
        }

        if (_responses.TryDequeue(out WorldOutboundReply reply))
        {
            _checkHeartbeatBeforeNextReply = true;
            _pending = reply.Correction is { } correction
                ? _sendCorrection(correction, now, _stopping.Token)
                : _sendActionReply!(reply.ActionResponse, now, _stopping.Token);
        }
    }

    public ValueTask DisposeAsync() => new(_disposeTask ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync()
    {
        Task? pending = _pending;
        _pending = null;
        _responses.Clear();
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

internal readonly record struct WorldOutboundReply(WorldCorrectionReply? Correction,
    GameWorldActionResponse ActionResponse);

internal sealed class WorldResponseQueueFullException : IOException
{
    internal WorldResponseQueueFullException() : base("World outbound reply queue exceeded its bounded capacity.")
    {
    }
}
