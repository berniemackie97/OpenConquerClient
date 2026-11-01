using OpenConquer.Content.Ani;

namespace OpenConquer.Content.Map;

public sealed record GameMapTerrainObjectBinding(GameMapTerrainObject Terrain, AniIndexSection Animation);
public sealed record GameMapTerrainScenePlacement(
    GameMapSceneryRecord Scenery, IReadOnlyList<GameMapTerrainObjectBinding> Objects);

/// <summary>Type-1 DMap scenery placements bound to authored .scene objects and ANI frames.</summary>
public sealed class GameMapTerrainSceneResources
{
    private readonly IReadOnlyList<GameMapTerrainScenePlacement> _placements;

    private GameMapTerrainSceneResources(GameMapTerrainScenePlacement[] placements, int fileCount)
    {
        _placements = Array.AsReadOnly(placements);
        SceneFileCount = fileCount;
    }

    public IReadOnlyList<GameMapTerrainScenePlacement> Placements => _placements;
    public int SceneFileCount
    {
        get;
    }

    public static GameMapTerrainSceneResources Load(IClientContentSource source, GameMapContent map)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(map);

        Dictionary<string, IReadOnlyList<GameMapTerrainObjectBinding>> sceneFiles =
            new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, AniIndexFile> indexes = new(StringComparer.OrdinalIgnoreCase);
        List<GameMapTerrainScenePlacement> placements = [];

        foreach (GameMapSceneryRecord scenery in map.Scene.Scenery)
        {
            if (scenery.TypeCode != 1)
            {
                continue;
            }

            string path = scenery.ResourcePath
                ?? throw new InvalidDataException("Type-1 map scenery is missing its scene path.");
            if (!sceneFiles.TryGetValue(path, out IReadOnlyList<GameMapTerrainObjectBinding>? objects))
            {
                byte[] bytes = ContentReader.ReadRequiredBytes(
                    source, path, ContentLookupMode.LooseOnly, GameMapTerrainSceneFile.MaximumFileBytes);
                GameMapTerrainSceneFile file = GameMapTerrainSceneFile.Parse(bytes);
                List<GameMapTerrainObjectBinding> bound = new(file.Objects.Count);

                foreach (GameMapTerrainObject obj in file.Objects)
                {
                    if (!indexes.TryGetValue(obj.AnimationPath, out AniIndexFile? index))
                    {
                        index = AniIndexFile.Load(source, obj.AnimationPath, ContentLookupMode.LooseOnly);
                        indexes.Add(obj.AnimationPath, index);
                    }

                    AniIndexSection section = index.GetRequiredSection(obj.SectionName);
                    if (!string.Equals(section.Name, obj.SectionName, StringComparison.Ordinal) ||
                        section.FrameCount == 0)
                    {
                        throw new InvalidDataException(
                            $"Terrain scene '{path}' has an invalid ANI section [{obj.SectionName}].");
                    }

                    bound.Add(new GameMapTerrainObjectBinding(obj, section));
                }

                objects = Array.AsReadOnly(bound.ToArray());
                sceneFiles.Add(path, objects);
            }

            placements.Add(new GameMapTerrainScenePlacement(scenery, objects));
        }

        return new GameMapTerrainSceneResources(placements.ToArray(), sceneFiles.Count);
    }
}
