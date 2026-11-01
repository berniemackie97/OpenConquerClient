using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tool.World;

internal readonly record struct WorldMapPuzzleValidationResult(int PuzzleCount, int TileAnimationCount, int FrameCount);

internal static class WorldMapPuzzleValidator
{
    public static WorldMapPuzzleValidationResult Validate(string clientRootPath, uint mapDataId)
    {
        PackagedClientContentSource source = PackagedClientContentSource.Open(clientRootPath);
        GameMapContent map = GameMapContent.Load(source, GameMapCatalog.Load(source), mapDataId);
        GameMapPuzzleResources resources = GameMapPuzzleResources.Load(source, map);

        HashSet<string> puzzlePaths = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> decodedFrames = new(StringComparer.OrdinalIgnoreCase);
        int animationCount = 0;

        void ValidateBitmap(string path)
        {
            if (!puzzlePaths.Add(path))
            {
                return;
            }

            GameMapPuzzleBitmap bitmap = resources.GetRequired(path);
            HashSet<ushort> visitedTiles = [];

            foreach (ushort tileIndex in bitmap.Tiles)
            {
                if (tileIndex == ushort.MaxValue || !visitedTiles.Add(tileIndex))
                {
                    continue;
                }

                AniIndexSection section = resources.GetRequiredTileAnimation(path, tileIndex);
                animationCount++;

                for (int frame = 0; frame < section.FrameCount; frame++)
                {
                    if (decodedFrames.Add(section.FramePaths[frame]))
                    {
                        _ = AniFrameLoader.LoadWrappedFrame(source, section, (uint)frame,
                            ContentLookupMode.LooseThenPackage);
                    }
                }
            }
        }

        ValidateBitmap(map.CellGrid.PuzzlePath);

        foreach (GameMapPositionedOverlayRecord record in map.Scene.PositionedOverlays)
        {
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is GameMapPuzzleOverlayEntry puzzle)
                {
                    ValidateBitmap(puzzle.PuzzlePath);
                }
            }
        }

        return new WorldMapPuzzleValidationResult(puzzlePaths.Count, animationCount, decodedFrames.Count);
    }
}
