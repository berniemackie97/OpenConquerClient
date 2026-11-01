using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Map;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.World;

internal readonly record struct WorldMapLayer(
    IReadOnlyList<WorldPuzzleTile>? Puzzle = null, IReadOnlyList<WorldTerrainSprite>? Terrain = null);

internal sealed class WorldMapScene
{
    private readonly GameMapContent _map;
    private readonly GameMapPuzzleResources _puzzles;
    private readonly IReadOnlyList<GameMapTerrainScenePlacement> _terrain;
    private readonly WorldPuzzleAnimationClock _groundClock;
    private readonly Dictionary<GameMapPuzzleOverlayEntry, WorldPuzzleAnimationClock> _overlayClocks =
        new(ReferenceEqualityComparer.Instance);
    private readonly int _heroX;
    private readonly int _heroY;
    private readonly uint _startMilliseconds;
    private IReadOnlyList<WorldMapLayer> _layers = [];
    private WorldPuzzleProjection _projection;

    internal WorldMapScene(GameMapContent map, GameMapPuzzleResources puzzles,
        IReadOnlyList<GameMapTerrainScenePlacement> terrain, int heroTileX, int heroTileY,
        uint puzzleIntervalMilliseconds, uint startMilliseconds)
    {
        _map = map;
        _puzzles = puzzles;
        _terrain = terrain;
        (_heroX, _heroY) = WorldCameraProjection.ProjectTile(map, heroTileX, heroTileY);
        _groundClock = new WorldPuzzleAnimationClock(puzzleIntervalMilliseconds);
        _startMilliseconds = startMilliseconds;
        foreach (GameMapPositionedOverlayRecord record in map.Scene.PositionedOverlays)
        {
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is GameMapPuzzleOverlayEntry puzzle)
                {
                    _overlayClocks.Add(puzzle, new WorldPuzzleAnimationClock(puzzleIntervalMilliseconds));
                }
            }
        }
    }

    public static WorldMapScene Load(IClientContentSource source, GameMapCatalog catalog,
        uint mapDataId, ushort heroTileX, ushort heroTileY, uint startMilliseconds)
    {
        GameMapContent map = GameMapContent.Load(source, catalog, mapDataId);
        GameMapPuzzleResources puzzles = GameMapPuzzleResources.Load(source, map);
        GameMapTerrainSceneResources terrain = GameMapTerrainSceneResources.Load(source, map);
        uint puzzleInterval = PuzzleFrameIntervalConfiguration.Load(source);
        return new WorldMapScene(map, puzzles, terrain.Placements, heroTileX, heroTileY,
            puzzleInterval, startMilliseconds);
    }

    internal IReadOnlyList<WorldMapLayer> BuildFrame(int screenWidth, int screenHeight, uint nowMilliseconds)
    {
        _projection = WorldCameraProjection.FromHeroWorldPosition(_map, _puzzles,
            _heroX, _heroY, screenWidth, screenHeight);

        IReadOnlyList<WorldPuzzleTile> ground = WorldPuzzleDrawList.Ground(
            _map, _puzzles, _projection, _groundClock.FrameForRender(nowMilliseconds));
        IReadOnlyList<WorldTerrainSprite> terrain = WorldTerrainSceneDrawList.Build(
            _map, _terrain, _projection, nowMilliseconds, _startMilliseconds);

        List<WorldMapLayer> layers =
        [
            new(Puzzle: ground),
            default,
            default,
            new(Terrain: terrain),
            default,
        ];

        foreach (GameMapPositionedOverlayRecord record in _map.Scene.PositionedOverlays)
        {
            List<WorldPuzzleTile> puzzleTiles = [];
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is not GameMapPuzzleOverlayEntry puzzle)
                {
                    throw new NotSupportedException("Animated positioned map overlays are not implemented.");
                }

                uint frame = _overlayClocks[puzzle].FrameForRender(nowMilliseconds);
                puzzleTiles.AddRange(WorldPuzzleDrawList.PositionedPuzzle(
                    _map, _puzzles, _projection, record, puzzle, frame));
            }

            layers.Insert(Math.Clamp(record.InsertIndex, 0, layers.Count), new(Puzzle: puzzleTiles));
        }

        _layers = layers;
        return layers;
    }

    public void Prepare(WorldMapTextureRenderer textures, int screenWidth, int screenHeight, uint nowMilliseconds)
    {
        BuildFrame(screenWidth, screenHeight, nowMilliseconds);
        foreach (WorldMapLayer layer in _layers)
        {
            if (layer.Puzzle is { } puzzle)
            {
                textures.Prepare(puzzle);
            }
            if (layer.Terrain is { } terrain)
            {
                textures.Prepare(terrain);
            }
        }
    }

    public void Draw(WorldMapTextureRenderer textures, OpenGLRenderer renderer)
    {
        foreach (WorldMapLayer layer in _layers)
        {
            if (layer.Puzzle is { } puzzle)
            {
                textures.Draw(renderer, puzzle, SpriteColor.White);
            }
            if (layer.Terrain is { } terrain)
            {
                textures.Draw(renderer, terrain, _projection, SpriteColor.White);
            }
        }
    }
}
