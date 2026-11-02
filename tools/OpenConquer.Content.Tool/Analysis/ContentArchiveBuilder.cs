using System.IO.Compression;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Analysis;

internal static class ContentArchiveBuilder
{
    private static readonly DateTimeOffset s_timestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Build(IClientContentSource content, ContentManifest manifest,
        string destinationPath, CompressionLevel compression)
    {
        using FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using ZipArchive archive = new(destination, ZipArchiveMode.Create, leaveOpen: false);
        foreach (ContentManifestEntry item in manifest.Entries.OrderBy(entry => entry.PathKey, StringComparer.Ordinal))
        {
            ZipArchiveEntry entry = archive.CreateEntry(item.PathKey, compression);
            entry.LastWriteTime = s_timestamp;
            entry.ExternalAttributes = 0;

            using Stream source = content.OpenRequiredRead(item.PathKey, ContentLookupMode.LooseOnly);
            using Stream output = entry.Open();
            source.CopyTo(output);
        }
    }
}
