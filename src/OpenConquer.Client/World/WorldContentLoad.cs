namespace OpenConquer.Client.World;

/// <summary>
/// One cancellable world entry job. Completion transfers the result. Disposal releases an unclaimed disposable result.
/// </summary>
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_task.IsCompleted)
        {
            throw new InvalidOperationException("World content preparation has not completed.");
        }

        _disposed = true;
        try
        {
            return _task.GetAwaiter().GetResult();
        }
        finally
        {
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

        _disposed = true;
        try
        {
            _cancellation.Cancel();
            T result;
            try
            {
                result = await _task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                // Disposal owns cancellation of this job.
                return;
            }

            if (result is IDisposable unclaimed)
            {
                unclaimed.Dispose();
            }
        }
        finally
        {
            _cancellation.Dispose();
        }
    }
}
