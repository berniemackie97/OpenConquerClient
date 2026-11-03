namespace OpenConquer.Client.World;

internal static class WorldSceneryDepthOrder
{
    internal static void Insert(List<WorldScenerySprite> active, WorldScenerySprite incoming)
    {
        int cursor = -1;
        for (int i = 0; i < active.Count; i++)
        {
            WorldScenerySprite existing = active[i];
            if (Overlaps(incoming.TileX, incoming.FootprintWidth, existing.TileX, existing.FootprintWidth))
            {
                if (incoming.TileY < existing.TileY)
                {
                    InsertBefore(active, incoming, i, cursor);
                    return;
                }

                cursor = ScanForward(active, incoming, i);
            }
            else if (Overlaps(incoming.TileY, incoming.FootprintHeight, existing.TileY, existing.FootprintHeight))
            {
                if (incoming.TileX < existing.TileX)
                {
                    InsertBefore(active, incoming, i, cursor);
                    return;
                }

                cursor = ScanForward(active, incoming, i);
            }
        }

        if (cursor >= 0)
        {
            active.Insert(cursor + 1, incoming);
            return;
        }

        long sum = Sum(incoming);
        int position = 0;
        while (position < active.Count && sum >= Sum(active[position]))
        {
            position++;
        }

        active.Insert(position, incoming);
    }

    private static int ScanForward(List<WorldScenerySprite> active, WorldScenerySprite incoming, int pivot)
    {
        int cursor = pivot;
        long pivotSum = Sum(active[pivot]);
        long sum = Sum(incoming);
        for (int i = pivot + 1; i < active.Count; i++)
        {
            long candidate = Sum(active[i]);
            if (candidate != pivotSum && sum <= candidate)
            {
                break;
            }

            cursor = i;
        }

        return cursor;
    }

    private static void InsertBefore(List<WorldScenerySprite> active, WorldScenerySprite incoming,
        int pivot, int cursor)
    {
        int position = pivot;
        long pivotSum = Sum(active[pivot]);
        long sum = Sum(incoming);
        for (int i = pivot - 1; i >= 0; i--)
        {
            if (i <= cursor || (Sum(active[i]) != pivotSum && sum >= Sum(active[i])))
            {
                break;
            }

            position = i;
        }

        active.Insert(position, incoming);
    }

    private static bool Overlaps(int a, int aWidth, int b, int bWidth) =>
        (long)a - aWidth < b && (long)b - bWidth < a;

    private static long Sum(WorldScenerySprite sprite) => (long)sprite.TileX + sprite.TileY;
}
