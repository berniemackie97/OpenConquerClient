using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tool.World;

/// <summary>Counts from validating a map's terrain scenes, including decoding every referenced frame.</summary>
internal readonly record struct WorldMapTerrainSceneValidationResult(
    int PlacementCount, int SceneFileCount, int ObjectCount, int FrameCount);

internal static class WorldMapTerrainSceneValidator
{
    public static WorldMapTerrainSceneValidationResult Validate(string clientRootPath, uint mapDataId)
    {
        PackagedClientContentSource source = PackagedClientContentSource.Open(clientRootPath);
        GameMapContent map = GameMapContent.Load(source, GameMapCatalog.Load(source), mapDataId);
        GameMapTerrainSceneResources resources = GameMapTerrainSceneResources.Load(source, map);

        HashSet<GameMapTerrainObjectBinding> objects = new(ReferenceEqualityComparer.Instance);
        WorldFrameValidation frames = new(source);
        foreach (GameMapTerrainScenePlacement placement in resources.Placements)
        {
            foreach (GameMapTerrainObjectBinding binding in placement.Objects)
            {
                if (!objects.Add(binding))
                {
                    continue;
                }

                frames.Validate(binding.Animation);
            }
        }

        return new WorldMapTerrainSceneValidationResult(
            resources.Placements.Count, resources.SceneFileCount, objects.Count, frames.FrameCount);
    }
}
