using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tool.World;

internal readonly record struct WorldMapSceneryAniValidationResult(
    int SceneryCount, int AnimationCount, int FrameCount);

internal static class WorldMapSceneryAniValidator
{
    public static WorldMapSceneryAniValidationResult Validate(string clientRootPath, uint mapDataId)
    {
        PackagedClientContentSource source = PackagedClientContentSource.Open(clientRootPath);
        GameMapContent map = GameMapContent.Load(source, GameMapCatalog.Load(source), mapDataId);
        IReadOnlyList<GameMapSceneryAniBinding> bindings = GameMapSceneryAniBindings.Load(source, map.Scene);

        HashSet<AniIndexSection> animations = [];
        HashSet<string> decodedPaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (GameMapSceneryAniBinding binding in bindings)
        {
            if (!animations.Add(binding.Animation))
            {
                continue;
            }

            for (int frame = 0; frame < binding.Animation.FrameCount; frame++)
            {
                if (decodedPaths.Add(binding.Animation.FramePaths[frame]))
                {
                    _ = AniFrameLoader.LoadWrappedFrame(source, binding.Animation, (uint)frame,
                        ContentLookupMode.LooseThenPackage);
                }
            }
        }

        return new WorldMapSceneryAniValidationResult(bindings.Count, animations.Count, decodedPaths.Count);
    }
}
