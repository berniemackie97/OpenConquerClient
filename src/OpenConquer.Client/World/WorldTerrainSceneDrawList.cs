using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquer.Client.World;

internal readonly record struct WorldTerrainSprite(
    AniIndexSection Animation, int FrameIndex, int X, int Y,
    int ScaleNumerator, int ScaleDenominator,
    int TileX, int TileY, int FootprintWidth, int FootprintHeight)
{
    internal bool IntersectsViewport(int width, int height, int screenWidth, int screenHeight) =>
        width > 0 && height > 0 && (long)X + width > 0 && (long)Y + height > 0 &&
        X < screenWidth && Y < screenHeight;
}

internal static class WorldTerrainSceneDrawList
{
    public static IReadOnlyList<WorldTerrainSprite> Build(GameMapContent map,
        IReadOnlyList<GameMapTerrainScenePlacement> placements, WorldPuzzleProjection projection,
        uint nowMilliseconds, uint sceneStartMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(placements);
        if (projection.ScreenWidth <= 0 || projection.ScreenHeight <= 0 ||
            projection.ScaleNumerator <= 0 || projection.ScaleDenominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(projection));
        }

        List<WorldTerrainSprite> sprites = [];

        foreach (GameMapTerrainScenePlacement placement in placements)
        {
            int groupX = placement.Scenery.TileX!.Value;
            int groupY = placement.Scenery.TileY!.Value;

            foreach (GameMapTerrainObjectBinding binding in placement.Objects)
            {
                GameMapTerrainObject terrain = binding.Terrain;
                int tileX = checked(groupX + terrain.AnchorOffsetX);
                int tileY = checked(groupY + terrain.AnchorOffsetY);
                (int worldX, int worldY) = WorldCameraProjection.ProjectTile(map, tileX, tileY);

                long x = (long)worldX - projection.CenterX + terrain.RenderOffsetX;
                long y = (long)worldY - projection.CenterY + terrain.RenderOffsetY;
                x = (long)projection.ScaleNumerator * (x - projection.ScreenWidth / 2) /
                    projection.ScaleDenominator + projection.ScreenWidth / 2;
                y = (long)projection.ScaleNumerator * (y - projection.ScreenHeight / 2) /
                    projection.ScaleDenominator + projection.ScreenHeight / 2;

                uint interval = (uint)Math.Max(1, terrain.FrameIntervalMilliseconds);
                int frameIndex = (int)((unchecked(nowMilliseconds - sceneStartMilliseconds) / interval) %
                    (uint)binding.Animation.FrameCount);

                InsertByDepth(sprites, new WorldTerrainSprite(binding.Animation, frameIndex,
                    checked((int)x), checked((int)y), projection.ScaleNumerator, projection.ScaleDenominator,
                    tileX, tileY, terrain.GridWidth, terrain.GridHeight));
            }
        }

        return sprites;
    }

    private static void InsertByDepth(List<WorldTerrainSprite> sorted, WorldTerrainSprite item)
    {
        int cursor = -1;
        for (int i = 0; i < sorted.Count; i++)
        {
            WorldTerrainSprite existing = sorted[i];
            if (OverlapsX(item, existing))
            {
                if (item.TileY < existing.TileY)
                {
                    InsertBefore(sorted, item, i, cursor);
                    return;
                }

                cursor = ScanForward(sorted, item, i);
            }
            else if (OverlapsY(item, existing))
            {
                if (item.TileX < existing.TileX)
                {
                    InsertBefore(sorted, item, i, cursor);
                    return;
                }

                cursor = ScanForward(sorted, item, i);
            }
        }

        if (cursor >= 0)
        {
            sorted.Insert(cursor + 1, item);
            return;
        }

        long sum = Sum(item);
        int position = 0;
        while (position < sorted.Count && sum >= Sum(sorted[position]))
        {
            position++;
        }

        sorted.Insert(position, item);
    }

    private static int ScanForward(List<WorldTerrainSprite> sorted, WorldTerrainSprite item, int pivotIndex)
    {
        int cursor = pivotIndex;
        long pivotSum = Sum(sorted[pivotIndex]);
        long itemSum = Sum(item);
        for (int j = pivotIndex + 1; j < sorted.Count; j++)
        {
            long sum = Sum(sorted[j]);
            if (sum != pivotSum && itemSum <= sum)
            {
                break;
            }

            cursor = j;
        }

        return cursor;
    }

    private static void InsertBefore(List<WorldTerrainSprite> sorted, WorldTerrainSprite item,
        int existingIndex, int cursor)
    {
        int position = existingIndex;
        long existingSum = Sum(sorted[existingIndex]);
        for (int k = existingIndex - 1; k >= 0; k--)
        {
            if (k <= cursor || (Sum(sorted[k]) != existingSum && Sum(item) >= Sum(sorted[k])))
            {
                break;
            }

            position = k;
        }

        sorted.Insert(position, item);
    }

    private static bool OverlapsX(WorldTerrainSprite a, WorldTerrainSprite b) =>
        (long)a.TileX - a.FootprintWidth < b.TileX &&
        (long)b.TileX - b.FootprintWidth < a.TileX;

    private static bool OverlapsY(WorldTerrainSprite a, WorldTerrainSprite b) =>
        (long)a.TileY - a.FootprintHeight < b.TileY &&
        (long)b.TileY - b.FootprintHeight < a.TileY;

    private static long Sum(WorldTerrainSprite sprite) => (long)sprite.TileX + sprite.TileY;
}
