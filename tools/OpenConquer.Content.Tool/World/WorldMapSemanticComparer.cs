using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tool.World;

internal static class WorldMapSemanticComparer
{
    public static void Compare(string sourceRootPath, string candidateRootPath, uint mapDataId)
    {
        PackagedClientContentSource source = PackagedClientContentSource.Open(sourceRootPath);
        PackagedClientContentSource candidate = PackagedClientContentSource.Open(candidateRootPath);

        GameMapContent sourceMap = GameMapContent.Load(source, GameMapCatalog.Load(source), mapDataId);
        GameMapContent candidateMap = GameMapContent.Load(candidate, GameMapCatalog.Load(candidate), mapDataId);
        Compare(sourceMap, candidateMap);
    }

    internal static void Compare(GameMapContent source, GameMapContent candidate)
    {
        Require(source.MapDataId == candidate.MapDataId, "map data ID");
        Require(string.Equals(source.ArchivePath, candidate.ArchivePath, StringComparison.Ordinal), "catalog archive path");
        Require(source.CompanionValue == candidate.CompanionValue, "catalog companion value");
        Compare(source.CellGrid, candidate.CellGrid);
        Compare(source.Scene, candidate.Scene);
    }

    internal static void Compare(GameMapCellGrid source, GameMapCellGrid candidate)
    {
        Require(source.HeaderField0 == candidate.HeaderField0 &&
            source.HeaderField1 == candidate.HeaderField1, "grid header");
        Require(string.Equals(source.PuzzlePath, candidate.PuzzlePath, StringComparison.Ordinal), "ground puzzle path");
        Require(source.Width == candidate.Width && source.Height == candidate.Height, "grid dimensions");

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Require(source.GetCell(x, y) == candidate.GetCell(x, y), $"cell ({x}, {y})");
            }
        }
    }

    internal static void Compare(GameMapSceneSections source, GameMapSceneSections candidate)
    {
        Require(source.ExitMarkers.Count == candidate.ExitMarkers.Count, "exit marker count");
        for (int index = 0; index < source.ExitMarkers.Count; index++)
        {
            Require(source.ExitMarkers[index] == candidate.ExitMarkers[index], $"exit marker {index}");
        }

        Require(source.Scenery.Count == candidate.Scenery.Count, "scenery count");
        for (int index = 0; index < source.Scenery.Count; index++)
        {
            GameMapSceneryRecord left = source.Scenery[index];
            GameMapSceneryRecord right = candidate.Scenery[index];
            Require(left.TypeCode == right.TypeCode &&
                string.Equals(left.ResourcePath, right.ResourcePath, StringComparison.Ordinal) &&
                string.Equals(left.PartName, right.PartName, StringComparison.Ordinal) &&
                left.TileX == right.TileX && left.TileY == right.TileY &&
                left.EncodedPayload.Span.SequenceEqual(right.EncodedPayload.Span), $"scenery record {index}");
        }

        Require(source.PositionedOverlays.Count == candidate.PositionedOverlays.Count, "overlay record count");
        for (int index = 0; index < source.PositionedOverlays.Count; index++)
        {
            CompareOverlay(source.PositionedOverlays[index], candidate.PositionedOverlays[index], index);
        }
    }

    private static void CompareOverlay(GameMapPositionedOverlayRecord source,
        GameMapPositionedOverlayRecord candidate, int index)
    {
        Require(source.InsertIndex == candidate.InsertIndex &&
            source.HeaderInt0 == candidate.HeaderInt0 &&
            source.HeaderInt1 == candidate.HeaderInt1, $"overlay record {index} header");
        Require(source.Entries.Count == candidate.Entries.Count, $"overlay record {index} entry count");

        for (int entryIndex = 0; entryIndex < source.Entries.Count; entryIndex++)
        {
            Require(Equals(source.Entries[entryIndex], candidate.Entries[entryIndex]),
                $"overlay record {index} entry {entryIndex}");
        }
    }

    private static void Require(bool matches, string location)
    {
        if (!matches)
        {
            throw new InvalidDataException($"Map semantics differ at {location}.");
        }
    }
}
