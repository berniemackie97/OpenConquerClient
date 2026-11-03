using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Handshake;

namespace OpenConquer.Client.World;

internal sealed class WorldFrameIngress : IAsyncDisposable
{
    private const int MaximumFramesPerTick = 32;

    private readonly Func<CancellationToken, Task<GameSecuredInboundFrame?>> _receive;
    private readonly CancellationTokenSource _stopping = new();
    private Task<GameSecuredInboundFrame?>? _pending;
    private bool _disposed;

    internal WorldFrameIngress(GameServerHandshakeSession session) : this(session.ReadWorldFrameAsync)
    {
    }

    internal WorldFrameIngress(Func<CancellationToken, Task<GameSecuredInboundFrame?>> receive)
    {
        _receive = receive;
        _pending = receive(_stopping.Token);
    }

    internal int Drain(Action<ReadOnlyMemory<byte>> apply)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int processed = 0;

        while (processed < MaximumFramesPerTick && _pending is { IsCompleted: true } completed)
        {
            _pending = null;
            using GameSecuredInboundFrame frame = completed.GetAwaiter().GetResult() ??
                throw new EndOfStreamException("GameServer closed the admitted world session.");
            apply(frame.Packet);
            processed++;
            _pending = _receive(_stopping.Token);
        }

        return processed;
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
                using GameSecuredInboundFrame? frame = await pending.ConfigureAwait(false);
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
}
