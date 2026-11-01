using System.IO.Compression;
using OpenConquer.Client.World;
using OpenConquer.Content;
using OpenConquer.Content.Map;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldPuzzleDrawListTests
{
    private const int GroundOffsetX = -9 * 256;
    private const int GroundOffsetY = -3 * 256;

    [Fact]
    public void RetailNewbieGround_EnumeratesVisibleANIFramesInTileOrder()
    {
        using RetailNewbieSource retail = new();
        WorldPuzzleProjection projection = new(800, 600, GroundOffsetX, GroundOffsetY, 3500, 1400, 1, 1);

        IReadOnlyList<WorldPuzzleTile> tiles = WorldPuzzleDrawList.Ground(retail.Map, retail.Puzzles, projection, 0);

        Assert.NotEmpty(tiles);
        Assert.InRange(tiles.Count, 1, 30);
        Assert.All(tiles, tile =>
        {
            Assert.Equal(256, tile.Size);
            Assert.Equal(0, tile.FrameIndex);
            Assert.Equal(0, tile.X % 256);
            Assert.Equal(0, tile.Y % 256);
            Assert.True(tile.X < projection.ScreenWidth && tile.Y < projection.ScreenHeight);
            Assert.True(tile.X + tile.Size > 0 && tile.Y + tile.Size > 0);
            Assert.StartsWith("data/map/puzzle/", tile.Animation.FramePaths[tile.FrameIndex], StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void RetailNewbieBackdrop_UsesPositionedPuzzleAndParallax()
    {
        using RetailNewbieSource retail = new();
        WorldPuzzleProjection projection = new(800, 600, 0, 0, 3500, 1400, 1, 1);

        IReadOnlyList<WorldPuzzleTile> tiles = WorldPuzzleDrawList.PositionedPuzzles(retail.Map, retail.Puzzles, projection);

        Assert.NotEmpty(tiles);
        Assert.InRange(tiles.Count, 1, 30);
        Assert.All(tiles, tile =>
        {
            Assert.Equal(256, tile.Size);
            Assert.Equal(0, tile.FrameIndex);
            Assert.StartsWith("data/map/puzzle/newbie/bg", tile.Animation.FramePaths[tile.FrameIndex], StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void LargerViewport_DoesNotExpandTheRetail800By600TileEnumeration()
    {
        using RetailNewbieSource retail = new();
        IReadOnlyList<WorldPuzzleTile> baseTiles = WorldPuzzleDrawList.Ground(retail.Map, retail.Puzzles,
            new WorldPuzzleProjection(800, 600, GroundOffsetX, GroundOffsetY, 3500, 1400, 1, 1), 0);
        IReadOnlyList<WorldPuzzleTile> larger = WorldPuzzleDrawList.Ground(retail.Map, retail.Puzzles,
            new WorldPuzzleProjection(1024, 768, GroundOffsetX, GroundOffsetY, 3500, 1400, 1, 1), 0);

        Assert.Equal(baseTiles.Count, larger.Count);
        Assert.Equal(baseTiles.Select(t => t.Animation.Name), larger.Select(t => t.Animation.Name));
    }

    [Fact]
    public void RejectsInvalidProjectionBeforeReadingTiles()
    {
        using RetailNewbieSource retail = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => WorldPuzzleDrawList.Ground(retail.Map, retail.Puzzles,
            new WorldPuzzleProjection(800, 600, 0, 0, 0, 0, 1, 0), 0));
    }

    private sealed class RetailNewbieSource : IDisposable
    {
        private readonly string _root;

        public RetailNewbieSource()
        {
            _root = Path.Combine(Path.GetTempPath(), "OpenConquer.WorldPuzzleDrawListTests",
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

        public GameMapContent Map { get; }
        public GameMapPuzzleResources Puzzles { get; }

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
}
