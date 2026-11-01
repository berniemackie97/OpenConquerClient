using System.Collections.ObjectModel;
using OpenConquer.Content.Ani;

namespace OpenConquer.Content.Map;

/// <summary>A terrain object with its resolved animation section.</summary>
public sealed record GameMapTerrainObjectBinding(GameMapTerrainObject Terrain, AniIndexSection Animation);

/// <summary>A type-1 map scenery placement and the terrain objects of its scene file, in file order.</summary>
public sealed class GameMapTerrainScenePlacement
{
    internal GameMapTerrainScenePlacement(GameMapSceneryRecord scenery, IReadOnlyList<GameMapTerrainObjectBinding> objects)
    {
        Scenery = scenery;
        Objects = objects;
    }

    public GameMapSceneryRecord Scenery
    {
        get;
    }

    /// <summary>Terrain objects of the placement's scene file; placements of the same file share this list.</summary>
    public IReadOnlyList<GameMapTerrainObjectBinding> Objects
    {
        get;
    }
}

/// <summary>
/// Loads the scene files referenced by a map's type-1 scenery placements and resolves their animation sections.
/// Placement order is the map's authored order, which is not a draw order.
/// </summary>
public sealed class GameMapTerrainSceneResources
{
    private const int MaximumScenePathLength = 260;

    private GameMapTerrainSceneResources(GameMapTerrainScenePlacement[] placements, int sceneFileCount)
    {
        Placements = Array.AsReadOnly(placements);
        SceneFileCount = sceneFileCount;
    }

    public IReadOnlyList<GameMapTerrainScenePlacement> Placements
    {
        get;
    }

    /// <summary>Number of distinct scene files the placements reference.</summary>
    public int SceneFileCount
    {
        get;
    }

    /// <exception cref="FileNotFoundException">A referenced scene file or animation index is missing.</exception>
    /// <exception cref="InvalidDataException">A scene file or a referenced animation section is invalid.</exception>
    public static GameMapTerrainSceneResources Load(IClientContentSource source, GameMapContent map)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(map);

        AniSectionResolver animations = new(source, ContentLookupMode.LooseOnly);
        Dictionary<string, IReadOnlyList<GameMapTerrainObjectBinding>> sceneFiles = new(StringComparer.OrdinalIgnoreCase);
        List<GameMapTerrainScenePlacement> placements = [];

        foreach (GameMapSceneryRecord scenery in map.Scene.Scenery)
        {
            if (scenery.TypeCode != 1)
            {
                continue;
            }

            string scenePath = NormalizeScenePath(scenery.ResourcePath);
            if (!sceneFiles.TryGetValue(scenePath, out IReadOnlyList<GameMapTerrainObjectBinding>? objects))
            {
                objects = LoadSceneFile(source, animations, scenePath);
                sceneFiles.Add(scenePath, objects);
            }

            placements.Add(new GameMapTerrainScenePlacement(scenery, objects));
        }

        return new GameMapTerrainSceneResources([.. placements], sceneFiles.Count);
    }

    private static ReadOnlyCollection<GameMapTerrainObjectBinding> LoadSceneFile(IClientContentSource source,
        AniSectionResolver animations, string scenePath)
    {
        byte[] bytes = ContentReader.ReadRequiredBytes(source, scenePath, ContentLookupMode.LooseOnly,
            GameMapTerrainSceneFile.MaximumFileBytes);
        GameMapTerrainSceneFile file = GameMapTerrainSceneFile.Parse(bytes);

        GameMapTerrainObjectBinding[] bindings = new GameMapTerrainObjectBinding[file.Objects.Count];
        for (int index = 0; index < bindings.Length; index++)
        {
            GameMapTerrainObject terrainObject = file.Objects[index];
            bindings[index] = new GameMapTerrainObjectBinding(
                terrainObject, animations.Resolve(terrainObject.AnimationPath, terrainObject.SectionName));
        }

        return Array.AsReadOnly(bindings);
    }

    private static string NormalizeScenePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidDataException("Type-1 map scenery has no scene file path.");
        }

        try
        {
            return ClientContentPath.NormalizeVirtualPath(path, nameof(path), MaximumScenePathLength);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"Type-1 map scenery scene path '{path}' is invalid.", exception);
        }
    }
}
