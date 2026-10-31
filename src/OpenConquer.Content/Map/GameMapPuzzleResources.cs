using System.Globalization;
using OpenConquer.Content.Ani;

namespace OpenConquer.Content.Map;

/// <summary>Ground and positioned puzzle bitmaps with their authored tile animation sections.</summary>
public sealed class GameMapPuzzleResources
{
    private const int MaximumPuzzleFileBytes = 128 * 1024;

    private readonly Dictionary<string, PuzzleResource> _puzzles;

    private GameMapPuzzleResources(GameMapPuzzleBitmap ground, Dictionary<string, PuzzleResource> puzzles)
    {
        Ground = ground;
        _puzzles = puzzles;
    }

    public GameMapPuzzleBitmap Ground
    {
        get;
    }

    public GameMapPuzzleBitmap GetRequired(string contentPath)
    {
        if (!_puzzles.TryGetValue(contentPath, out PuzzleResource? puzzle))
        {
            throw new KeyNotFoundException($"Puzzle bitmap '{contentPath}' does not belong to this map.");
        }

        return puzzle.Bitmap;
    }

    public AniIndexSection GetRequiredTileAnimation(string contentPath, ushort tileIndex)
    {
        if (!_puzzles.TryGetValue(contentPath, out PuzzleResource? puzzle))
        {
            throw new KeyNotFoundException($"Puzzle bitmap '{contentPath}' does not belong to this map.");
        }

        if (!puzzle.Animations.TryGetValue(tileIndex, out AniIndexSection? section))
        {
            throw new InvalidDataException($"Puzzle bitmap '{contentPath}' does not reference tile index {tileIndex}.");
        }

        return section;
    }

    public static GameMapPuzzleResources Load(IClientContentSource contentSource, GameMapContent map)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(map);

        Dictionary<string, GameMapPuzzleBitmap> bitmaps = new(StringComparer.OrdinalIgnoreCase);

        GameMapPuzzleBitmap LoadPuzzle(string path)
        {
            if (!bitmaps.TryGetValue(path, out GameMapPuzzleBitmap? puzzle))
            {
                byte[] payload = ContentReader.ReadRequiredBytes(contentSource, path,
                    ContentLookupMode.LooseOnly, MaximumPuzzleFileBytes);
                puzzle = GameMapPuzzleBitmap.Parse(payload);
                bitmaps.Add(path, puzzle);
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

        Dictionary<string, AniIndexFile> aniFiles = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, PuzzleResource> puzzles = new(bitmaps.Count, StringComparer.OrdinalIgnoreCase);

        foreach ((string path, GameMapPuzzleBitmap bitmap) in bitmaps)
        {
            if (!aniFiles.TryGetValue(bitmap.TextureIndexPath, out AniIndexFile? index))
            {
                index = AniIndexFile.Load(contentSource, bitmap.TextureIndexPath, ContentLookupMode.LooseOnly);
                aniFiles.Add(bitmap.TextureIndexPath, index);
            }

            Dictionary<ushort, AniIndexSection> animations = [];
            foreach (ushort tileIndex in bitmap.Tiles)
            {
                if (tileIndex == ushort.MaxValue || animations.ContainsKey(tileIndex))
                {
                    continue;
                }

                string sectionName = $"Puzzle{tileIndex.ToString(CultureInfo.InvariantCulture)}";
                if (!index.TryGetSection(sectionName, out AniIndexSection? section) || section.FrameCount == 0)
                {
                    throw new InvalidDataException(
                        $"Puzzle bitmap '{path}' references missing or empty [{sectionName}] in '{bitmap.TextureIndexPath}'.");
                }

                animations.Add(tileIndex, section);
            }

            puzzles.Add(path, new PuzzleResource(bitmap, animations));
        }

        return new GameMapPuzzleResources(ground, puzzles);
    }

    private sealed record PuzzleResource(GameMapPuzzleBitmap Bitmap, Dictionary<ushort, AniIndexSection> Animations);
}
