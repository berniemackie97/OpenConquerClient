using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tool.World;

internal readonly record struct WorldMapTerrainSceneValidationResult(
    int PlacementCount, int SceneFileCount, int ObjectCount, int FrameCount);

internal static class WorldMapTerrainSceneValidator
{
    public static WorldMapTerrainSceneValidationResult Validate(string retailRootPath, uint mapDataId)
    {
        PackagedClientContentSource source = PackagedClientContentSource.Open(retailRootPath);
        GameMapContent map = GameMapContent.Load(source, GameMapCatalog.Load(source), mapDataId);
        GameMapTerrainSceneResources resources = GameMapTerrainSceneResources.Load(source, map);

        HashSet<GameMapTerrainObjectBinding> objects = [];
        HashSet<string> decodedFrames = new(StringComparer.OrdinalIgnoreCase);
        foreach (GameMapTerrainScenePlacement placement in resources.Placements)
        {
            foreach (GameMapTerrainObjectBinding obj in placement.Objects)
            {
                if (!objects.Add(obj))
                {
                    continue;
                }

                for (int frame = 0; frame < obj.Animation.FrameCount; frame++)
                {
                    if (decodedFrames.Add(obj.Animation.FramePaths[frame]))
                    {
                        _ = AniFrameLoader.LoadWrappedFrame(source, obj.Animation, (uint)frame,
                            ContentLookupMode.LooseThenPackage);
                    }
                }
            }
        }

        return new WorldMapTerrainSceneValidationResult(
            resources.Placements.Count, resources.SceneFileCount, objects.Count, decodedFrames.Count);
    }
}
