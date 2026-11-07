namespace OpenConquer.Client.World;

/// <summary>
/// Owns the admitted world's appearance preparation. The frame thread polls completed CPU work and submits at
/// most one request. Equipment changes during a load are represented by the next poll's desired sources.
/// </summary>
internal sealed class WorldRoleLoad(Func<WorldRoleSources, CancellationToken, WorldRoleAssets?> prepare) : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private Task<WorldRoleAssets?>? _pending;
    private WorldRoleSources _requested;

    internal bool TryPrepare(WorldRoleSources desired, WorldRoleSources? installed, out WorldRoleAssets? assets)
    {
        assets = null;
        if (_pending is { IsCompleted: true } completed)
        {
            _pending = null;
            bool wanted = desired == _requested && desired != installed;
            try
            {
                WorldRoleAssets? ready = completed.GetAwaiter().GetResult();
                if (wanted)
                {
                    assets = ready;
                    return true;
                }
            }
            catch (Exception exception) when (!wanted && IsContentFailure(exception))
            {
                // A failed stale appearance is irrelevant to the current equipment.
            }
        }

        if (_pending is null && desired != installed)
        {
            _requested = desired;
            CancellationToken token = _stop.Token;
            _pending = Task.Run(() => prepare(desired, token), token);
        }

        return false;
    }

    internal void Cancel() => _stop.Cancel();

    public async ValueTask DisposeAsync()
    {
        Cancel();
        try
        {
            if (_pending is { } pending)
            {
                await pending.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // World shutdown cancels preparation between content operations.
        }
        catch (Exception exception) when (IsContentFailure(exception))
        {
            // The closing world will never install this appearance.
        }
        finally
        {
            _pending = null;
            _stop.Dispose();
        }
    }

    internal static bool IsContentFailure(Exception exception) =>
        exception is IOException or InvalidDataException or UnauthorizedAccessException;
}
