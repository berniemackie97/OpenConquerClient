using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Analysis;

internal readonly record struct ContentSetDeltaGroup(int FileCount, long Bytes)
{
    public ContentSetDeltaGroup Add(long length) =>
        new(checked(FileCount + 1), checked(Bytes + length));
}

internal sealed record ContentSetDeltaReport(
    ContentSetDeltaGroup UnchangedPaths,
    ContentSetDeltaGroup AddedPaths,
    ContentSetDeltaGroup ReplacedPaths,
    ContentSetDeltaGroup RemovedPaths,
    ContentSetDeltaGroup ReusablePayloads,
    ContentSetDeltaGroup NewPayloads)
{
    public static ContentSetDeltaReport Compare(ContentManifest previous, ContentManifest candidate)
    {
        Dictionary<string, ContentManifestEntry> previousPaths = previous.Entries
            .ToDictionary(entry => entry.PathKey, StringComparer.Ordinal);
        HashSet<(string Sha256, long Length)> previousPayloads = previous.Entries
            .Select(entry => (entry.Sha256, entry.Length))
            .ToHashSet();
        HashSet<(string Sha256, long Length)> candidatePayloads = [];

        ContentSetDeltaGroup unchanged = default;
        ContentSetDeltaGroup added = default;
        ContentSetDeltaGroup replaced = default;
        ContentSetDeltaGroup reused = default;
        ContentSetDeltaGroup newPayloads = default;

        foreach (ContentManifestEntry entry in candidate.Entries)
        {
            if (!previousPaths.TryGetValue(entry.PathKey, out ContentManifestEntry old))
            {
                added = added.Add(entry.Length);
            }
            else if (old.Length == entry.Length &&
                string.Equals(old.Sha256, entry.Sha256, StringComparison.Ordinal))
            {
                unchanged = unchanged.Add(entry.Length);
            }
            else
            {
                replaced = replaced.Add(entry.Length);
            }

            (string Sha256, long Length) identity = (entry.Sha256, entry.Length);
            if (candidatePayloads.Add(identity))
            {
                if (previousPayloads.Contains(identity))
                {
                    reused = reused.Add(entry.Length);
                }
                else
                {
                    newPayloads = newPayloads.Add(entry.Length);
                }
            }
        }

        ContentSetDeltaGroup removed = default;
        HashSet<string> candidatePaths = candidate.Entries
            .Select(entry => entry.PathKey).ToHashSet(StringComparer.Ordinal);
        foreach (ContentManifestEntry entry in previous.Entries)
        {
            if (!candidatePaths.Contains(entry.PathKey))
            {
                removed = removed.Add(entry.Length);
            }
        }

        return new ContentSetDeltaReport(unchanged, added, replaced, removed, reused, newPayloads);
    }

    public IEnumerable<string> ToReportLines()
    {
        yield return $"Unchanged paths: {UnchangedPaths.FileCount} files, {UnchangedPaths.Bytes} bytes";
        yield return $"Added paths: {AddedPaths.FileCount} files, {AddedPaths.Bytes} bytes";
        yield return $"Replaced paths: {ReplacedPaths.FileCount} files, {ReplacedPaths.Bytes} candidate bytes";
        yield return $"Removed paths: {RemovedPaths.FileCount} files, {RemovedPaths.Bytes} previous bytes";
        yield return $"Distinct candidate payloads present in previous set: {ReusablePayloads.FileCount} payloads, {ReusablePayloads.Bytes} bytes";
        yield return $"Distinct candidate payloads absent from previous set: {NewPayloads.FileCount} payloads, {NewPayloads.Bytes} bytes";
    }
}
