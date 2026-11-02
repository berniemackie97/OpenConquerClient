using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Tool.Analysis;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Tests.Analysis;

public sealed class ContentArchiveSourceTests
{
    [Theory]
    [InlineData(CompressionLevel.NoCompression)]
    [InlineData(CompressionLevel.Optimal)]
    public void BuildProducesReproducibleArchivesAndVerifiedReadStreams(CompressionLevel compression)
    {
        using TemporarySourceTree content = new();
        using TemporarySourceTree output = new();
        byte[] first = [1, 2, 3, 4, 5];
        byte[] second = new byte[4096];
        content.WriteBytes("data/first.bin", first);
        content.WriteBytes("ini/second.bin", second);
        ContentManifest manifest = Manifest(
            Entry("ini/second.bin", second), Entry("data/first.bin", first));

        string path1 = output.ChildPath("first.zip");
        string path2 = output.ChildPath("repeat.zip");
        ClientContentRoot loose = new(content.RootPath);
        ContentArchiveBuilder.Build(loose, manifest, path1, compression);
        ContentArchiveBuilder.Build(loose, manifest, path2, compression);

        Assert.Equal(File.ReadAllBytes(path1), File.ReadAllBytes(path2));
        using ContentArchiveSource archive = new(path1, manifest);
        Assert.Equal(first, Read(archive.OpenRequiredRead("DATA/FIRST.BIN", ContentLookupMode.LooseOnly)));
        Assert.Equal(second, Read(archive.OpenRequiredRead("ini/second.bin", ContentLookupMode.LooseThenPackage)));
        Assert.False(archive.TryOpenRead("data/first.bin", ContentLookupMode.PackageOnly, out _));

        ContentReadPass[] passes = ContentReadProfiler.Measure(archive, manifest, reverseOrder: true);
        Assert.Equal(2, passes.Length);
        Assert.All(passes, pass =>
        {
            Assert.Equal(2, pass.FileCount);
            Assert.Equal(4101, pass.BytesRead);
        });
    }

    [Fact]
    public void OpenRejectsModifiedEntryContents()
    {
        using TemporarySourceTree output = new();
        string path = output.ChildPath("changed.zip");
        using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using Stream entry = zip.CreateEntry("data/file.bin").Open();
            entry.Write([2, 2, 2]);
        }

        Assert.Throws<InvalidDataException>(() => new ContentArchiveSource(path,
            Manifest(Entry("data/file.bin", [1, 2, 3]))));
    }

    [Fact]
    public void OpenRejectsRepeatedPaths()
    {
        using TemporarySourceTree output = new();
        string path = output.ChildPath("duplicate.zip");
        using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (int _ in Enumerable.Range(0, 2))
            {
                using Stream entry = zip.CreateEntry("data/file.bin").Open();
                entry.Write([1, 2, 3]);
            }
        }

        Assert.Throws<InvalidDataException>(() => new ContentArchiveSource(path,
            Manifest(Entry("data/file.bin", [1, 2, 3]))));
    }

    [Fact]
    public void OpenRejectsMissingDeclaredPaths()
    {
        using TemporarySourceTree output = new();
        string path = output.ChildPath("different.zip");
        using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using Stream entry = zip.CreateEntry("data/other.bin").Open();
            entry.Write([1, 2, 3]);
        }

        Assert.Throws<InvalidDataException>(() => new ContentArchiveSource(path,
            Manifest(Entry("data/file.bin", [1, 2, 3]))));
    }

    private static byte[] Read(Stream stream)
    {
        using (stream)
        using (MemoryStream output = new())
        {
            stream.CopyTo(output);
            return output.ToArray();
        }
    }

    private static ContentManifest Manifest(params ContentManifestEntry[] entries) =>
        new("5517", new string('0', 64), entries);

    private static ContentManifestEntry Entry(string path, byte[] bytes) =>
        new(path, path, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)), "unknown");
}
