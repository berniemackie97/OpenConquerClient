using System.Runtime.ExceptionServices;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher;

/// <summary>Owns installation evaluation, recovery, and process-lifetime cancellation.</summary>
internal sealed class LauncherApplication : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly IManagedInstallationResolver _installationResolver;
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    private LauncherState _state = new LauncherState.Starting();
    private Task? _evaluation;
    private Task? _shutdown;

    public LauncherApplication(IManagedInstallationResolver installationResolver)
    {
        ArgumentNullException.ThrowIfNull(installationResolver);
        _installationResolver = installationResolver;
    }

    public LauncherState State => Volatile.Read(ref _state);

    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_state is not LauncherState.Starting)
            {
                throw new InvalidOperationException("Launcher startup requires an unstarted application.");
            }

            return BeginEvaluationLocked();
        }
    }

    /// <summary>Rechecks an unavailable installation; simultaneous requests share the active check.</summary>
    public Task RetryInstallationAsync()
    {
        lock (_gate)
        {
            if (_shutdown is not null)
            {
                throw new InvalidOperationException("Launcher shutdown has started.");
            }

            if (_evaluation is { IsCompleted: false })
            {
                return _evaluation;
            }

            if (_state is not LauncherState.InstallationUnavailable)
            {
                throw new InvalidOperationException("Only an unavailable installation can be retried.");
            }

            return BeginEvaluationLocked();
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

            PublishLocked(new LauncherState.Stopping());

            Task cancellation = _lifetimeCancellation.CancelAsync();
            _shutdown = DrainAsync(_evaluation, cancellation);
            return _shutdown;
        }
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(StopAsync());
    }

    private Task BeginEvaluationLocked()
    {
        PublishLocked(new LauncherState.EvaluatingInstallation());
        CancellationToken token = _lifetimeCancellation.Token;

        _evaluation = Task.Run(() => EvaluateAsync(token), CancellationToken.None);
        return _evaluation;
    }

    private async Task DrainAsync(Task? evaluation, Task cancellation)
    {
        Exception? failure = null;
        CancellationToken token = _lifetimeCancellation.Token;

        try
        {
            try
            {
                await cancellation.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // A broken cancellation callback must not skip evaluation drain or disposal.
                failure = exception;
            }

            if (evaluation is not null)
            {
                try
                {
                    await evaluation.ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when (token.IsCancellationRequested && exception.CancellationToken == token)
                {
                    // Only cancellation owned by this lifecycle is normal shutdown.
                }
                catch (Exception exception)
                {
                    failure = failure is null ? exception : new AggregateException(failure, exception);
                }
            }
        }
        finally
        {
            _lifetimeCancellation.Dispose();
            lock (_gate)
            {
                PublishLocked(new LauncherState.Stopped());
            }
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ManagedInstallationResolution resolution = await _installationResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);

            LauncherState resolvedState = resolution switch
            {
                ManagedInstallationResolution.Resolved resolved => new LauncherState.InstallationResolved(resolved.Installation),
                ManagedInstallationResolution.Rejected rejected => new LauncherState.InstallationUnavailable(rejected.Issue),
                _ => throw new InvalidOperationException("The managed installation resolver returned an invalid result."),
            };

            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PublishLocked(resolvedState);
            }
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested && exception.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                if (_state is not (LauncherState.Stopping or LauncherState.Stopped))
                {
                    PublishLocked(new LauncherState.Faulted());
                }
            }

            if (exception is OperationCanceledException)
            {
                throw new InvalidOperationException("The installation check was canceled outside launcher shutdown.", exception);
            }

            throw;
        }
    }

    private void PublishLocked(LauncherState state)
    {
        Volatile.Write(ref _state, state);
    }
}
