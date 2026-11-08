using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Handshake;

namespace OpenConquer.Client.World;

internal sealed class WorldFrameIngress : IAsyncDisposable
{
    private const int MaximumFramesPerTick = 32;

    private readonly Func<CancellationToken, Task<GameSecuredInboundFrame?>> _receive;
    private readonly CancellationTokenSource _stopping = new();
    private Task<GameSecuredInboundFrame?>? _pending;
    private Task? _disposeTask;

    internal WorldFrameIngress(GameServerHandshakeSession session) : this(session.ReadWorldFrameAsync)
    {
    }

    internal WorldFrameIngress(Func<CancellationToken, Task<GameSecuredInboundFrame?>> receive)
    {
        _receive = receive;
        _pending = receive(_stopping.Token);
    }

    /// <summary>Observe EOF or a failed receive while the world is preparing. Retain any prefetched frame.</summary>
    internal void ObserveConnectionFailure()
    {
        ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
        if (_pending is { IsCompleted: true } completed &&
            (!completed.IsCompletedSuccessfully || completed.GetAwaiter().GetResult() is null))
        {
            // Clear the pending receive before propagating its failure to the owner.
            _pending = null;
            _ = RequireFrame(completed);
        }
    }

    internal int Drain(Action<ReadOnlyMemory<byte>> apply)
    {
        ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
        int processed = 0;

        while (processed < MaximumFramesPerTick && _pending is { IsCompleted: true } completed)
        {
            _pending = null;
            using GameSecuredInboundFrame frame = RequireFrame(completed);
            apply(frame.Packet);
            processed++;
            _pending = _receive(_stopping.Token);
        }

        return processed;
    }

    private static GameSecuredInboundFrame RequireFrame(Task<GameSecuredInboundFrame?> completed) =>
        completed.GetAwaiter().GetResult() ??
        throw new EndOfStreamException("GameServer closed the admitted world session.");

    public ValueTask DisposeAsync() => new(_disposeTask ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync()
    {
        Task<GameSecuredInboundFrame?>? pending = _pending;
        _pending = null;
        await ClientShutdown.CancelAndDrainAsync(_stopping, async () =>
        {
            if (pending is not null)
            {
                using GameSecuredInboundFrame? frame = await pending.ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

}
