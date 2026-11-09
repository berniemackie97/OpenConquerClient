namespace OpenConquer.Client.World;

/// <summary>
/// Owns the admitted world's appearance preparation. The frame thread polls completed CPU work and submits at
/// most one request. Equipment changes during a load are represented by the next poll's desired sources.
/// </summary>
internal sealed class WorldRoleLoad(Func<WorldRoleSources, CancellationToken, WorldRoleAssets?> prepare) : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private Task<WorldRoleAssets?>? _pending;
    private Task? _disposeTask;
    private WorldRoleSources _requested;

    internal WorldRoleLoad(WorldRoleAssetReader reader, uint packedAppearance)
        : this((sources, token) => reader.Load(packedAppearance, sources, token))
    {
    }

    internal bool TryPrepare(WorldRoleSources desired, WorldRoleSources? installed, out WorldRoleAssets? assets)
    {
        ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
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

    internal void Cancel()
    {
        if (_disposeTask is null)
        {
            _stop.Cancel();
        }
    }

    public ValueTask DisposeAsync() => new(_disposeTask ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync()
    {
        Task<WorldRoleAssets?>? pending = _pending;
        _pending = null;
        await ClientShutdown.CancelAndDrainAsync(_stop, async () =>
        {
            try
            {
                if (pending is not null)
                {
                    await pending.ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (IsContentFailure(exception))
            {
                // A closing world will never install this appearance.
            }
        }).ConfigureAwait(false);
    }

    internal static bool IsContentFailure(Exception exception) =>
        exception is IOException or InvalidDataException or UnauthorizedAccessException;
}
