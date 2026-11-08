namespace OpenConquer.Client.Startup.Catalog;

/// <summary>
/// Refreshes the signed catalog on a timer. A catalog is accepted only if its sequence is higher than any
/// accepted before it in this process.
/// </summary>
internal sealed class LoginCatalogRefresh : IDisposable
{
    private static readonly TimeSpan s_refreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan s_retryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_expiryLead = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan s_minimumRefreshInterval = TimeSpan.FromSeconds(10);

    private readonly Func<CancellationToken, Task<SignedLoginCatalog?>> _fetch;
    private readonly CancellationTokenSource _cancellation = new();
    private Task<SignedLoginCatalog?>? _pending;
    private DateTimeOffset _nextAttemptAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastObservedAt = DateTimeOffset.MinValue;
    private long _highestAcceptedSequence;
    private bool _clockRolledBack;
    private Task? _stopTask;

    public SignedLoginCatalog? Current
    {
        get; private set;
    }
    public bool LastAttemptFailed
    {
        get; private set;
    }

    public LoginCatalogRefresh(Func<CancellationToken, Task<SignedLoginCatalog?>> fetch)
    {
        ArgumentNullException.ThrowIfNull(fetch);
        _fetch = fetch;
    }

    /// <summary>Runs on the render thread. Returns true only when <see cref="Current"/> changes.</summary>
    public bool Advance(DateTimeOffset now)
    {
        ObjectDisposedException.ThrowIf(_stopTask is not null, this);

        if (_cancellation.IsCancellationRequested)
        {
            return false;
        }

        bool changed = false;

        if (now < _lastObservedAt)
        {
            _clockRolledBack = true;
            LastAttemptFailed = true;
        }
        else
        {
            _lastObservedAt = now;
        }

        if (Current is not null && (_clockRolledBack || !Current.IsCurrent(now)))
        {
            Current = null;
            changed = true;
        }

        if (_pending is { IsCompleted: true } completed)
        {
            _pending = null;
            SignedLoginCatalog? incoming = null;

            try
            {
                incoming = completed.GetAwaiter().GetResult();
            }
            catch (Exception failure) when (IsAcquisitionFailure(failure))
            {
                // Acquisition errors are unavailable catalog data, not UI or process failures.
            }

            if (!_clockRolledBack && incoming is not null && incoming.IsCurrent(now) && incoming.Sequence > _highestAcceptedSequence)
            {
                _highestAcceptedSequence = incoming.Sequence;
                Current = incoming;
                LastAttemptFailed = false;
                changed = true;
                _nextAttemptAt = NextRefresh(now, incoming.ExpiresAt);
            }
            else if (!_clockRolledBack && incoming is not null && incoming.Sequence == _highestAcceptedSequence && Current is not null)
            {
                // The publisher may still serve the last accepted signed snapshot.
                LastAttemptFailed = false;
                _nextAttemptAt = NextRefresh(now, Current.ExpiresAt);
            }
            else
            {
                LastAttemptFailed = true;
                _nextAttemptAt = now + s_retryInterval;
            }
        }

        if (!_clockRolledBack && _pending is null && now >= _nextAttemptAt)
        {
            try
            {
                _pending = _fetch(_cancellation.Token) ?? throw new InvalidOperationException("The catalog fetch returned no task.");
            }
            catch (Exception failure) when (IsAcquisitionFailure(failure))
            {
                LastAttemptFailed = true;
                _nextAttemptAt = now + s_retryInterval;
            }
        }

        return changed;
    }

    public void Cancel()
    {
        if (_stopTask is null && !_cancellation.IsCancellationRequested)
        {
            _cancellation.Cancel();
        }
    }

    public Task StopAsync() => _stopTask ??= StopCoreAsync();

    private async Task StopCoreAsync()
    {
        Task<SignedLoginCatalog?>? pending = _pending;
        _pending = null;
        Current = null;
        await ClientShutdown.CancelAndDrainAsync(_cancellation, async () =>
        {
            try
            {
                if (pending is not null)
                {
                    await pending.ConfigureAwait(false);
                }
            }
            catch (Exception failure) when (IsAcquisitionFailure(failure))
            {
                // A transport failure cannot install a catalog during shutdown.
            }
        }).ConfigureAwait(false);
    }

    private static bool IsAcquisitionFailure(Exception failure) =>
        failure is HttpRequestException or IOException or InvalidDataException or TimeoutException or OperationCanceledException;

    public void Dispose() => StopAsync().GetAwaiter().GetResult();

    private static DateTimeOffset NextRefresh(DateTimeOffset now, DateTimeOffset expiresAt)
    {
        DateTimeOffset earliest = now + s_minimumRefreshInterval;
        DateTimeOffset latest = now + s_refreshInterval;
        DateTimeOffset beforeExpiry = expiresAt - s_expiryLead;
        return beforeExpiry <= earliest ? earliest : beforeExpiry < latest ? beforeExpiry : latest;
    }
}
