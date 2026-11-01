using OpenConquer.Content.Ani;

namespace OpenConquer.Content.Map;

public sealed record GameMapSceneryAniBinding(GameMapSceneryRecord Scenery, AniIndexSection Animation);

/// <summary>Resolves authored type-4 scenery ANI sections, without assigning any rendering semantics.</summary>
public static class GameMapSceneryAniBindings
{
    public static IReadOnlyList<GameMapSceneryAniBinding> Load(IClientContentSource source, GameMapSceneSections scene)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scene);

        Dictionary<string, AniIndexFile> indexes = new(StringComparer.OrdinalIgnoreCase);
        List<GameMapSceneryAniBinding> bindings = [];

        foreach (GameMapSceneryRecord record in scene.Scenery)
        {
            if (record.TypeCode != 4)
            {
                continue;
            }

            if (record.ResourcePath is not { Length: > 0 } path ||
                record.PartName is not { Length: > 0 } sectionName)
            {
                throw new InvalidDataException("Type-4 map scenery has no ANI path or section name.");
            }

            if (!indexes.TryGetValue(path, out AniIndexFile? index))
            {
                index = AniIndexFile.Load(source, path, ContentLookupMode.LooseOnly);
                indexes.Add(path, index);
            }

            AniIndexSection animation = index.GetRequiredSection(sectionName);
            if (!string.Equals(animation.Name, sectionName, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Map scenery ANI section [{sectionName}] in '{path}' resolved to [{animation.Name}].");
            }

            if (animation.FrameCount == 0)
            {
                throw new InvalidDataException($"Map scenery ANI section [{sectionName}] in '{path}' has no frames.");
            }

            bindings.Add(new GameMapSceneryAniBinding(record, animation));
        }

        return Array.AsReadOnly(bindings.ToArray());
    }
}
