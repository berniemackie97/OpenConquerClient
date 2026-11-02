using OpenConquer.Content.Map;
using OpenConquer.Content.Tool.Manifest;
using OpenConquer.Content.Tool.Verify;

namespace OpenConquer.Content.Tool.Analysis;

internal static class MapDependencyAnalyzer
{
    public static MapDependencyReport Analyze(string contentSetRootPath, bool availableMaps)
    {
        ContentManifest manifest = ContentSetVerifier.Verify(contentSetRootPath,
            allMaps: !availableMaps, availableMaps: availableMaps);
        ClientContentRoot content = new(Path.Combine(contentSetRootPath, "payload"));
        GameMapCatalog catalog = GameMapCatalog.Load(content);
        HashSet<uint> excluded = manifest.MissingMapArchives?
            .Select(archive => archive.MapDataId).ToHashSet() ?? [];

        SortedDictionary<uint, IReadOnlyList<ClientContentRequirement>> maps = new();
        foreach (uint mapDataId in catalog.MapDataIds.Order())
        {
            if (!excluded.Contains(mapDataId))
            {
                maps.Add(mapDataId, ClientContentClosure.ResolveMap(content, catalog, mapDataId));
            }
        }

        return MapDependencyReport.Create(manifest, ClientContentClosure.Resolve(content),
            maps, excluded.Count);
    }
}
