namespace OpenConquer.Client.World;

/// <summary>
/// One cancellable world entry job. Completion transfers the result. Disposal releases an unclaimed disposable result.
/// </summary>
internal sealed class WorldContentLoad<T> : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task<T> _task;
    private bool _claimed;
    private Task? _disposeTask;

    internal WorldContentLoad(Func<CancellationToken, T> load)
    {
        _task = Task.Run(() => load(_cancellation.Token), _cancellation.Token);
    }

    internal bool IsCompleted => _task.IsCompleted;

    internal T Complete()
    {
        ObjectDisposedException.ThrowIf(_claimed || _disposeTask is not null, this);
        if (!_task.IsCompleted)
        {
            throw new InvalidOperationException("World content preparation has not completed.");
        }

        _claimed = true;
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
        if (!_claimed && _disposeTask is null)
        {
            _cancellation.Cancel();
        }
    }

    public ValueTask DisposeAsync() => _claimed ? ValueTask.CompletedTask : new(_disposeTask ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync()
    {
        await ClientShutdown.CancelAndDrainAsync(_cancellation, async () =>
        {
            T result = await _task.ConfigureAwait(false);
            if (result is IDisposable unclaimed)
            {
                unclaimed.Dispose();
            }
        }).ConfigureAwait(false);
    }
}
