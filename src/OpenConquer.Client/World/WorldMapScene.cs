using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Map;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.World;

internal readonly record struct WorldMapLayer(
    IReadOnlyList<WorldPuzzleTile>? Puzzle = null, IReadOnlyList<WorldScenerySprite>? Scenery = null);

internal sealed class WorldMapScene
{
    private readonly GameMapContent _map;
    private readonly GameMapPuzzleResources _puzzles;
    private readonly IReadOnlyList<GameMapTerrainScenePlacement> _terrain;
    private readonly WorldSceneryIndex _sceneryIndex;
    private readonly Dictionary<GameMapSceneryRecord, (int Width, int Height)> _sceneryFrameExtents =
        new(ReferenceEqualityComparer.Instance);
    private readonly WorldPuzzleAnimationClock _groundClock;
    private readonly Dictionary<GameMapPuzzleOverlayEntry, WorldPuzzleAnimationClock> _overlayClocks =
        new(ReferenceEqualityComparer.Instance);
    private readonly int _heroX;
    private readonly int _heroY;
    private readonly uint _startMilliseconds;
    private readonly LogicalRenderSize _backdropExtent;
    private IReadOnlyList<WorldMapLayer> _layers = [];
    private WorldPuzzleProjection _projection;

    internal WorldMapScene(GameMapContent map, GameMapPuzzleResources puzzles,
        IReadOnlyList<GameMapTerrainScenePlacement> terrain,
        IReadOnlyList<GameMapSceneryAniBinding> animatedScenery,
        int heroTileX, int heroTileY, uint puzzleIntervalMilliseconds, uint startMilliseconds)
    {
        _map = map;
        _puzzles = puzzles;
        _terrain = terrain;
        _sceneryIndex = new WorldSceneryIndex(map, animatedScenery);
        (_heroX, _heroY) = WorldCameraProjection.ProjectTile(map, heroTileX, heroTileY);
        _groundClock = new WorldPuzzleAnimationClock(puzzleIntervalMilliseconds);
        _startMilliseconds = startMilliseconds;
        int backdropWidth = checked(puzzles.GroundColumns * 256);
        int backdropHeight = checked(puzzles.GroundRows * 256);
        foreach (GameMapPositionedOverlayRecord record in map.Scene.PositionedOverlays)
        {
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is GameMapPuzzleOverlayEntry puzzle)
                {
                    _overlayClocks.Add(puzzle, new WorldPuzzleAnimationClock(puzzleIntervalMilliseconds));
                    if (record.InsertIndex == 0)
                    {
                        GameMapPuzzleBitmap bitmap = puzzles.GetRequired(puzzle.PuzzlePath);
                        backdropWidth = Math.Min(backdropWidth, checked(bitmap.Columns * 256));
                        backdropHeight = Math.Min(backdropHeight, checked(bitmap.Rows * 256));
                    }
                }
            }
        }

        _backdropExtent = new LogicalRenderSize(backdropWidth, backdropHeight);
    }

    internal LogicalRenderSize BackdropExtent => _backdropExtent;

    public static WorldMapScene Load(IClientContentSource source, GameMapCatalog catalog,
        uint mapDataId, ushort heroTileX, ushort heroTileY, uint startMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GameMapContent map = GameMapContent.Load(source, catalog, mapDataId);
        cancellationToken.ThrowIfCancellationRequested();
        GameMapPuzzleResources puzzles = GameMapPuzzleResources.Load(source, map);
        cancellationToken.ThrowIfCancellationRequested();
        GameMapTerrainSceneResources terrain = GameMapTerrainSceneResources.Load(source, map);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<GameMapSceneryAniBinding> scenery = GameMapSceneryAniBindings.Load(source, map.Scene);
        cancellationToken.ThrowIfCancellationRequested();
        uint puzzleInterval = PuzzleFrameIntervalConfiguration.Load(source);
        cancellationToken.ThrowIfCancellationRequested();
        return new WorldMapScene(map, puzzles, terrain.Placements, scenery, heroTileX, heroTileY,
            puzzleInterval, startMilliseconds);
    }

    internal IReadOnlyList<WorldMapLayer> BuildFrame(int screenWidth, int screenHeight, uint nowMilliseconds)
    {
        _projection = WorldCameraProjection.FromHeroWorldPosition(_map, _puzzles,
            _heroX, _heroY, screenWidth, screenHeight);

        IReadOnlyList<WorldPuzzleTile> ground = WorldPuzzleDrawList.Ground(
            _map, _puzzles, _projection, _groundClock.FrameForRender(nowMilliseconds));
        IReadOnlyList<WorldScenerySprite> scenery = WorldSceneryDrawList.Build(
            _map, _terrain, _sceneryIndex, _sceneryFrameExtents,
            _projection, nowMilliseconds, _startMilliseconds);

        List<WorldMapLayer> layers =
        [
            new(Puzzle: ground),
            default,
            default,
            new(Scenery: scenery),
            default,
        ];

        foreach (GameMapPositionedOverlayRecord record in _map.Scene.PositionedOverlays)
        {
            List<WorldPuzzleTile> puzzleTiles = [];
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is not GameMapPuzzleOverlayEntry puzzle)
                {
                    continue;
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

    public bool Prepare(WorldMapTextureRenderer textures, int screenWidth, int screenHeight, uint nowMilliseconds,
        int textureBudget = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textureBudget);
        BuildFrame(screenWidth, screenHeight, nowMilliseconds);
        int remaining = textureBudget;
        bool complete = true;
        foreach (WorldMapLayer layer in _layers)
        {
            if (layer.Puzzle is { } puzzle && !textures.Prepare(puzzle, ref remaining))
            {
                complete = false;
            }
            if (layer.Scenery is { } scenery)
            {
                if (!textures.Prepare(scenery, ref remaining))
                {
                    complete = false;
                }

                foreach (WorldScenerySprite sprite in scenery)
                {
                    if (sprite.Source is { } record && !_sceneryFrameExtents.ContainsKey(record) &&
                        textures.TryGetFrameExtent(sprite.Animation.FramePaths[sprite.FrameIndex],
                            out int width, out int height))
                    {
                        _sceneryFrameExtents.Add(record, (width, height));
                    }
                }
            }
        }

        return complete;
    }

    public void Draw(WorldMapTextureRenderer textures, OpenGLRenderer renderer)
    {
        foreach (WorldMapLayer layer in _layers)
        {
            if (layer.Puzzle is { } puzzle)
            {
                textures.Draw(renderer, puzzle, SpriteColor.White);
            }
            if (layer.Scenery is { } scenery)
            {
                textures.Draw(renderer, scenery, _projection, SpriteColor.White);
            }
        }
    }
}
