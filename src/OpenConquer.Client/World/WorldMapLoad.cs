using OpenConquer.Content;
using OpenConquer.Content.Map;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.World;

internal sealed class WorldMapLoad : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task<WorldMapScene> _task;
    private bool _disposed;

    internal WorldMapLoad(IClientContentSource source, GameWorldEntrySnapshot world)
        : this(token =>
        {
            token.ThrowIfCancellationRequested();
            GameMapCatalog catalog = GameMapCatalog.Load(source);
            token.ThrowIfCancellationRequested();
            return WorldMapScene.Load(source, catalog, world.MapDataId, world.X, world.Y,
                unchecked((uint)Environment.TickCount), token);
        })
    {
    }

    internal WorldMapLoad(Func<CancellationToken, WorldMapScene> load)
    {
        _task = Task.Run(() => load(_cancellation.Token), _cancellation.Token);
    }

    internal bool IsCompleted => _task.IsCompleted;

    internal WorldMapScene Complete()
    {
        if (!_task.IsCompleted)
        {
            throw new InvalidOperationException("World map preparation has not completed.");
        }

        try
        {
            return _task.GetAwaiter().GetResult();
        }
        finally
        {
            _disposed = true;
            _cancellation.Dispose();
        }
    }

    internal void Cancel()
    {
        if (!_disposed)
        {
            _cancellation.Cancel();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cancellation.Cancel();
        try
        {
            _task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _disposed = true;
            _cancellation.Dispose();
        }
    }
}
