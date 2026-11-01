using System.IO.Compression;
using System.Text;
using OpenConquer.Content.Map;
using OpenConquer.Content.Tool.Import;
using OpenConquer.Content.Tool.Manifest;
using OpenConquer.Content.Tool.Verify;
using OpenConquer.Tests.Fixtures;

namespace OpenConquer.Content.Tool.Tests.Import;

public sealed class WorldContentImportTests
{
    private static readonly string[] s_sceneNames =
        ["stand01", "stand02", "stand03", "stand05", "stand06", "stand08"];

    [Fact]
    public void Import_SelectedMap_ContainsExactlyItsRequiredResources()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destination = new();
        WriteMapSource(source);

        string contentSet = destination.ChildPath("world");
        ContentManifest manifest = ContentSetImporter.Import(source.RootPath, contentSet, 1010);

        Assert.Equal(manifest.FileCount, ContentSetVerifier.Verify(contentSet, 1010).FileCount);
        Assert.Contains(manifest.Entries, static entry => entry.PathKey == "map/map/newbie.7z");
        Assert.Contains(manifest.Entries, static entry => entry.PathKey == "map/puzzle/newbie.pul");
        Assert.Contains(manifest.Entries, static entry => entry.PathKey == "map/puzzle/newbiebg.pul");
        Assert.Contains(manifest.Entries, static entry => entry.PathKey == "ani/newbie.ani");
        Assert.Contains(manifest.Entries, static entry => entry.PathKey == "ani/mapscene.ani");
        Assert.Contains(manifest.Entries, static entry =>
            entry.PathKey == "data/map/mapobj/newbie/newbie17.dds");
        Assert.Contains(manifest.Entries, static entry => entry.PathKey == "map/scene/stand06.scene");
        Assert.Contains(manifest.Entries, static entry => entry.PathKey == "data/map/stand06.dds");
        Assert.Contains(manifest.Entries, static entry =>
            entry.PathKey == "data/map/puzzle/newbie/bg000.dds");
        Assert.DoesNotContain(manifest.Entries, static entry => entry.PathKey == "data/map/unused.dds");
        Assert.DoesNotContain(manifest.Entries, static entry => entry.PathKey == "ini/package.ini");

        Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));
    }

    [Fact]
    public void Import_AllMaps_IncludesEveryCatalogEntryWithoutDuplicatingSharedFiles()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destination = new();
        WriteMapSource(source);
        WriteCatalog(source, (1010u, "map/map/newbie.7z"), (1011u, "map/map/newbie.7z"));

        string oneMapPath = destination.ChildPath("single");
        string allMapsPath = destination.ChildPath("all");
        ContentManifest single = ContentSetImporter.Import(source.RootPath, oneMapPath, 1010);
        ContentManifest complete = ContentSetImporter.Import(source.RootPath, allMapsPath, allMaps: true);

        Assert.Equal(single.Entries, complete.Entries);
        Assert.Equal(complete.FileCount, ContentSetVerifier.Verify(allMapsPath, allMaps: true).FileCount);
        Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(allMapsPath));
    }

    [Fact]
    public void Import_AllMaps_ReportsMissingCatalogMapAndDoesNotPublish()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destination = new();
        WriteMapSource(source);
        WriteCatalog(source, (1010u, "map/map/newbie.7z"), (1011u, "map/map/missing.7z"));

        string target = destination.ChildPath("all");
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ContentSetImporter.Import(source.RootPath, target, allMaps: true));

        Assert.Contains("1011", exception.Message, StringComparison.Ordinal);
        Assert.Contains("map/map/missing.7z", exception.Message, StringComparison.Ordinal);
        Assert.IsType<FileNotFoundException>(exception.InnerException);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Import_AvailableMaps_DeclaresMissingArchivesAndVerifiesExactClosure()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destination = new();
        WriteMapSource(source);
        WriteCatalog(source, (1010u, "map/map/newbie.7z"), (1712u, "map/map/kunlun1.7z"));

        string contentSet = destination.ChildPath("available");
        ContentManifest manifest = ContentSetImporter.Import(source.RootPath, contentSet, availableMaps: true);

        MissingMapArchive missing = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<MissingMapArchive>>(manifest.MissingMapArchives));
        Assert.Equal(1712u, missing.MapDataId);
        Assert.Equal("map/map/kunlun1.7z", missing.ArchivePath);
        Assert.Equal(ContentManifest.AvailableMapsSchemaVersion, manifest.SchemaVersion);
        Assert.Contains(manifest.Entries, static entry => entry.PathKey == "map/map/newbie.7z");
        Assert.DoesNotContain(manifest.Entries, static entry => entry.PathKey == "map/map/kunlun1.7z");

        ContentManifest verified = ContentSetVerifier.Verify(contentSet, availableMaps: true);
        Assert.Equal(manifest.Entries, verified.Entries);
        Assert.Equal(manifest.MissingMapArchives, verified.MissingMapArchives);
        Assert.Equal(File.ReadAllBytes(source.ChildPath("ini/GameMap.dat")),
            File.ReadAllBytes(Path.Combine(contentSet, "payload/ini/gamemap.dat")));

        Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));
        Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet, allMaps: true));
    }

    [Fact]
    public void Import_AvailableMaps_StillRejectsMissingDependenciesForExistingMap()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destination = new();
        WriteMapSource(source);
        WriteCatalog(source, (1010u, "map/map/newbie.7z"), (1712u, "map/map/kunlun1.7z"));
        File.Delete(source.ChildPath("map/Scene/stand08.scene"));

        string target = destination.ChildPath("available");
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ContentSetImporter.Import(source.RootPath, target, availableMaps: true));
        Assert.Contains("map data ID 1010", error.Message, StringComparison.Ordinal);
        Assert.IsType<FileNotFoundException>(error.InnerException);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Verify_AvailableMaps_RejectsInventedMissingArchivePaths()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destination = new();
        WriteMapSource(source);
        WriteCatalog(source, (1010u, "map/map/newbie.7z"), (1712u, "map/map/kunlun1.7z"));

        string target = destination.ChildPath("available");
        ContentSetImporter.Import(source.RootPath, target, availableMaps: true);

        string manifestPath = Path.Combine(target, "manifest.json");
        string manifest = File.ReadAllText(manifestPath);
        Assert.Contains("map/map/kunlun1.7z", manifest, StringComparison.Ordinal);
        File.WriteAllText(manifestPath,
            manifest.Replace("map/map/kunlun1.7z", "map/map/unrelated.7z", StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(target, availableMaps: true));
    }

    [Fact]
    public void Import_AvailableMaps_FailsWhenEveryArchiveIsMissing()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destination = new();
        WriteMapSource(source);
        WriteCatalog(source, (1712u, "map/map/kunlun1.7z"));

        string target = destination.ChildPath("available");
        Assert.Throws<InvalidDataException>(() =>
            ContentSetImporter.Import(source.RootPath, target, availableMaps: true));
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Import_AvailableMaps_RejectsUnsupportedCatalogArchivePath()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destination = new();
        WriteMapSource(source);
        WriteCatalog(source, (1010u, "map/map/newbie.7z"), (1712u, "map/map/unexpected.dat"));

        string target = destination.ChildPath("available");
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            ContentSetImporter.Import(source.RootPath, target, availableMaps: true));
        Assert.Contains("1712", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Import_AvailableMaps_RejectsConflictingSelection()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destination = new();
        source.WriteStartupSnapshot();

        Assert.Throws<ArgumentException>(() => ContentSetImporter.Import(
            source.RootPath, destination.ChildPath("invalid"), allMaps: true, availableMaps: true));
        Assert.Throws<ArgumentException>(() => ContentSetImporter.Import(
            source.RootPath, destination.ChildPath("invalid"), 1010, availableMaps: true));
    }

    [Fact]
    public void Import_AllMapsCannotBeCombinedWithOneMap()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destination = new();
        source.WriteStartupSnapshot();

        Assert.Throws<ArgumentException>(() => ContentSetImporter.Import(
            source.RootPath, destination.ChildPath("invalid"), 1010, allMaps: true));
    }

    [Fact]
    public void Import_SelectedMapMissingScene_LeavesNoPartialContentSet()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree destination = new();
        WriteMapSource(source);
        File.Delete(source.ChildPath("map/Scene/stand08.scene"));

        string contentSet = destination.ChildPath("world");
        Assert.Throws<FileNotFoundException>(() => ContentSetImporter.Import(source.RootPath, contentSet, 1010));
        Assert.False(Directory.Exists(contentSet));
    }

    private static void WriteCatalog(TemporarySourceTree source, params (uint Id, string Path)[] maps)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true))
        {
            writer.Write((uint)maps.Length);
            foreach ((uint id, string path) in maps)
            {
                byte[] encoded = Encoding.Latin1.GetBytes(path);
                writer.Write(id);
                writer.Write((uint)encoded.Length);
                writer.Write(encoded);
                writer.Write(0u);
            }
        }

        source.WriteBytes("ini/GameMap.dat", stream.ToArray());
    }

    private static void WriteMapSource(TemporarySourceTree source)
    {
        source.WriteStartupSnapshot();
        string fixture = Path.Combine(AppContext.BaseDirectory, "RetailFixture");
        source.WriteBytes("ini/GameMap.dat", File.ReadAllBytes(Path.Combine(fixture, "ini", "GameMap.dat")));
        source.WriteBytes("map/map/newbie.7z", File.ReadAllBytes(Path.Combine(fixture, "map", "newbie.7z")));
        source.WriteBytes("map/puzzle/newbie.pul",
            File.ReadAllBytes(Path.Combine(fixture, "map", "puzzle", "newbie.pul")));
        source.WriteBytes("map/puzzle/newbiebg.pul",
            File.ReadAllBytes(Path.Combine(fixture, "map", "puzzle", "newbiebg.pul")));

        using (FileStream compressed = File.OpenRead(Path.Combine(fixture, "ani", "newbie.ani.zlib")))
        using (ZLibStream decoded = new(compressed, CompressionMode.Decompress))
        using (MemoryStream buffer = new())
        {
            decoded.CopyTo(buffer);
            source.WriteBytes("ani/newbie.ani", buffer.ToArray());
        }

        StringBuilder index = new();
        index.Append(File.ReadAllText(Path.Combine(fixture, "ani", "mapscene-newbie-sections.ani"), Encoding.Latin1));
        foreach (string name in s_sceneNames)
        {
            source.WriteBytes($"map/Scene/{name}.scene", TerrainSceneTestData.Create(name, 1, 1));
            index.Append('[').Append(name).Append(".tga]\nFrameAmount=1\nFrame0=data/map/")
                .Append(name).Append(".dds\n");
            source.WriteText($"data/map/{name}.dds", $"DDS {name}");
        }
        index.Append("[unreferenced.tga]\nFrameAmount=1\nFrame0=data/map/unused.dds\n");
        source.WriteText("ani/MapScene.ani", index.ToString());
        source.WriteText("data/map/unused.dds", "DDS unused");

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(source.RootPath);
        GameMapContent map = GameMapContent.Load(contentSource, GameMapCatalog.Load(contentSource), 1010);
        GameMapPuzzleResources puzzles = GameMapPuzzleResources.Load(contentSource, map);
        foreach (GameMapSceneryAniBinding binding in GameMapSceneryAniBindings.Load(contentSource, map.Scene))
        {
            foreach (string framePath in binding.Animation.FramePaths)
            {
                source.WriteText(framePath, "DDS scenery frame");
            }
        }
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase)
        {
            map.CellGrid.PuzzlePath,
        };
        foreach (GameMapPositionedOverlayRecord record in map.Scene.PositionedOverlays)
        {
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is GameMapPuzzleOverlayEntry puzzle)
                {
                    paths.Add(puzzle.PuzzlePath);
                }
            }
        }
        foreach (string path in paths)
        {
            GameMapPuzzleBitmap bitmap = puzzles.GetRequired(path);
            HashSet<ushort> tileIndices = [];
            foreach (ushort tile in bitmap.Tiles)
            {
                if (tile == ushort.MaxValue || !tileIndices.Add(tile))
                {
                    continue;
                }
                foreach (string frame in puzzles.GetRequiredTileAnimation(path, tile).FramePaths)
                {
                    source.WriteText(frame, "DDS puzzle frame");
                }
            }
        }
    }
}
