using System.Numerics;
using OpenConquer.Client.Diagnostics;
using OpenConquer.Networking.Game.World;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Client.World;

internal readonly record struct WorldRoleAppearance(uint BodyFamily, WorldRoleSources Sources)
{
    internal static WorldRoleAppearance From(GamePlayerSpawn10014 player) =>
        new(player.PackedAppearance % 1000,
            WorldRoleSources.Resolve(player.PackedAppearance, player.HairComposite,
                player.HeadwearItemType, player.ArmorItemType, player.RightHandItemType,
                player.LeftHandItemType));
}

internal sealed class WorldRemoteRolePresentation(
    Func<WorldRoleAppearance, CancellationToken, WorldRoleAssets?> prepare,
    ClientDiagnostics diagnostics) : IAsyncDisposable
{
    internal const int MaximumPreparedAppearances = 64;

    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<WorldRoleAppearance, WorldRoleRenderer> _renderers = [];
    private readonly HashSet<WorldRoleAppearance> _failures = [];
    private Dictionary<uint, WorldRoleAppearance> _roles = [];
    private List<WorldRoleAppearance> _priority = [];
    private Task<WorldRoleAssets?>? _pending;
    private WorldRoleAppearance _pendingKey;
    private Task? _disposeTask;
    private int _revision = int.MinValue;
    private int _heroX = int.MinValue;
    private int _heroY = int.MinValue;

    internal WorldRemoteRolePresentation(WorldRoleAssetReader reader, ClientDiagnostics diagnostics)
        : this((appearance, token) => reader.Load(appearance.BodyFamily, appearance.Sources, token),
            diagnostics)
    {
    }

    internal int SelectedRoleCount => _roles.Count;
    internal int LoadedAppearanceCount => _renderers.Count;

    internal bool Includes(uint entityId) => _roles.ContainsKey(entityId);

    internal void Synchronize(WorldRemoteRoles roles, int heroX, int heroY)
    {
        ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
        if (_revision == roles.Revision && _heroX == heroX && _heroY == heroY)
        {
            return;
        }

        _revision = roles.Revision;
        _heroX = heroX;
        _heroY = heroY;

        List<GamePlayerSpawn10014> nearest = [.. roles.All];
        nearest.Sort((left, right) =>
        {
            long dx = (long)left.X - heroX;
            long dy = (long)left.Y - heroY;
            long leftDistance = dx * dx + dy * dy;
            dx = (long)right.X - heroX;
            dy = (long)right.Y - heroY;
            long rightDistance = dx * dx + dy * dy;
            int order = leftDistance.CompareTo(rightDistance);
            return order != 0 ? order : left.EntityId.CompareTo(right.EntityId);
        });

        HashSet<WorldRoleAppearance> chosen = [];
        Dictionary<uint, WorldRoleAppearance> identities = new(nearest.Count);
        List<WorldRoleAppearance> priority = [];
        foreach (GamePlayerSpawn10014 player in nearest)
        {
            WorldRoleAppearance appearance = WorldRoleAppearance.From(player);
            if (!chosen.Contains(appearance) && chosen.Count >= MaximumPreparedAppearances)
            {
                continue;
            }

            if (chosen.Add(appearance))
            {
                priority.Add(appearance);
            }

            identities.Add(player.EntityId, appearance);
        }

        _roles = identities;
        _priority = priority;
        _failures.RemoveWhere(key => !chosen.Contains(key));

        ClientShutdown cleanup = new();
        foreach ((WorldRoleAppearance key, WorldRoleRenderer renderer) in _renderers.ToArray())
        {
            if (chosen.Contains(key))
            {
                continue;
            }

            _renderers.Remove(key);
            cleanup.Run(renderer.Dispose);
        }

        cleanup.ThrowIfFailed();
    }

    internal void Prepare(OpenGLGraphicsDevice device)
    {
        ObjectDisposedException.ThrowIf(_disposeTask is not null, this);

        if (_pending is { IsCompleted: true } completed)
        {
            _pending = null;
            bool wanted = _priority.Contains(_pendingKey);
            try
            {
                WorldRoleAssets? assets = completed.GetAwaiter().GetResult();
                if (wanted)
                {
                    if (assets is null)
                    {
                        _failures.Add(_pendingKey);
                        diagnostics.WorldRoleMissing();
                    }
                    else
                    {
                        _renderers.Add(_pendingKey, WorldRoleRenderer.Create(device, assets));
                    }
                }
            }
            catch (Exception exception) when (WorldRoleLoad.IsContentFailure(exception))
            {
                if (wanted)
                {
                    _failures.Add(_pendingKey);
                    diagnostics.WorldRoleUnavailable(ClientFailure.Classify(exception));
                }
            }
        }

        if (_pending is not null)
        {
            return;
        }

        foreach (WorldRoleAppearance appearance in _priority)
        {
            if (_renderers.ContainsKey(appearance) || _failures.Contains(appearance))
            {
                continue;
            }

            _pendingKey = appearance;
            CancellationToken token = _stop.Token;
            _pending = Task.Run(() => prepare(appearance, token), token);
            break;
        }
    }

    internal void Draw(OpenGLRenderer renderer, uint entityId, Matrix4x4 main, Matrix4x4 shadow,
        uint milliseconds)
    {
        if (_roles.TryGetValue(entityId, out WorldRoleAppearance appearance) &&
            _renderers.TryGetValue(appearance, out WorldRoleRenderer? parts))
        {
            parts.Draw(renderer, main, shadow, milliseconds);
        }
    }

    internal void ReleaseGraphics()
    {
        WorldRoleRenderer[] renderers = [.. _renderers.Values];
        _renderers.Clear();
        ClientShutdown cleanup = new();
        foreach (WorldRoleRenderer renderer in renderers)
        {
            cleanup.Run(renderer.Dispose);
        }

        cleanup.ThrowIfFailed();
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
            if (pending is null)
            {
                return;
            }

            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
            catch (Exception exception) when (WorldRoleLoad.IsContentFailure(exception))
            {
            }
        }).ConfigureAwait(false);
    }
}
