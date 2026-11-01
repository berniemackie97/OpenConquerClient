using OpenConquer.Content.Ani;
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

        // Placements of one scene file share bindings; count each object once.
        HashSet<GameMapTerrainObjectBinding> objects = new(ReferenceEqualityComparer.Instance);
        HashSet<string> decodedFrames = new(StringComparer.OrdinalIgnoreCase);
        foreach (GameMapTerrainScenePlacement placement in resources.Placements)
        {
            foreach (GameMapTerrainObjectBinding binding in placement.Objects)
            {
                if (!objects.Add(binding))
                {
                    continue;
                }

                for (int frame = 0; frame < binding.Animation.FrameCount; frame++)
                {
                    if (decodedFrames.Add(binding.Animation.FramePaths[frame]))
                    {
                        _ = AniFrameLoader.LoadWrappedFrame(source, binding.Animation, (uint)frame,
                            ContentLookupMode.LooseThenPackage);
                    }
                }
            }
        }

        return new WorldMapTerrainSceneValidationResult(
            resources.Placements.Count, resources.SceneFileCount, objects.Count, decodedFrames.Count);
    }
}
