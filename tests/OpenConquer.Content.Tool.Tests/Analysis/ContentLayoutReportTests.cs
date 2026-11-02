using OpenConquer.Content.Tool.Analysis;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Tests.Analysis;

public sealed class ContentLayoutReportTests
{
    [Fact]
    public void CreateReportsVerifiedInventoryCharacteristics()
    {
        ContentLayoutReport report = ContentLayoutReport.Create(CreateManifest());

        Assert.Equal(4, report.FileCount);
        Assert.Equal(21_236, report.Length);
        Assert.Equal(3, report.DistinctFileCount);
        Assert.Equal(17_012, report.DistinctLength);
        Assert.Equal(1, report.DuplicateFileCount);
        Assert.Equal(4_224, report.DuplicateLength);
        Assert.Equal((4_224L, 12_288L, 12_288L, 12_288L),
            (report.MedianFileLength, report.P95FileLength, report.P99FileLength, report.MaximumFileLength));

        Assert.Equal(
            [new ContentLayoutGroup("dds", 3, 20_736), new ContentLayoutGroup("unknown", 1, 500)],
            report.Formats);
        Assert.Equal(
            [new ContentLayoutGroup("data/icons", 3, 20_736), new ContentLayoutGroup("ini", 1, 500)],
            report.Families);
    }

    [Fact]
    public void CreateDoesNotDependOnInputOrder()
    {
        ContentManifest original = CreateManifest();
        ContentManifest reversed = new(original.ClientVersion, original.VersionMarkerSha256,
            original.Entries.Reverse().ToArray());

        Assert.Equal(ContentLayoutReport.Create(original).ToReportLines(),
            ContentLayoutReport.Create(reversed).ToReportLines());
    }

    [Fact]
    public void CreateRejectsEmptyInventory()
    {
        ContentManifest empty = new("5517", new string('0', 64), []);
        Assert.Throws<InvalidDataException>(() => ContentLayoutReport.Create(empty));
    }

    private static ContentManifest CreateManifest()
    {
        string repeatedHash = new('a', 64);
        return new ContentManifest("5517", new string('0', 64),
        [
            new ContentManifestEntry("Data/Icons/One.dds", "data/icons/one.dds", 4_224, repeatedHash, "dds"),
            new ContentManifestEntry("Data/Icons/Three.dds", "data/icons/three.dds", 12_288, new string('b', 64), "dds"),
            new ContentManifestEntry("Data/Icons/Two.dds", "data/icons/two.dds", 4_224, repeatedHash, "dds"),
            new ContentManifestEntry("ini/GameMap.dat", "ini/gamemap.dat", 500, new string('c', 64), "unknown"),
        ]);
    }
}
