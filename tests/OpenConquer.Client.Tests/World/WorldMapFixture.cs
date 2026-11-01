using System.IO.Compression;
using OpenConquer.Content;
using OpenConquer.Content.Map;

namespace OpenConquer.Client.Tests.World;

internal sealed class WorldMapFixture : IDisposable
{
    private readonly string _root;

    public WorldMapFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "OpenConquer.WorldMapFixture",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        try
        {
            string fixture = Path.Combine(AppContext.BaseDirectory, "RetailMapFixture");
            Write("ini/GameMap.dat", File.ReadAllBytes(Path.Combine(fixture, "ini", "GameMap.dat")));
            Write("map/map/newbie.7z", File.ReadAllBytes(Path.Combine(fixture, "map", "newbie.7z")));
            Write("map/puzzle/newbie.pul", File.ReadAllBytes(Path.Combine(fixture, "map", "puzzle", "newbie.pul")));
            Write("map/puzzle/newbiebg.pul", File.ReadAllBytes(Path.Combine(fixture, "map", "puzzle", "newbiebg.pul")));

            using (FileStream compressed = File.OpenRead(Path.Combine(fixture, "ani", "newbie.ani.zlib")))
            using (ZLibStream decoded = new(compressed, CompressionMode.Decompress))
            using (MemoryStream content = new())
            {
                decoded.CopyTo(content);
                Write("ani/newbie.ani", content.ToArray());
            }

            ClientContentRoot source = new(_root);
            Map = GameMapContent.Load(source, GameMapCatalog.Load(source), 1010);
            Puzzles = GameMapPuzzleResources.Load(source, Map);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public GameMapContent Map
    {
        get;
    }

    public GameMapPuzzleResources Puzzles
    {
        get;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void Write(string path, byte[] bytes)
    {
        string destination = Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllBytes(destination, bytes);
    }
}
