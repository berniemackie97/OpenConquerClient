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

        if (IsWithin(contentSetRoot, scratchRoot) ||
            (previousRootPath is not null && IsWithin(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(previousRootPath)), scratchRoot)))
        {
            throw new InvalidOperationException("Benchmark scratch must be outside both content sets.");
        }

        RequireUnlinkedDivergence(scratchRoot, contentSetRoot);
        RequireUnlinkedDivergence(contentSetRoot, scratchRoot);
        if (previousRootPath is not null)
        {
            string previousRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(previousRootPath));
            RequireUnlinkedDivergence(scratchRoot, previousRoot);
            RequireUnlinkedDivergence(previousRoot, scratchRoot);
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

    private static void RequireUnlinkedDivergence(string path, string otherPath)
    {
        for (DirectoryInfo? current = new(path); current is not null; current = current.Parent)
        {
            if (IsWithin(current.FullName, otherPath))
            {
                break;
            }

            if (current.LinkTarget is not null || (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "Benchmark scratch must be outside both content sets without linked path ancestors.");
            }
        }
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
