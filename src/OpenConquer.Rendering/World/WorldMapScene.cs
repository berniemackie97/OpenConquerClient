using System.Numerics;
using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Map;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.World;

internal readonly record struct WorldMapLayer(
    IReadOnlyList<WorldPuzzleTile>? Puzzle = null, IReadOnlyList<WorldScenerySprite>? Scenery = null);

public sealed class WorldMapScene
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
    private int _heroX;
    private int _heroY;
    private int _heroTileX;
    private int _heroTileY;
    private byte _heroDirection;
    private int _zoomPercent = 100;
    private readonly uint _startMilliseconds;
    private readonly LogicalRenderSize _backdropExtent;
    private MapFrame _frame = new([], default, [], 0, 0, 0);
    private WorldSceneRole[] _remoteRoles = [];
    private int _remoteRoleRevision = int.MinValue;
    private WorldMapTextureRenderer? _presentedTextures;
    private MapFrame? _pendingFrame;
    private ViewKey _pendingView;

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
        (_heroTileX, _heroTileY) = (heroTileX, heroTileY);
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

    public LogicalRenderSize BackdropExtent => _backdropExtent;
    public GameMapContent Map => _map;
    internal IReadOnlyList<WorldRoleDrawSlot> CommittedRoleOrder => _frame.RoleSlots;

    public (Matrix4x4 Main, Matrix4x4 Shadow) HeroMatrices
    {
        get
        {
            (int x, int y, byte direction) = _presentedTextures is null
                ? (_heroX, _heroY, _heroDirection)
                : (_frame.HeroX, _frame.HeroY, _frame.HeroDirection);
            return WorldRoleCameraMatrices.Create(_map, _puzzles, _frame.Projection,
                x, y, rotationDegrees: (-1 - direction) * 45f, scale: 0.75f);
        }
    }

    public void SetRemoteRoles(int revision, IEnumerable<WorldSceneRole> roles)
    {
        if (_remoteRoleRevision == revision)
        {
            return;
        }

        _remoteRoles = roles.ToArray();
        _remoteRoleRevision = revision;
    }

    internal (Matrix4x4 Main, Matrix4x4 Shadow) RoleMatrices(WorldSceneRole role)
    {
        (int x, int y) = WorldCameraProjection.ProjectTile(_map, role.TileX, role.TileY);
        return WorldRoleCameraMatrices.Create(_map, _puzzles, _frame.Projection,
            x, y, (-1 - role.Direction) * 45f, 0.75f);
    }

    public void SetCameraTile(int tileX, int tileY)
    {
        // Native accepts signed off-grid tiles. Screen visibility and the ground-extents camera clamp are separate.
        (_heroX, _heroY) = WorldCameraProjection.ProjectTile(_map, tileX, tileY);
        (_heroTileX, _heroTileY) = (tileX, tileY);
    }

    public void SetHeroDirection(byte directionIndex)
    {
        if (directionIndex > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(directionIndex));
        }

        _heroDirection = directionIndex;
    }

    public void SetZoomPercent(int zoomPercent)
    {
        if (zoomPercent is < 10 or > 800)
        {
            throw new ArgumentOutOfRangeException(nameof(zoomPercent), "Map zoom must be between 10 and 800 percent.");
        }
        _zoomPercent = zoomPercent;
    }

    public static WorldMapScene Load(IClientContentSource source, GameMapCatalog catalog,
        uint mapDataId, ushort heroTileX, ushort heroTileY, uint startMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GameMapContent map = GameMapContent.Load(source, catalog, mapDataId);
        return Load(source, map, heroTileX, heroTileY, startMilliseconds, cancellationToken);
    }

    public static WorldMapScene Load(IClientContentSource source, GameMapContent map,
        ushort cameraTileX, ushort cameraTileY, uint startMilliseconds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(map);
        cancellationToken.ThrowIfCancellationRequested();
        GameMapPuzzleResources puzzles = GameMapPuzzleResources.Load(source, map);
        cancellationToken.ThrowIfCancellationRequested();
        AniSectionResolver animations = new(source, ContentLookupMode.LooseOnly);
        GameMapTerrainSceneResources terrain = GameMapTerrainSceneResources.Load(source, map, animations);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<GameMapSceneryAniBinding> scenery = GameMapSceneryAniBindings.Load(source, map.Scene, animations);
        cancellationToken.ThrowIfCancellationRequested();
        uint puzzleInterval = PuzzleFrameIntervalConfiguration.Load(source);
        cancellationToken.ThrowIfCancellationRequested();
        return new WorldMapScene(map, puzzles, terrain.Placements, scenery, cameraTileX, cameraTileY,
            puzzleInterval, startMilliseconds);
    }

    internal IReadOnlyList<WorldMapLayer> BuildFrame(int screenWidth, int screenHeight, uint nowMilliseconds)
    {
        _frame = BuildCandidate(screenWidth, screenHeight, nowMilliseconds);
        return _frame.Layers;
    }

    private MapFrame BuildCandidate(int screenWidth, int screenHeight, uint nowMilliseconds)
    {
        WorldPuzzleProjection projection = WorldCameraProjection.FromHeroWorldPosition(_map, _puzzles,
            _heroX, _heroY, screenWidth, screenHeight, zoomPercent: _zoomPercent);

        IReadOnlyList<WorldPuzzleTile> ground = WorldPuzzleDrawList.Ground(
            _map, _puzzles, projection, _groundClock.FrameForRender(nowMilliseconds));
        IReadOnlyList<WorldScenerySprite> scenery = WorldSceneryDrawList.BuildWithRoles(
            _map, _terrain, _sceneryIndex, _sceneryFrameExtents,
            projection, nowMilliseconds, _startMilliseconds,
            new WorldSceneRole(0, _heroTileX, _heroTileY, _heroDirection), _remoteRoles,
            out IReadOnlyList<WorldRoleDrawSlot> roleSlots);

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
                    _map, _puzzles, projection, record, puzzle, frame));
            }

            layers.Insert(Math.Clamp(record.InsertIndex, 0, layers.Count), new(Puzzle: puzzleTiles));
        }

        return new MapFrame(layers, projection, roleSlots, _heroX, _heroY, _heroDirection);
    }

    public bool Prepare(WorldMapTextureRenderer textures, int screenWidth, int screenHeight, uint nowMilliseconds,
        int textureBudget = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textureBudget);
        ViewKey view = new(screenWidth, screenHeight, _zoomPercent, _heroTileX, _heroTileY,
            _heroDirection, _remoteRoleRevision);
        // Keep animation requests stable while the current viewport catches up.
        MapFrame candidate = textures.IsStreaming && _pendingFrame is { } pending && view == _pendingView
            ? pending
            : BuildCandidate(screenWidth, screenHeight, nowMilliseconds);
        textures.BeginPreparation();
        int remaining = textureBudget;
        bool complete = true;
        foreach (WorldMapLayer layer in candidate.Layers)
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

        if (complete || !textures.IsStreaming)
        {
            _frame = candidate;
            _presentedTextures = textures.IsStreaming ? textures : null;
            _pendingFrame = null;
        }
        else
        {
            _pendingFrame = candidate;
            _pendingView = view;
            if (!ReferenceEquals(_presentedTextures, textures))
            {
                // A replacement context cannot draw a frame that used another renderer's texture cache.
                _frame = new MapFrame([], default, [], 0, 0, 0);
                _presentedTextures = null;
            }
        }

        textures.EndPreparation(complete);
        return complete;
    }

    public void Draw(WorldMapTextureRenderer textures, OpenGLRenderer renderer,
        Action<OpenGLRenderer>? drawRole = null,
        Action<OpenGLRenderer, uint, Matrix4x4, Matrix4x4>? drawRemoteRole = null)
    {
        foreach (WorldMapLayer layer in _frame.Layers)
        {
            if (layer.Puzzle is { } puzzle)
            {
                textures.Draw(renderer, puzzle, SpriteColor.White);
            }

            if (layer.Scenery is not { } scenery)
            {
                continue;
            }

            int drawn = 0;
            foreach (WorldRoleDrawSlot slot in _frame.RoleSlots)
            {
                textures.Draw(renderer, scenery, _frame.Projection, SpriteColor.White,
                    drawn, slot.SpriteIndex - drawn);
                drawn = slot.SpriteIndex;
                if (slot.Role.EntityId == 0)
                {
                    drawRole?.Invoke(renderer);
                    continue;
                }

                if (drawRemoteRole is not null)
                {
                    (Matrix4x4 main, Matrix4x4 shadow) = RoleMatrices(slot.Role);
                    drawRemoteRole(renderer, slot.Role.EntityId, main, shadow);
                }
            }

            textures.Draw(renderer, scenery, _frame.Projection, SpriteColor.White,
                drawn, scenery.Count - drawn);
        }
    }

    private readonly record struct ViewKey(int Width, int Height, int Zoom, int TileX, int TileY,
        byte Direction, int RemoteRolesRevision);

    private readonly record struct MapFrame(IReadOnlyList<WorldMapLayer> Layers, WorldPuzzleProjection Projection,
        IReadOnlyList<WorldRoleDrawSlot> RoleSlots, int HeroX, int HeroY, byte HeroDirection);
}
