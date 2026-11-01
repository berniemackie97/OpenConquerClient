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

        AniSectionResolver animations = new(source, ContentLookupMode.LooseOnly);
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

            bindings.Add(new GameMapSceneryAniBinding(record, animations.Resolve(path, sectionName)));
        }

        return Array.AsReadOnly(bindings.ToArray());
    }
}
