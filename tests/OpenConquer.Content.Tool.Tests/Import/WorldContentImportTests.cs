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
        Assert.Contains(manifest.Entries, static entry => entry.PathKey == "map/scene/stand06.scene");
        Assert.Contains(manifest.Entries, static entry => entry.PathKey == "data/map/stand06.dds");
        Assert.Contains(manifest.Entries, static entry =>
            entry.PathKey == "data/map/puzzle/newbie/bg000.dds");
        Assert.DoesNotContain(manifest.Entries, static entry => entry.PathKey == "data/map/unused.dds");
        Assert.DoesNotContain(manifest.Entries, static entry => entry.PathKey == "ini/package.ini");

        Assert.Throws<InvalidDataException>(() => ContentSetVerifier.Verify(contentSet));
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
