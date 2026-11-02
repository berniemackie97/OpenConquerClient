using OpenConquer.Content.Tool.Analysis;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Tests.Analysis;

public sealed class ContentSetDeltaReportTests
{
    [Fact]
    public void CompareSeparatesPathChangesFromDistinctPayloadReuse()
    {
        ContentManifest previous = Manifest(
            Entry("data/stable.dds", 10, 'a'),
            Entry("data/changed.dds", 20, 'b'),
            Entry("data/removed.dds", 30, 'd'));
        ContentManifest candidate = Manifest(
            Entry("data/stable.dds", 10, 'a'),
            Entry("data/changed.dds", 25, 'c'),
            Entry("data/copied.dds", 10, 'a'),
            Entry("data/new-copy.dds", 25, 'c'),
            Entry("data/new.dds", 5, 'e'));

        ContentSetDeltaReport report = ContentSetDeltaReport.Compare(previous, candidate);

        Assert.Equal(new ContentSetDeltaGroup(1, 10), report.UnchangedPaths);
        Assert.Equal(new ContentSetDeltaGroup(3, 40), report.AddedPaths);
        Assert.Equal(new ContentSetDeltaGroup(1, 25), report.ReplacedPaths);
        Assert.Equal(new ContentSetDeltaGroup(1, 30), report.RemovedPaths);
        Assert.Equal(new ContentSetDeltaGroup(1, 10), report.ReusablePayloads);
        Assert.Equal(new ContentSetDeltaGroup(2, 30), report.NewPayloads);
    }

    [Fact]
    public void CompareCountsReusedPayloadAtADifferentPath()
    {
        ContentManifest previous = Manifest(Entry("data/old.dds", 15, 'a'));
        ContentManifest candidate = Manifest(Entry("data/new.dds", 15, 'a'));

        ContentSetDeltaReport report = ContentSetDeltaReport.Compare(previous, candidate);

        Assert.Equal(new ContentSetDeltaGroup(1, 15), report.AddedPaths);
        Assert.Equal(new ContentSetDeltaGroup(1, 15), report.RemovedPaths);
        Assert.Equal(new ContentSetDeltaGroup(1, 15), report.ReusablePayloads);
        Assert.Equal(default, report.NewPayloads);
    }

    [Fact]
    public void CompareTreatsLengthChangeAsNewContentEvenWithMatchingDigest()
    {
        ContentManifest previous = Manifest(Entry("data/file.dds", 10, 'a'));
        ContentManifest candidate = Manifest(Entry("data/file.dds", 20, 'a'));

        ContentSetDeltaReport report = ContentSetDeltaReport.Compare(previous, candidate);

        Assert.Equal(new ContentSetDeltaGroup(1, 20), report.ReplacedPaths);
        Assert.Equal(new ContentSetDeltaGroup(1, 20), report.NewPayloads);
    }

    [Fact]
    public void CompareIsIndependentOfManifestOrdering()
    {
        ContentManifest previous = Manifest(
            Entry("data/a.dds", 10, 'a'), Entry("data/b.dds", 20, 'b'));
        ContentManifest candidate = Manifest(
            Entry("data/b.dds", 10, 'a'), Entry("data/c.dds", 30, 'c'));

        ContentSetDeltaReport expected = ContentSetDeltaReport.Compare(previous, candidate);
        ContentSetDeltaReport reversed = ContentSetDeltaReport.Compare(
            Manifest(previous.Entries.Reverse().ToArray()),
            Manifest(candidate.Entries.Reverse().ToArray()));

        Assert.Equal(expected, reversed);
        Assert.Equal(expected.ToReportLines(), reversed.ToReportLines());
    }

    private static ContentManifest Manifest(params ContentManifestEntry[] entries) =>
        new("5517", new string('0', 64), entries);

    private static ContentManifestEntry Entry(string path, long length, char digest) =>
        new(path, path, length, new string(digest, 64), "dds");
}
