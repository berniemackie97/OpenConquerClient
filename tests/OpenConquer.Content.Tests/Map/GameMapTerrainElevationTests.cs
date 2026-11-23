using System.Text;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

/// <summary>
/// Tail-link elevation composition against CMapSceneryComponent_AttachTerrainObjectGroupToTileGrid (0x635FC5).
/// </summary>
public sealed class GameMapTerrainElevationTests
{
    [Fact]
    public void BaseOnlyTilesHaveZeroAdjustmentAndOffGridTilesHaveNoEntry()
    {
        GameMapCellGrid grid = GameMapCellGrid.Parse(Grid(4, 3, static (x, y) => (short)(x * 10 - y)));

        GameMapTerrainElevation elevation = GameMapTerrainElevation.Compose(grid, []);

        Assert.True(elevation.TryGetTail(3, 2, out int tail));
        Assert.Equal(28, tail);
        Assert.True(elevation.TryGetAdjusted(3, 2, out int adjusted));
        Assert.Equal(0, adjusted);
        Assert.False(elevation.TryGetAdjusted(4, 0, out _));
        Assert.False(elevation.TryGetAdjusted(0, -1, out _));
        Assert.False(elevation.TryGetTail(-1, 0, out _));
    }

    [Fact]
    public void ObjectCellsAttachAtAnchorMinusCellCoordinatesUsingRowMajorRecords()
    {
        GameMapCellGrid grid = GameMapCellGrid.Parse(Grid(6, 6, static (_, _) => 5));
        // Group anchor (2,1) + object offset (2,3) = object anchor (4,4); 3x2 cells, record index w*y+x.
        GameMapTerrainObject terrain = Terrain(width: 3, height: 2, anchorOffsetX: 2, anchorOffsetY: 3,
            deltas: [10, 20, 30, 40, 50, 60]);

        GameMapTerrainElevation elevation = GameMapTerrainElevation.Compose(grid, [Placement(2, 1, terrain)]);

        Assert.Equal(10, Adjusted(elevation, 4, 4));
        Assert.Equal(20, Adjusted(elevation, 3, 4));
        Assert.Equal(30, Adjusted(elevation, 2, 4));
        Assert.Equal(40, Adjusted(elevation, 4, 3));
        Assert.Equal(50, Adjusted(elevation, 3, 3));
        Assert.Equal(60, Adjusted(elevation, 2, 3));
        Assert.Equal(0, Adjusted(elevation, 5, 4));
        Assert.True(elevation.TryGetTail(2, 3, out int tail));
        Assert.Equal(65, tail);
    }

    [Fact]
    public void LaterAttachmentsAccumulateOnThePreviousTailWithSixteenBitWrap()
    {
        GameMapCellGrid grid = GameMapCellGrid.Parse(Grid(2, 2, static (_, _) => 32000));
        GameMapTerrainObject first = Terrain(1, 1, 0, 0, [700]);
        GameMapTerrainObject second = Terrain(1, 1, 0, 0, [100]);

        GameMapTerrainElevation elevation = GameMapTerrainElevation.Compose(grid,
            [Placement(1, 1, first), Placement(1, 1, second)]);

        Assert.True(elevation.TryGetTail(1, 1, out int tail));
        Assert.Equal(unchecked((short)32800), tail);
        Assert.Equal(unchecked((short)32800) - 32000, Adjusted(elevation, 1, 1));
    }

    [Fact]
    public void CellsLandingOutsideTheGridAreSkipped()
    {
        GameMapCellGrid grid = GameMapCellGrid.Parse(Grid(2, 2, static (_, _) => 0));
        GameMapTerrainObject terrain = Terrain(3, 1, 0, 0, [7, 8, 9]);

        GameMapTerrainElevation elevation = GameMapTerrainElevation.Compose(grid, [Placement(1, 0, terrain)]);

        Assert.Equal(7, Adjusted(elevation, 1, 0));
        Assert.Equal(8, Adjusted(elevation, 0, 0));
        Assert.Equal(0, Adjusted(elevation, 1, 1));
    }

    private static int Adjusted(GameMapTerrainElevation elevation, int x, int y)
    {
        Assert.True(elevation.TryGetAdjusted(x, y, out int adjusted));
        return adjusted;
    }

    private static GameMapTerrainObject Terrain(int width, int height, int anchorOffsetX, int anchorOffsetY,
        short[] deltas)
    {
        return new GameMapTerrainObject("ani/MapScene.ani", "Test", 0, 0, 100, width, height, 0, anchorOffsetX, anchorOffsetY, 0,
            [.. deltas.Select(static delta => new GameMapTerrainCell(0, 0, delta))]);
    }

    private static GameMapTerrainScenePlacement Placement(int x, int y, GameMapTerrainObject terrain)
    {
        GameMapSceneryRecord scenery = new(1, "map/Scene/test.scene", null, x, y, ReadOnlyMemory<byte>.Empty);
        return new GameMapTerrainScenePlacement(scenery,
            [new GameMapTerrainObjectBinding(terrain, new AniIndexSection("Test", ["frame.dds"]))]);
    }

    private static byte[] Grid(int width, int height, Func<int, int, short> elevation)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true))
        {
            writer.Write("DMAP101\0"u8);
            byte[] puzzlePath = new byte[0x104];
            Encoding.Latin1.GetBytes(@"map\puzzle\newbie.pul").CopyTo(puzzlePath, 0);
            writer.Write(puzzlePath);
            writer.Write(width);
            writer.Write(height);
            for (int y = 0; y < height; y++)
            {
                uint checksum = 0;
                for (int x = 0; x < width; x++)
                {
                    short value = elevation(x, y);
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write(value);
                    checksum = unchecked(checksum + ((uint)x + 1) * (uint)(value + 2));
                }

                writer.Write(checksum);
            }
        }

        return stream.ToArray();
    }
}
