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
        WorldFrameValidation frames = new(source);

        foreach (GameMapSceneryAniBinding binding in bindings)
        {
            if (!animations.Add(binding.Animation))
            {
                continue;
            }

            frames.Validate(binding.Animation);
        }

        return new WorldMapSceneryAniValidationResult(bindings.Count, animations.Count, frames.FrameCount);
    }
}
