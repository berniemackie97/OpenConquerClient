using System.Runtime.ExceptionServices;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher;

/// <summary>Owns launcher startup evaluation and process-lifetime cancellation.</summary>
internal sealed class LauncherApplication : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly IManagedInstallationResolver _installationResolver;

    private LauncherState _state = new LauncherState.Starting();
    private CancellationTokenSource? _lifetimeCancellation;
    private Task? _evaluation;
    private bool _started;
    private bool _stopping;

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
            if (_started)
            {
                throw new InvalidOperationException("Launcher application startup may run only once.");
            }

            if (_stopping)
            {
                throw new InvalidOperationException("Launcher application shutdown has started.");
            }

            _started = true;
            _lifetimeCancellation = new CancellationTokenSource();

            PublishLocked(new LauncherState.EvaluatingInstallation());

            _evaluation = EvaluateAsync(_lifetimeCancellation.Token);
            return _evaluation;
        }
    }

    public async Task StopAsync()
    {
        Task? evaluation;
        CancellationTokenSource? lifetimeCancellation;
        bool shutdownCancellationRequested;
        Exception? failure = null;

        lock (_gate)
        {
            if (_stopping)
            {
                evaluation = _evaluation;
                lifetimeCancellation = null;
                shutdownCancellationRequested = true;
            }
            else
            {
                _stopping = true;

                PublishLocked(new LauncherState.Stopping());

                evaluation = _evaluation;
                lifetimeCancellation = _lifetimeCancellation;
                shutdownCancellationRequested = lifetimeCancellation is not null;
            }
        }

        lifetimeCancellation?.Cancel();

        if (evaluation is not null)
        {
            try
            {
                await evaluation.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (shutdownCancellationRequested)
            {
                // Shutdown owns cancellation. Normal application teardown is not a fault.
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }

        lock (_gate)
        {
            if (_state is not LauncherState.Stopped)
            {
                PublishLocked(new LauncherState.Stopped());
            }

            _lifetimeCancellation?.Dispose();
            _lifetimeCancellation = null;
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(StopAsync());
    }

    private async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        try
        {
            ManagedInstallationResolution resolution = await _installationResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);

            LauncherState resolvedState = resolution switch
            {
                ManagedInstallationResolution.Resolved resolved => new LauncherState.InstallationResolved(resolved.Installation),
                ManagedInstallationResolution.Rejected rejected => new LauncherState.InstallationUnavailable(rejected.Issue),

                _ => throw new InvalidOperationException("The managed installation resolver returned an invalid result."),
            };

            PublishEvaluationResult(resolvedState, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            PublishEvaluationFailure();
            throw;
        }
    }

    private void PublishEvaluationResult(LauncherState state, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_stopping)
            {
                return;
            }

            PublishLocked(state);
        }
    }

    private void PublishEvaluationFailure()
    {
        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }

            PublishLocked(new LauncherState.Faulted());
        }
    }

    private void PublishLocked(LauncherState state)
    {
        Volatile.Write(ref _state, state);
    }
}
