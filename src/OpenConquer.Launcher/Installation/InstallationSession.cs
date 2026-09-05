namespace OpenConquer.Launcher.Installation;

/// <summary>Owns inspection transitions and rejects overlapping requests.</summary>
/// <remarks>The caller owns cancellation and must await the operation before releasing its resources.</remarks>
internal sealed class InstallationSession
{
    private readonly IInstallationInspector _inspector;
    private InstallationState _state = new InstallationState.Unselected();
    private int _inspectionInProgress;

    public InstallationSession(IInstallationInspector inspector)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        _inspector = inspector;
    }

    public InstallationState State => Volatile.Read(ref _state);

    public void ClearSelection()
    {
        if (Interlocked.CompareExchange(ref _inspectionInProgress, 1, 0) != 0)
        {
            throw new InvalidOperationException("An installation inspection is already in progress.");
        }

        try
        {
            if (State is not InstallationState.Unselected)
            {
                Volatile.Write(ref _state, new InstallationState.Unselected());
            }
        }
        finally
        {
            Interlocked.Exchange(ref _inspectionInProgress, 0);
        }
    }

    public async Task InspectAsync(string? path, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _inspectionInProgress, 1, 0) != 0)
        {
            throw new InvalidOperationException("An installation inspection is already in progress.");
        }

        try
        {
            if (!InstallationRoot.TryCreate(path, out InstallationRoot? root))
            {
                Volatile.Write(ref _state, new InstallationState.InvalidPath());
                return;
            }

            Volatile.Write(ref _state, new InstallationState.Checking(root));
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                InstallationInspection inspection = await _inspector.InspectAsync(root, cancellationToken).ConfigureAwait(false);
                // Cancellation wins over a worker result that raced the cancellation request.
                cancellationToken.ThrowIfCancellationRequested();
                InstallationState state = inspection switch
                {
                    InstallationInspection.Located located => new InstallationState.Located(root, located.AssemblyVersion),
                    InstallationInspection.Rejected rejected => new InstallationState.Rejected(root, rejected.Issue),
                    _ => throw new InvalidOperationException("The installation inspector returned an invalid outcome."),
                };
                Volatile.Write(ref _state, state);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Volatile.Write(ref _state, new InstallationState.Cancelled(root));
            }
            catch
            {
                Volatile.Write(ref _state, new InstallationState.Faulted(root));
                throw;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _inspectionInProgress, 0);
        }
    }
}
