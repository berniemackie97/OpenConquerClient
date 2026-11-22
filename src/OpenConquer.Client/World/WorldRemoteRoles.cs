using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal sealed class WorldRemoteRoles
{
    internal const int MaximumTrackedRoles = 4096;
    private const int VisibilityRadius = 18;
    private readonly Dictionary<uint, RemoteRole> _roles = [];
    private GamePlayerSpawn10014[]? _snapshot;

    internal int Count => _roles.Count;
    internal int Revision
    {
        get;
        private set;
    }
    internal IReadOnlyCollection<GamePlayerSpawn10014> All => _snapshot ??= [.. _roles.Values.Select(static role => role.Player)];

    internal bool Contains(uint entityId)
    {
        return _roles.ContainsKey(entityId);
    }

    internal bool TryGet(uint entityId, out GamePlayerSpawn10014? player)
    {
        if (_roles.TryGetValue(entityId, out RemoteRole? role))
        {
            player = role.Player;
            return true;
        }

        player = null;
        return false;
    }

    internal IEnumerable<(uint EntityId, WorldRolePlayback Playback)> Placements
    {
        get
        {
            foreach ((uint id, RemoteRole role) in _roles)
            {
                if (role.Playback.IsProjected)
                {
                    yield return (id, role.Playback);
                }
            }
        }
    }

    internal WorldRolePlayback? GetPlayback(uint entityId)
    {
        return _roles.TryGetValue(entityId, out RemoteRole? role) ? role.Playback : null;
    }

    internal void Upsert(GamePlayerSpawn10014 player)
    {
        if (_roles.Count >= MaximumTrackedRoles && !_roles.ContainsKey(player.EntityId))
        {
            throw new InvalidDataException("The visible-player registry exceeded its configured capacity.");
        }

        _roles[player.EntityId] = new RemoteRole(player);
        _snapshot = null;
        Revision = unchecked(Revision + 1);
    }

    internal bool ApplyWalk(GameWalk10005 walk)
    {
        if (!_roles.TryGetValue(walk.EntityId, out RemoteRole? role))
        {
            return false;
        }

        GamePlayerSpawn10014 player = role.Player;
        (int dx, int dy) = walk.GetDelta();
        int x = player.X + dx;
        int y = player.Y + dy;

        if ((uint)x > ushort.MaxValue || (uint)y > ushort.MaxValue)
        {
            return false;
        }

        role.Player = player with
        {
            X = (ushort)x,
            Y = (ushort)y,
            Direction = (byte)(walk.DirectionIndex % 8)
        };

        role.Playback.Enqueue(walk);

        _snapshot = null;
        Revision = unchecked(Revision + 1);
        return true;
    }

    internal bool ApplyGroundJump(uint entityId, ushort targetX, ushort targetY)
    {
        if (!_roles.TryGetValue(entityId, out RemoteRole? role))
        {
            return false;
        }

        GamePlayerSpawn10014 player = role.Player;
        role.Playback.EnqueueJump(targetX, targetY);

        if (player.X == targetX && player.Y == targetY)
        {
            return true;
        }

        role.Player = player with
        {
            X = targetX,
            Y = targetY
        };

        _snapshot = null;
        Revision = unchecked(Revision + 1);

        return true;
    }

    internal void Advance(uint nowMilliseconds, Func<uint, ushort, int> frameCount, Func<int, int, (int X, int Y)> project, Func<int, int, (int X, int Y)> unproject)
    {
        ArgumentNullException.ThrowIfNull(frameCount);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(unproject);

        foreach ((uint id, RemoteRole role) in _roles)
        {
            bool projected = role.Playback.IsProjected;
            (int X, int Y) depth = role.Playback.DepthTile;
            role.Playback.Advance(nowMilliseconds, action => frameCount(id, action), project, unproject);
            if (!projected || role.Playback.DepthTile != depth)
            {
                Revision = unchecked(Revision + 1);
            }
        }
    }

    internal bool Remove(uint entityId)
    {
        if (!_roles.Remove(entityId))
        {
            return false;
        }

        _snapshot = null;
        Revision = unchecked(Revision + 1);
        return true;
    }

    internal void Cull(int heroX, int heroY)
    {
        List<uint>? outside = null;
        foreach ((uint id, RemoteRole role) in _roles)
        {
            GamePlayerSpawn10014 player = role.Player;
            if (Math.Abs((long)heroX - player.X) > VisibilityRadius || Math.Abs((long)heroY - player.Y) > VisibilityRadius)
            {
                (outside ??= []).Add(id);
            }
        }

        if (outside is null)
        {
            return;
        }

        {
            foreach (uint id in outside)
            {
                Remove(id);
            }
        }
    }

    private sealed class RemoteRole(GamePlayerSpawn10014 player)
    {
        internal GamePlayerSpawn10014 Player = player;
        internal WorldRolePlayback Playback { get; } = new(player.X, player.Y, (byte)(player.Direction % 8));
    }
}
