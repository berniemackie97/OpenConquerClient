namespace OpenConquer.Content.Map;

/// <summary>Ground and positioned puzzle bitmaps referenced by one loaded DMap.</summary>
public sealed class GameMapPuzzleResources
{
    private const int MaximumPuzzleFileBytes = 128 * 1024;
    private readonly Dictionary<string, GameMapPuzzleBitmap> _puzzles;

    private GameMapPuzzleResources(GameMapPuzzleBitmap ground, Dictionary<string, GameMapPuzzleBitmap> puzzles)
    {
        Ground = ground;
        _puzzles = puzzles;
    }

    public GameMapPuzzleBitmap Ground { get; }

    public GameMapPuzzleBitmap GetRequired(string contentPath)
    {
        if (!_puzzles.TryGetValue(contentPath, out GameMapPuzzleBitmap? puzzle))
        {
            throw new KeyNotFoundException($"Puzzle bitmap '{contentPath}' does not belong to this map.");
        }

        return puzzle;
    }

    public static GameMapPuzzleResources Load(IClientContentSource contentSource, GameMapContent map)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(map);

        Dictionary<string, GameMapPuzzleBitmap> puzzles = new(StringComparer.OrdinalIgnoreCase);

        GameMapPuzzleBitmap LoadPuzzle(string path)
        {
            if (!puzzles.TryGetValue(path, out GameMapPuzzleBitmap? puzzle))
            {
                byte[] payload = ContentReader.ReadRequiredBytes(contentSource, path,
                    ContentLookupMode.LooseOnly, MaximumPuzzleFileBytes);
                puzzle = GameMapPuzzleBitmap.Parse(payload);
                puzzles.Add(path, puzzle);
            }

            return puzzle;
        }

        GameMapPuzzleBitmap ground = LoadPuzzle(map.CellGrid.PuzzlePath);
        foreach (GameMapPositionedOverlayRecord record in map.Scene.PositionedOverlays)
        {
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is GameMapPuzzleOverlayEntry overlay)
                {
                    LoadPuzzle(overlay.PuzzlePath);
                }
            }
        }

        return new GameMapPuzzleResources(ground, puzzles);
    }
}
