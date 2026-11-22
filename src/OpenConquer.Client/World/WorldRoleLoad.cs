namespace OpenConquer.Client.World;

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
            }
        }

        if (_pending is not null || desired == installed)
        {
            return false;
        }

        _requested = desired;
        CancellationToken token = _stop.Token;
        _pending = Task.Run(() => prepare(desired, token), token);

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
            }
        }).ConfigureAwait(false);
    }

    internal static bool IsContentFailure(Exception exception)
    {
        return exception is IOException or InvalidDataException or UnauthorizedAccessException;
    }
}
