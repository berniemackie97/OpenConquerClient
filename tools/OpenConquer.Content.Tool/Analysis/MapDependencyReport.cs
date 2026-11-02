using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Analysis;

internal readonly record struct MapDependencyGroup(string Name, int FileCount, long Bytes);
internal readonly record struct MapDependencyMap(uint MapDataId, int FileCount, long Bytes,
    int ExclusiveFileCount, long ExclusiveBytes);

internal sealed class MapDependencyReport
{
    private MapDependencyReport(MapDependencyGroup[] groups, MapDependencyMap[] maps, int excludedMapCount)
    {
        Groups = groups;
        Maps = maps;
        ExcludedMapCount = excludedMapCount;
    }

    public IReadOnlyList<MapDependencyGroup> Groups
    {
        get;
    }
    public IReadOnlyList<MapDependencyMap> Maps
    {
        get;
    }
    public int ExcludedMapCount
    {
        get;
    }

    public static MapDependencyReport Create(ContentManifest manifest,
        IReadOnlyList<ClientContentRequirement> startup,
        IReadOnlyDictionary<uint, IReadOnlyList<ClientContentRequirement>> maps,
        int excludedMapCount = 0)
    {
        if (maps.Count == 0)
        {
            throw new InvalidDataException("No maps were selected for dependency analysis.");
        }

        Dictionary<string, int> indices = manifest.Entries
            .Select((entry, index) => (entry.PathKey, index))
            .ToDictionary(entry => entry.PathKey, entry => entry.index, StringComparer.Ordinal);
        bool[] startupUsage = new bool[manifest.FileCount];
        int[] mapUsage = new int[manifest.FileCount];
        Dictionary<uint, int[]> mapIndices = new();

        foreach (ClientContentRequirement requirement in startup)
        {
            startupUsage[RequireIndex(requirement, indices)] = true;
        }

        foreach ((uint id, IReadOnlyList<ClientContentRequirement> requirements) in maps)
        {
            HashSet<int> selected = [];
            foreach (ClientContentRequirement requirement in requirements)
            {
                selected.Add(RequireIndex(requirement, indices));
            }

            int[] fileIndices = [.. selected];
            mapIndices.Add(id, fileIndices);
            foreach (int index in fileIndices)
            {
                mapUsage[index]++;
            }
        }

        MapDependencyGroup[] groups =
        [
            new("Startup only", 0, 0),
            new("Startup and maps", 0, 0),
            new("Exclusive to one map", 0, 0),
            new("Shared across maps", 0, 0),
        ];

        for (int index = 0; index < manifest.FileCount; index++)
        {
            int mapCount = mapUsage[index];
            bool usedAtStartup = startupUsage[index];
            if (mapCount == 0 && !usedAtStartup)
            {
                throw new InvalidDataException($"Content file '{manifest.Entries[index].PathKey}' has no dependency owner.");
            }

            int group = usedAtStartup ? (mapCount == 0 ? 0 : 1) : (mapCount == 1 ? 2 : 3);
            MapDependencyGroup current = groups[group];
            groups[group] = current with
            {
                FileCount = current.FileCount + 1,
                Bytes = checked(current.Bytes + manifest.Entries[index].Length),
            };
        }

        MapDependencyMap[] summaries = mapIndices.OrderBy(entry => entry.Key)
            .Select(entry =>
            {
                long bytes = 0;
                long exclusiveBytes = 0;
                int exclusiveCount = 0;

                foreach (int index in entry.Value)
                {
                    long length = manifest.Entries[index].Length;
                    bytes = checked(bytes + length);
                    if (!startupUsage[index] && mapUsage[index] == 1)
                    {
                        exclusiveCount++;
                        exclusiveBytes = checked(exclusiveBytes + length);
                    }
                }

                return new MapDependencyMap(entry.Key, entry.Value.Length, bytes,
                    exclusiveCount, exclusiveBytes);
            })
            .ToArray();

        return new MapDependencyReport(groups, summaries, excludedMapCount);
    }

    public IEnumerable<string> ToReportLines()
    {
        yield return $"Maps analyzed: {Maps.Count}, excluded: {ExcludedMapCount}";
        foreach (MapDependencyGroup group in Groups)
        {
            yield return $"{group.Name}: {group.FileCount} files, {group.Bytes} bytes";
        }

        foreach (MapDependencyMap map in Maps)
        {
            yield return $"MapDataId {map.MapDataId}: {map.FileCount} files, {map.Bytes} bytes; " +
                $"exclusive {map.ExclusiveFileCount} files, {map.ExclusiveBytes} bytes";
        }
    }

    private static int RequireIndex(ClientContentRequirement requirement,
        IReadOnlyDictionary<string, int> indices)
    {
        string key = ContentPath.ToKey(requirement.ContentPath.Replace('\\', '/'));
        if (!indices.TryGetValue(key, out int index))
        {
            throw new InvalidDataException($"Content dependency '{requirement.ContentPath}' is absent from the verified manifest.");
        }

        return index;
    }
}
