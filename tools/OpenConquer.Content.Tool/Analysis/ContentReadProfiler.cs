using System.Diagnostics;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Analysis;

internal readonly record struct ContentReadPass(int Number, int FileCount, long BytesRead, TimeSpan Elapsed);

internal static class ContentReadProfiler
{
    public static ContentReadPass[] Measure(IClientContentSource source, ContentManifest manifest)
    {
        byte[] buffer = new byte[64 * 1024];
        ContentReadPass[] passes = new ContentReadPass[2];

        for (int pass = 0; pass < passes.Length; pass++)
        {
            long start = Stopwatch.GetTimestamp();
            long totalBytes = 0;

            foreach (ContentManifestEntry entry in manifest.Entries)
            {
                using Stream stream = source.OpenRequiredRead(entry.PathKey, ContentLookupMode.LooseThenPackage);
                long fileBytes = 0;

                int read;
                while ((read = stream.Read(buffer)) != 0)
                {
                    if (read > entry.Length - fileBytes)
                    {
                        throw new InvalidDataException($"Content file '{entry.PathKey}' exceeded its verified length.");
                    }

                    fileBytes += read;
                }

                if (fileBytes != entry.Length)
                {
                    throw new InvalidDataException($"Content file '{entry.PathKey}' changed length.");
                }

                totalBytes = checked(totalBytes + fileBytes);
            }

            passes[pass] = new ContentReadPass(pass + 1, manifest.FileCount,
                totalBytes, Stopwatch.GetElapsedTime(start));
        }

        return passes;
    }
}
