namespace OpenConquer.Client.World;

internal sealed class WorldSceneryDepthOrder
{
    private readonly List<Entry> _active = [];

    internal void Insert(WorldScenerySprite sprite, bool terrain = false)
    {
        if (!terrain)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                Entry parent = _active[i];
                if (!parent.Terrain || !Contains(parent.Sprite, sprite))
                {
                    continue;
                }

                InsertChild(parent.Children ??= [], sprite);
                _active[i] = parent;
                return;
            }
        }

        Entry incoming = new(sprite, terrain);
        int cursor = -1;
        for (int i = 0; i < _active.Count; i++)
        {
            WorldScenerySprite existing = _active[i].Sprite;
            if (Overlaps(sprite.TileX, sprite.FootprintWidth, existing.TileX, existing.FootprintWidth))
            {
                if (sprite.TileY < existing.TileY)
                {
                    InsertBefore(incoming, i, cursor);
                    return;
                }

                cursor = ScanForward(sprite, i);
            }
            else if (Overlaps(sprite.TileY, sprite.FootprintHeight, existing.TileY, existing.FootprintHeight))
            {
                if (sprite.TileX < existing.TileX)
                {
                    InsertBefore(incoming, i, cursor);
                    return;
                }

                cursor = ScanForward(sprite, i);
            }
        }

        if (cursor >= 0)
        {
            _active.Insert(cursor + 1, incoming);
            return;
        }

        long sum = Sum(sprite);
        int position = 0;
        while (position < _active.Count && sum >= Sum(_active[position].Sprite))
        {
            position++;
        }

        _active.Insert(position, incoming);
    }

    internal IReadOnlyList<WorldScenerySprite> ToDrawList()
    {
        List<WorldScenerySprite> sprites = new(_active.Count);
        foreach (Entry entry in _active)
        {
            sprites.Add(entry.Sprite);
            if (entry.Children is { } children)
            {
                sprites.AddRange(children);
            }
        }

        return sprites;
    }

    private int ScanForward(WorldScenerySprite incoming, int pivot)
    {
        int cursor = pivot;
        long pivotSum = Sum(_active[pivot].Sprite);
        long sum = Sum(incoming);
        for (int i = pivot + 1; i < _active.Count; i++)
        {
            long candidate = Sum(_active[i].Sprite);
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
        long pivotSum = Sum(_active[pivot].Sprite);
        long sum = Sum(incoming.Sprite);
        for (int i = pivot - 1; i >= 0; i--)
        {
            if (i <= cursor || (Sum(_active[i].Sprite) != pivotSum && sum >= Sum(_active[i].Sprite)))
            {
                break;
            }

            position = i;
        }

        _active.Insert(position, incoming);
    }

    private static void InsertChild(List<WorldScenerySprite> children, WorldScenerySprite incoming)
    {
        for (int i = 0; i < children.Count; i++)
        {
            WorldScenerySprite existing = children[i];
            bool before = Overlaps(incoming.TileX, incoming.FootprintWidth, existing.TileX, existing.FootprintWidth)
                ? incoming.TileY < existing.TileY
                : Overlaps(incoming.TileY, incoming.FootprintHeight, existing.TileY, existing.FootprintHeight)
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

    private static bool Contains(WorldScenerySprite parent, WorldScenerySprite sprite) =>
        (long)parent.TileX - parent.FootprintWidth < sprite.TileX && sprite.TileX <= parent.TileX &&
        (long)parent.TileY - parent.FootprintHeight < sprite.TileY && sprite.TileY <= parent.TileY;

    private static bool Overlaps(int a, int aWidth, int b, int bWidth) =>
        (long)a - aWidth < b && (long)b - bWidth < a;

    private static long Sum(WorldScenerySprite sprite) => (long)sprite.TileX + sprite.TileY;

    private struct Entry
    {
        internal Entry(WorldScenerySprite sprite, bool terrain)
        {
            Sprite = sprite;
            Terrain = terrain;
        }

        internal readonly WorldScenerySprite Sprite;
        internal readonly bool Terrain;
        internal List<WorldScenerySprite>? Children;
    }
}
