using OpenConquer.Content.Tool.Analysis;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Tests.Analysis;

public sealed class ContentReadProfilerTests
{
    [Fact]
    public void MeasureReadsEveryContentFileTwice()
    {
        using TemporarySourceTree source = new();
        source.WriteBytes("data/items/first.bin", [1, 2, 3, 4]);
        source.WriteBytes("ini/second.bin", [5, 6, 7]);

        ContentManifest manifest = CreateManifest(
        [
            new ContentManifestEntry("data/items/first.bin", "data/items/first.bin", 4, new string('a', 64), "unknown"),
            new ContentManifestEntry("ini/second.bin", "ini/second.bin", 3, new string('b', 64), "unknown"),
        ]);

        ContentReadPass[] passes = ContentReadProfiler.Measure(new ClientContentRoot(source.RootPath), manifest);

        Assert.Equal(2, passes.Length);
        for (int index = 0; index < passes.Length; index++)
        {
            Assert.Equal(index + 1, passes[index].Number);
            Assert.Equal(2, passes[index].FileCount);
            Assert.Equal(7, passes[index].BytesRead);
            Assert.True(passes[index].Elapsed >= TimeSpan.Zero);
        }
    }

    [Fact]
    public void MeasureRejectsChangedPayloadLength()
    {
        using TemporarySourceTree source = new();
        source.WriteBytes("data/file.bin", [1, 2, 3, 4]);
        ContentManifest manifest = CreateManifest(
            [new ContentManifestEntry("data/file.bin", "data/file.bin", 3, new string('a', 64), "unknown")]);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ContentReadProfiler.Measure(new ClientContentRoot(source.RootPath), manifest));
        Assert.Contains("verified length", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MeasureRejectsMissingContent()
    {
        using TemporarySourceTree source = new();
        source.WriteBytes("data/present.bin", [1]);
        ContentManifest manifest = CreateManifest(
            [new ContentManifestEntry("data/missing.bin", "data/missing.bin", 1, new string('a', 64), "unknown")]);

        Assert.Throws<FileNotFoundException>(() =>
            ContentReadProfiler.Measure(new ClientContentRoot(source.RootPath), manifest));
    }

    private static ContentManifest CreateManifest(ContentManifestEntry[] entries) =>
        new("5517", new string('0', 64), entries);
}
