using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal sealed class WorldRemoteRoles
{
    internal const int MaximumTrackedRoles = 4096;
    private const int VisibilityRadius = 18;
    private readonly Dictionary<uint, GamePlayerSpawn10014> _roles = [];

    internal int Count => _roles.Count;
    internal IReadOnlyCollection<GamePlayerSpawn10014> All => _roles.Values;

    internal bool Contains(uint entityId) => _roles.ContainsKey(entityId);

    internal bool TryGet(uint entityId, out GamePlayerSpawn10014? player) =>
        _roles.TryGetValue(entityId, out player);

    internal void Upsert(GamePlayerSpawn10014 player)
    {
        if (_roles.Count >= MaximumTrackedRoles && !_roles.ContainsKey(player.EntityId))
        {
            throw new InvalidDataException("The visible-player registry exceeded its configured capacity.");
        }

        _roles[player.EntityId] = player;
    }

    internal bool Remove(uint entityId) => _roles.Remove(entityId);

    internal void Cull(int heroX, int heroY)
    {
        if (heroX <= 0 || heroY <= 0)
        {
            return;
        }

        List<uint>? outside = null;
        foreach ((uint id, GamePlayerSpawn10014 player) in _roles)
        {
            if (Math.Abs((long)heroX - player.X) > VisibilityRadius ||
                Math.Abs((long)heroY - player.Y) > VisibilityRadius)
            {
                (outside ??= []).Add(id);
            }
        }

        if (outside is not null)
        {
            foreach (uint id in outside)
            {
                _roles.Remove(id);
            }
        }
    }
}
