using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquer.Content;

internal static class ClientWorldContentRequirements
{
    public static void Add(List<ClientContentRequirement> requirements, IClientContentSource source,
        GameMapCatalog catalog, uint mapDataId)
    {
        GameMapContent map = GameMapContent.Load(source, catalog, mapDataId);
        GameMapPuzzleResources puzzles = GameMapPuzzleResources.Load(source, map);
        GameMapTerrainSceneResources terrain = GameMapTerrainSceneResources.Load(source, map);

        Add(map.ArchivePath, ContentLookupMode.LooseOnly);

        HashSet<string> puzzlePaths = new(StringComparer.OrdinalIgnoreCase);
        AddPuzzle(map.CellGrid.PuzzlePath);

        foreach (GameMapPositionedOverlayRecord record in map.Scene.PositionedOverlays)
        {
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is GameMapPuzzleOverlayEntry puzzle)
                {
                    AddPuzzle(puzzle.PuzzlePath);
                }
            }
        }

        HashSet<string> scenePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (GameMapTerrainScenePlacement placement in terrain.Placements)
        {
            string path = placement.Scenery.ResourcePath!;
            if (!scenePaths.Add(path))
            {
                continue;
            }

            Add(path, ContentLookupMode.LooseOnly);
            foreach (GameMapTerrainObjectBinding binding in placement.Objects)
            {
                Add(binding.Terrain.AnimationPath, ContentLookupMode.LooseOnly);
                foreach (string path in binding.Animation.FramePaths)
                {
                    Add(path, ContentLookupMode.LooseThenPackage);
                }
            }
        }

        void AddPuzzle(string path)
        {
            if (!puzzlePaths.Add(path))
            {
                return;
            }

            Add(path, ContentLookupMode.LooseOnly);
            GameMapPuzzleBitmap bitmap = puzzles.GetRequired(path);
            Add(bitmap.TextureIndexPath, ContentLookupMode.LooseOnly);

            HashSet<ushort> tileIndices = [];
            foreach (ushort tileIndex in bitmap.Tiles)
            {
                if (tileIndex == ushort.MaxValue || !tileIndices.Add(tileIndex))
                {
                    continue;
                }

                AniIndexSection animation = puzzles.GetRequiredTileAnimation(path, tileIndex);
                foreach (string framePath in animation.FramePaths)
                {
                    Add(framePath, ContentLookupMode.LooseThenPackage);
                }
            }
        }

        void Add(string path, ContentLookupMode lookupMode) =>
            requirements.Add(new ClientContentRequirement(path, lookupMode));
    }
}
