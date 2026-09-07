namespace OpenConquer.Launcher.Settings;

/// <summary>Owns one settings surface's I/O and its idempotent shutdown, independently of UI events.</summary>
internal sealed class DisplaySettingsSession : IAsyncDisposable
{
    private readonly IDisplaySettingsStore _store;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private Task _operation = Task.CompletedTask;
    private Task? _shutdown;

    public DisplaySettingsSession(IDisplaySettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public Task<SettingsReadResult> LoadAsync()
    {
        return Start(_store.LoadAsync);
    }

    public Task<SettingsIssue?> SaveAsync(DisplayPreferences preferences, string? revision, bool resetInvalid)
    {
        return Start(token => _store.SaveAsync(preferences, revision, resetInvalid, token));
    }

    private Task<T> Start<T>(Func<CancellationToken, Task<T>> start)
    {
        lock (_gate)
        {
            if (_shutdown is not null)
            {
                throw new InvalidOperationException("Settings are closing.");
            }

            if (!_operation.IsCompleted)
            {
                throw new InvalidOperationException("A settings operation is already running.");
            }

            Task<T> operation = start(_lifetime.Token);
            _operation = operation;
            return operation;
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            if (_shutdown is not null)
            {
                return _shutdown;
            }

            TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _shutdown = completion.Task;
            _ = DrainAsync(_operation, completion);
            return _shutdown;
        }
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(StopAsync());
    }

    private async Task DrainAsync(Task operation, TaskCompletionSource completion)
    {
        CancellationToken token = _lifetime.Token;
        Exception? failure = null;
        try
        {
            try
            {
                await _lifetime.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception exception) { failure = exception; }
            try
            {
                await operation.ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (token.IsCancellationRequested && exception.CancellationToken == token) { }
            catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        }
        finally { _lifetime.Dispose(); }

        if (failure is null)
        {
            completion.SetResult();
        }
        else
        {
            completion.SetException(failure);
        }
    }
}
