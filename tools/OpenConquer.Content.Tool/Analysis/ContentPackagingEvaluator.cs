using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Tool.Manifest;
using OpenConquer.Content.Tool.Verify;

namespace OpenConquer.Content.Tool.Analysis;

internal static class ContentPackagingEvaluator
{
    public static ContentPackagingReport Evaluate(string contentSetRootPath, string scratchRootPath,
        uint? mapDataId = null, bool allMaps = false, bool availableMaps = false,
        string? previousRootPath = null)
    {
        ContentManifest manifest = ContentSetVerifier.Verify(contentSetRootPath,
            mapDataId, allMaps, availableMaps);
        ContentManifest? previous = previousRootPath is null ? null :
            ContentSetVerifier.Verify(previousRootPath, mapDataId, allMaps, availableMaps);

        string contentSetRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentSetRootPath));
        string scratchRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(scratchRootPath));
        HostFileSystemGuard.RequireDirectory(scratchRoot, "benchmark scratch directory");

        string physicalScratch = ResolvePhysicalDirectory(scratchRoot);
        if (IsWithin(ResolvePhysicalDirectory(contentSetRoot), physicalScratch) ||
            (previousRootPath is not null && IsWithin(
                ResolvePhysicalDirectory(previousRootPath), physicalScratch)))
        {
            throw new InvalidOperationException("Benchmark scratch must be outside both content sets.");
        }

        ClientContentRoot source = new(Path.Combine(contentSetRoot, "payload"));
        ContentReadPass[] forward = ContentReadProfiler.Measure(source, manifest);
        ContentReadPass[] reverse = ContentReadProfiler.Measure(source, manifest, reverseOrder: true);

        string stage = Path.Combine(scratchRoot, $".content-evaluation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stage);

        try
        {
            ContentPackagingCandidate[] candidates =
            [
                EvaluateCandidate(source, manifest, previous, stage, "Stored ZIP",
                    CompressionLevel.NoCompression, mapDataId, allMaps, availableMaps),
                EvaluateCandidate(source, manifest, previous, stage, "Compressed ZIP",
                    CompressionLevel.Optimal, mapDataId, allMaps, availableMaps),
            ];

            return new ContentPackagingReport(manifest.FileCount, manifest.Length, forward,
                reverse, candidates, previous is null ? null : ContentSetDeltaReport.Compare(previous, manifest));
        }
        finally
        {
            Directory.Delete(stage, recursive: true);
        }
    }

    private static ContentPackagingCandidate EvaluateCandidate(IClientContentSource source,
        ContentManifest manifest, ContentManifest? previous, string stage,
        string name, CompressionLevel compression, uint? mapDataId, bool allMaps, bool availableMaps)
    {
        string first = Path.Combine(stage, compression == CompressionLevel.NoCompression ? "stored.zip" : "compressed.zip");
        string second = Path.Combine(stage, compression == CompressionLevel.NoCompression ? "stored-repeat.zip" : "compressed-repeat.zip");

        long start = Stopwatch.GetTimestamp();
        ContentArchiveBuilder.Build(source, manifest, first, compression);
        TimeSpan buildTime = Stopwatch.GetElapsedTime(start);
        ContentArchiveBuilder.Build(source, manifest, second, compression);

        using (FileStream original = File.OpenRead(first))
        using (FileStream repeated = File.OpenRead(second))
        {
            if (!SHA256.HashData(original).AsSpan().SequenceEqual(SHA256.HashData(repeated)))
            {
                throw new InvalidDataException($"{name} bytes differ between repeated builds.");
            }
        }

        using ContentArchiveSource archive = new(first, manifest);
        (ContentDecodeMetrics looseDecode, ContentDecodeMetrics archiveDecode) =
            ContentDecodeWorkload.Compare(source, archive, manifest, mapDataId, allMaps || availableMaps);
        return new ContentPackagingCandidate(
            name, new FileInfo(first).Length, buildTime,
            ContentReadProfiler.Measure(archive, manifest),
            ContentReadProfiler.Measure(archive, manifest, reverseOrder: true),
            looseDecode, archiveDecode,
            previous is null ? null : archive.CompressedBytesForNewPayloads(manifest, previous));
    }

    private static string ResolvePhysicalDirectory(string path)
    {
        DirectoryInfo directory = new(Path.GetFullPath(path));
        Stack<string> segments = new();
        while (directory.Parent is DirectoryInfo parent)
        {
            segments.Push(directory.Name);
            directory = parent;
        }

        string current = directory.FullName;
        while (segments.TryPop(out string? segment))
        {
            DirectoryInfo next = new(Path.Combine(current, segment));
            current = next.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? next.FullName;
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }

    private static bool IsWithin(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        return relative == "." ||
            (!Path.IsPathRooted(relative) &&
             relative != ".." &&
             !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }
}
