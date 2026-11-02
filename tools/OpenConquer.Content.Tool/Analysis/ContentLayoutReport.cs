using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Analysis;

internal readonly record struct ContentLayoutGroup(string Name, int FileCount, long Length);

internal sealed record ContentLayoutReport(
    int FileCount, long Length, int DistinctFileCount, long DistinctLength,
    long MedianFileLength, long P95FileLength, long P99FileLength, long MaximumFileLength,
    IReadOnlyList<ContentLayoutGroup> Formats, IReadOnlyList<ContentLayoutGroup> Families)
{
    public int DuplicateFileCount => FileCount - DistinctFileCount;
    public long DuplicateLength => Length - DistinctLength;

    public static ContentLayoutReport Create(ContentManifest manifest)
    {
        if (manifest.Entries.Count == 0)
        {
            throw new InvalidDataException("The content set contains no files.");
        }

        HashSet<(string Sha256, long Length)> distinct = [];
        long distinctLength = 0;
        long[] lengths = new long[manifest.Entries.Count];

        for (int index = 0; index < manifest.Entries.Count; index++)
        {
            ContentManifestEntry entry = manifest.Entries[index];
            lengths[index] = entry.Length;
            if (distinct.Add((entry.Sha256, entry.Length)))
            {
                distinctLength = checked(distinctLength + entry.Length);
            }
        }

        Array.Sort(lengths);
        return new ContentLayoutReport(
            manifest.FileCount, manifest.Length, distinct.Count, distinctLength,
            Percentile(lengths, 50), Percentile(lengths, 95), Percentile(lengths, 99),
            lengths[^1],
            Group(manifest.Entries, static entry => entry.Signature),
            Group(manifest.Entries, static entry => Family(entry.PathKey)));
    }

    public IEnumerable<string> ToReportLines()
    {
        yield return $"Files: {FileCount}, bytes: {Length}";
        yield return $"Distinct payloads: {DistinctFileCount}, bytes: {DistinctLength}";
        yield return $"Redundant copies: {DuplicateFileCount}, bytes: {DuplicateLength}";
        yield return $"File sizes (bytes): p50 {MedianFileLength}, p95 {P95FileLength}, p99 {P99FileLength}, max {MaximumFileLength}";

        foreach (ContentLayoutGroup format in Formats)
        {
            yield return $"Format {format.Name}: {format.FileCount} files, {format.Length} bytes";
        }

        foreach (ContentLayoutGroup family in Families)
        {
            yield return $"Family {family.Name}: {family.FileCount} files, {family.Length} bytes";
        }
    }

    private static long Percentile(long[] orderedLengths, int percentile) =>
        orderedLengths[(int)(((long)orderedLengths.Length * percentile + 99) / 100 - 1)];

    private static ContentLayoutGroup[] Group(
        IReadOnlyList<ContentManifestEntry> entries, Func<ContentManifestEntry, string> key) =>
        entries.GroupBy(key, StringComparer.Ordinal)
            .Select(group => new ContentLayoutGroup(group.Key, group.Count(), group.Sum(entry => entry.Length)))
            .OrderByDescending(group => group.Length)
            .ThenBy(group => group.Name, StringComparer.Ordinal)
            .ToArray();

    private static string Family(string path)
    {
        int firstSeparator = path.IndexOf('/');
        if (firstSeparator < 0)
        {
            return path;
        }

        int secondSeparator = path.IndexOf('/', firstSeparator + 1);
        return secondSeparator < 0 ? path[..firstSeparator] : path[..secondSeparator];
    }
}
