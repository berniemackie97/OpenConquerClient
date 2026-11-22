using System.Text;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;
using OpenConquer.Rendering.World;

namespace OpenConquer.Rendering.Tests.World;

/// <summary>
/// Jump arc and helpers against the 5517 instructions (20_notes Network/15_msgwalk_10005_direction_index_order.md,
/// "Remote jump presentation"). Golden values are computed by hand from the native formulas.
/// </summary>
public sealed class WorldJumpArcTests
{
    private const int MapWidth = 100;

    private static readonly AniIndexSection s_animation = AniIndexFile.Load(
        new MemoryStream("[Test]\r\nFrameAmount=1\r\nFrame0=frame.dds\r\n"u8.ToArray()), "ani/test.ani")
        .GetRequiredSection("Test");

    [Theory]
    [InlineData(15.584, 16)]
    [InlineData(15.36, 15)]
    [InlineData(0.5, 1)]
    [InlineData(2.4, 2)]
    [InlineData(-0.7, 0)]
    [InlineData(-6.4, -5)]
    [InlineData(-1.5, -1)]
    public void RoundHalfStepMatchesTheNativeCompare(double value, int expected)
    {
        Assert.Equal(expected, WorldJumpArc.RoundHalfStep(value));
    }

    [Theory]
    [InlineData(1664, 2704, 60, 108)]
    [InlineData(1696, 2720, 61, 108)]
    [InlineData(1680, 2712, 61, 108)]
    [InlineData(-40, -40, -51, 49)]
    public void ScreenToTileInvertsTheProjectionWithoutClamping(int x, int y, int tileX, int tileY)
    {
        Assert.Equal((tileX, tileY), WorldJumpArc.ScreenToTile(MapWidth, x, y));
    }

    [Fact]
    public void RoundedLineExcludesTheSourceAndBreaksTiesOnTheYAxis()
    {
        Assert.Equal([(61, 109), (62, 110)], WorldJumpArc.RoundedLineExcludingStart(60, 108, 62, 110));
        Assert.Equal([(4, 5), (3, 4), (2, 4)], WorldJumpArc.RoundedLineExcludingStart(5, 5, 2, 4));
        Assert.Equal([(0, 1), (1, 2), (1, 3)], WorldJumpArc.RoundedLineExcludingStart(0, 0, 1, 3));
        Assert.Empty(WorldJumpArc.RoundedLineExcludingStart(7, 7, 7, 7));
    }

    [Theory]
    [InlineData(1664, 2704, 0)]
    [InlineData(1664, 2720, 28)]
    [InlineData(1664, 2736, 41)]
    [InlineData(1664, 2752, 28)]
    [InlineData(1664, 2768, 0)]
    public void FlatArcRisesOnAHalfSineToDistanceOverThreePlusTwenty(int x, int y, int expected)
    {
        // (60,108) -> (62,110): screen distance 64, apex trunc(64/3)+20 = 41 at the midpoint.
        GameMapTerrainElevation flat = Elevation([]);

        Assert.Equal(expected, WorldJumpArc.Height(flat, (1664, 2704), (1664, 2768), (x, y)));
    }

    [Fact]
    public void PeakTileRaisesTheApexButTheApexStaysAtTheMidpoint()
    {
        // Line (61,109),(62,110); peak (61,109) adjusted 30: apex trunc(21.33 + 30) + 20 = 71.
        GameMapTerrainElevation raised = Elevation([(61, 109, 30)]);

        Assert.Equal(0, WorldJumpArc.Height(raised, (1664, 2704), (1664, 2768), (1664, 2704)));
        Assert.Equal(50, WorldJumpArc.Height(raised, (1664, 2704), (1664, 2768), (1664, 2720)));
        Assert.Equal(71, WorldJumpArc.Height(raised, (1664, 2704), (1664, 2768), (1664, 2736)));
        Assert.Equal(50, WorldJumpArc.Height(raised, (1664, 2704), (1664, 2768), (1664, 2752)));
    }

    [Fact]
    public void EndpointElevationAnchorsEachHalf()
    {
        // End tile (62,110) adjusted 12 is the peak; apex trunc(21.33 + 12) + 20 = 53.
        GameMapTerrainElevation raised = Elevation([(62, 110, 12)]);

        Assert.Equal(12, WorldJumpArc.Height(raised, (1664, 2704), (1664, 2768), (1664, 2768)));
        Assert.Equal(40, WorldJumpArc.Height(raised, (1664, 2704), (1664, 2768), (1664, 2752)));
        Assert.Equal(37, WorldJumpArc.Height(raised, (1664, 2704), (1664, 2768), (1664, 2720)));
    }

    [Fact]
    public void MissingEndpointTileReturnsZero()
    {
        GameMapTerrainElevation flat = Elevation([]);

        Assert.Equal(0, WorldJumpArc.Height(flat, (1664, 2704), (-9000, -9000), (1664, 2720)));
    }

    private static GameMapTerrainElevation Elevation((int X, int Y, short Delta)[] raised)
    {
        GameMapCellGrid grid = GameMapCellGrid.Parse(Grid(MapWidth, 200));
        List<GameMapTerrainScenePlacement> placements = [];
        foreach ((int x, int y, short delta) in raised)
        {
            GameMapTerrainObject terrain = new("ani/MapScene.ani", "Test", 0, 0, 100, 1, 1, 0, 0, 0, 0,
                [new GameMapTerrainCell(0, 0, delta)]);
            GameMapSceneryRecord scenery = new(1, "map/Scene/test.scene", null, x, y, ReadOnlyMemory<byte>.Empty);
            placements.Add(new GameMapTerrainScenePlacement(scenery,
                [new GameMapTerrainObjectBinding(terrain, s_animation)]));
        }

        return GameMapTerrainElevation.Compose(grid, placements);
    }

    private static byte[] Grid(int width, int height)
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
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write((short)0);
                    checksum = unchecked(checksum + ((uint)x + 1) * 2);
                }

                writer.Write(checksum);
            }
        }

        return stream.ToArray();
    }
}
