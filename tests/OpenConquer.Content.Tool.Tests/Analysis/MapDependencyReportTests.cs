using OpenConquer.Content.Tool.Analysis;
using OpenConquer.Content.Tool.Manifest;

namespace OpenConquer.Content.Tool.Tests.Analysis;

public sealed class MapDependencyReportTests
{
    [Fact]
    public void CreateClassifiesStartupAndPerMapDependenciesWithoutDoubleCounting()
    {
        MapDependencyReport report = MapDependencyReport.Create(CreateManifest(), Startup(),
            Maps(), excludedMapCount: 3);

        Assert.Equal(3, report.ExcludedMapCount);
        Assert.Equal(
        [
            new MapDependencyGroup("Startup only", 1, 10),
            new MapDependencyGroup("Startup and maps", 1, 20),
            new MapDependencyGroup("Exclusive to one map", 2, 80),
            new MapDependencyGroup("Shared across maps", 1, 40),
        ], report.Groups);

        Assert.Equal(
        [
            new MapDependencyMap(1010, 3, 90, 1, 30),
            new MapDependencyMap(1929, 2, 90, 1, 50),
        ], report.Maps);
    }

    [Fact]
    public void CreateIsIndependentOfManifestAndMapEnumerationOrder()
    {
        ContentManifest original = CreateManifest();
        ContentManifest reversed = new(original.ClientVersion, original.VersionMarkerSha256,
            original.Entries.Reverse().ToArray());
        Dictionary<uint, IReadOnlyList<ClientContentRequirement>> reverseMaps = Maps()
            .Reverse().ToDictionary(entry => entry.Key, entry => entry.Value);

        Assert.Equal(MapDependencyReport.Create(original, Startup(), Maps()).ToReportLines(),
            MapDependencyReport.Create(reversed, Startup(), reverseMaps).ToReportLines());
    }

    [Fact]
    public void CreateRejectsUnknownDependencies()
    {
        Dictionary<uint, IReadOnlyList<ClientContentRequirement>> maps = new()
        {
            [1010] = [Require("data/missing.bin")],
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            MapDependencyReport.Create(CreateManifest(), Startup(), maps));
        Assert.Contains("data/missing.bin", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRejectsUnownedManifestFiles()
    {
        Dictionary<uint, IReadOnlyList<ClientContentRequirement>> maps = new()
        {
            [1010] = [Require("data/b.bin")],
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            MapDependencyReport.Create(CreateManifest(), Startup(), maps));
        Assert.Contains("has no dependency owner", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRequiresAtLeastOneMap()
    {
        Assert.Throws<InvalidDataException>(() => MapDependencyReport.Create(CreateManifest(),
            Startup(), new Dictionary<uint, IReadOnlyList<ClientContentRequirement>>()));
    }

    private static ContentManifest CreateManifest() =>
        new("5517", new string('0', 64),
        [
            Entry("data/a.bin", 10),
            Entry("data/b.bin", 20),
            Entry("data/c.bin", 30),
            Entry("data/d.bin", 40),
            Entry("data/e.bin", 50),
        ]);

    private static ContentManifestEntry Entry(string path, long length) =>
        new(path, path, length, new string('a', 64), "unknown");

    private static ClientContentRequirement Require(string path) =>
        new(path, ContentLookupMode.LooseOnly);

    private static ClientContentRequirement[] Startup() =>
        [Require("data/a.bin"), Require("data/b.bin")];

    private static Dictionary<uint, IReadOnlyList<ClientContentRequirement>> Maps() =>
        new()
        {
            [1929] = [Require("data/d.bin"), Require("data/e.bin")],
            [1010] = [Require("data/b.bin"), Require("data/c.bin"),
                Require("data/d.bin"), Require("data/d.bin")],
        };
}
