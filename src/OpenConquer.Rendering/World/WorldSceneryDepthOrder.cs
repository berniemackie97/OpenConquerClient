namespace OpenConquer.Rendering.World;

internal sealed class WorldSceneryDepthOrder
{
    private readonly List<Entry> _active = [];

    internal void Insert(WorldScenerySprite sprite, bool terrain = false) =>
        Insert(new Entry(sprite, terrain));

    internal void InsertRole(int tileX, int tileY) =>
        InsertRole(new WorldSceneRole(0, tileX, tileY, 0));

    internal void InsertRole(WorldSceneRole role) => Insert(new Entry(role));

    private void Insert(Entry incoming)
    {
        if (!incoming.Terrain)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                Entry parent = _active[i];
                if (!parent.Terrain || !Contains(parent, incoming))
                {
                    continue;
                }

                InsertChild(parent.Children ??= [], incoming);
                _active[i] = parent;
                return;
            }
        }

        int cursor = -1;
        for (int i = 0; i < _active.Count; i++)
        {
            Entry existing = _active[i];
            if (Overlaps(incoming.TileX, incoming.Width, existing.TileX, existing.Width))
            {
                if (incoming.TileY < existing.TileY)
                {
                    InsertBefore(incoming, i, cursor);
                    return;
                }

                cursor = ScanForward(incoming, i);
            }
            else if (Overlaps(incoming.TileY, incoming.Height, existing.TileY, existing.Height))
            {
                if (incoming.TileX < existing.TileX)
                {
                    InsertBefore(incoming, i, cursor);
                    return;
                }

                cursor = ScanForward(incoming, i);
            }
        }

        if (cursor >= 0)
        {
            _active.Insert(cursor + 1, incoming);
            return;
        }

        int sum = Sum(incoming);
        int position = 0;
        while (position < _active.Count && sum >= Sum(_active[position]))
        {
            position++;
        }

        _active.Insert(position, incoming);
    }

    internal IReadOnlyList<WorldScenerySprite> ToDrawList() => ToDrawList(out _);

    internal IReadOnlyList<WorldScenerySprite> ToDrawList(out int roleIndex)
    {
        IReadOnlyList<WorldScenerySprite> sprites = ToDrawListWithRoles(out IReadOnlyList<WorldRoleDrawSlot> slots);
        roleIndex = slots.Count == 0 ? -1 : slots[^1].SpriteIndex;
        return sprites;
    }

    internal IReadOnlyList<WorldScenerySprite> ToDrawListWithRoles(out IReadOnlyList<WorldRoleDrawSlot> roles)
    {
        List<WorldScenerySprite> sprites = new(_active.Count);
        List<WorldRoleDrawSlot> markers = [];
        foreach (Entry entry in _active)
        {
            Append(entry);
            if (entry.Children is { } children)
            {
                foreach (Entry child in children)
                {
                    Append(child);
                }
            }
        }

        roles = markers;
        return sprites;

        void Append(Entry entry)
        {
            if (entry.Role is { } role)
            {
                markers.Add(new WorldRoleDrawSlot(sprites.Count, role));
            }
            else
            {
                sprites.Add(entry.Sprite!.Value);
            }
        }
    }

    private int ScanForward(Entry incoming, int pivot)
    {
        int cursor = pivot;
        int pivotSum = Sum(_active[pivot]);
        int sum = Sum(incoming);
        for (int i = pivot + 1; i < _active.Count; i++)
        {
            int candidate = Sum(_active[i]);
            if (candidate != pivotSum && sum <= candidate)
            {
                break;
            }

            cursor = i;
        }

        return cursor;
    }

    private void InsertBefore(Entry incoming, int pivot, int cursor)
    {
        int position = pivot;
        int pivotSum = Sum(_active[pivot]);
        int sum = Sum(incoming);
        for (int i = pivot - 1; i >= 0; i--)
        {
            if (i <= cursor || (Sum(_active[i]) != pivotSum && sum >= Sum(_active[i])))
            {
                break;
            }

            position = i;
        }

        _active.Insert(position, incoming);
    }

    private static void InsertChild(List<Entry> children, Entry incoming)
    {
        for (int i = 0; i < children.Count; i++)
        {
            Entry existing = children[i];
            bool before = Overlaps(incoming.TileX, incoming.Width, existing.TileX, existing.Width)
                ? incoming.TileY < existing.TileY
                : Overlaps(incoming.TileY, incoming.Height, existing.TileY, existing.Height)
                    ? incoming.TileX < existing.TileX
                    : Sum(incoming) < Sum(existing);
            if (before)
            {
                children.Insert(i, incoming);
                return;
            }
        }

        children.Add(incoming);
    }

    private static bool Contains(Entry parent, Entry entry) =>
        unchecked(parent.TileX - parent.Width) < entry.TileX && entry.TileX <= parent.TileX &&
        unchecked(parent.TileY - parent.Height) < entry.TileY && entry.TileY <= parent.TileY;

    // Native 0x635C5E..0x635C79 uses wrapped subtractions, INC and signed TEST/JG.
    private static bool Overlaps(int a, int aWidth, int b, int bWidth) =>
        unchecked(a - aWidth - b + 1) <= 0 && unchecked(b - bWidth - a + 1) <= 0;

    // Signed wrapping ADD/CMP: 0x635E62..0x635E6B. Footprint subtraction above: 0x644E1A/0x644E1D.
    private static int Sum(Entry entry) => unchecked(entry.TileX + entry.TileY);

    private struct Entry
    {
        internal Entry(WorldScenerySprite sprite, bool terrain)
        {
            Sprite = sprite;
            TileX = sprite.TileX;
            TileY = sprite.TileY;
            Width = sprite.FootprintWidth;
            Height = sprite.FootprintHeight;
            Terrain = terrain;
            Role = null;
        }

        internal Entry(WorldSceneRole role)
        {
            Role = role;
            TileX = role.TileX;
            TileY = role.TileY;
            Width = 1;
            Height = 1;
        }

        internal readonly WorldScenerySprite? Sprite;
        internal readonly int TileX;
        internal readonly int TileY;
        internal readonly int Width;
        internal readonly int Height;
        internal readonly bool Terrain;
        internal readonly WorldSceneRole? Role;
        internal List<Entry>? Children;
    }
}
