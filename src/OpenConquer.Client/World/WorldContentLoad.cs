namespace OpenConquer.Client.World;

/// <summary>One cancellable world entry job. Its owner can poll completion or await cleanup.</summary>
internal sealed class WorldContentLoad<T> : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task<T> _task;
    private bool _disposed;

    internal WorldContentLoad(Func<CancellationToken, T> load)
    {
        _task = Task.Run(() => load(_cancellation.Token), _cancellation.Token);
    }

    internal bool IsCompleted => _task.IsCompleted;

    internal T Complete()
    {
        if (!_task.IsCompleted)
        {
            throw new InvalidOperationException("World content preparation has not completed.");
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _cancellation.Cancel();
        try
        {
            await _task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            // Disposal owns cancellation of this job.
        }
        finally
        {
            _disposed = true;
            _cancellation.Dispose();
        }
    }
}
