using System.Buffers.Binary;
using System.Text;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapCellGridTests
{
    [Fact]
    public void VerifiedNewbieCellGrid_PreservesNativeDimensionsAndChecksums()
    {
        byte[] payload = Build(132, 132, static (_, _) => new GameMapCell(0, 1, 0));

        GameMapCellGrid grid = GameMapCellGrid.Parse(payload);

        Assert.Equal((132, 132), (grid.Width, grid.Height));
        Assert.Equal("map/puzzle/newbie.pul", grid.PuzzlePath);
        Assert.Equal(105348, grid.BytesConsumed);
        Assert.Equal(new GameMapCell(0, 1, 0), grid.GetCell(0, 0));
        Assert.Equal(new GameMapCell(0, 1, 0), grid.GetCell(131, 131));
        Assert.Equal(0x4518u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(276 + 132 * 6)));
        Assert.Equal(0x88A4u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(grid.BytesConsumed - 4)));
    }

    [Fact]
    public void Parse_StoresSurfacePassabilityAndSignedElevationInNativeOrder()
    {
        byte[] payload = Build(2, 2, static (x, y) =>
            new GameMapCell((ushort)(200 + x + y), (ushort)(x + 1), (short)(-25 + y)));
        byte[] withNextSection = [.. payload, 0, 0, 0, 0];

        GameMapCellGrid grid = GameMapCellGrid.Parse(withNextSection);

        Assert.Equal(payload.Length, grid.BytesConsumed);
        Assert.Equal(new GameMapCell(200, 1, -25), grid.GetCell(0, 0));
        Assert.Equal(new GameMapCell(201, 2, -25), grid.GetCell(1, 0));
        Assert.Equal(new GameMapCell(201, 1, -24), grid.GetCell(0, 1));
        Assert.Equal(new GameMapCell(202, 2, -24), grid.GetCell(1, 1));
        Assert.Equal(0xFFFFDE11u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(276 + 2 * 6)));
        Assert.Equal(0xFFFFDF7Fu, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(payload.Length - 4)));
        Assert.Equal("x", Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetCell(2, 0)).ParamName);
        Assert.Equal("y", Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetCell(0, 2)).ParamName);
    }

    [Fact]
    public void Parse_AcceptsBothVerifiedRetailHeaderVariants()
    {
        byte[] payload = Build(1, 1, static (_, _) => new GameMapCell(0, 1, 0));
        GameMapCellGrid latest = GameMapCellGrid.Parse(payload);
        Assert.Equal(0x50414D44u, latest.HeaderField0);
        Assert.Equal(0x00313031u, latest.HeaderField1);

        BinaryPrimitives.WriteUInt32LittleEndian(payload, 1004u);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), 0u);
        GameMapCellGrid legacy = GameMapCellGrid.Parse(payload);
        Assert.Equal(1004u, legacy.HeaderField0);
        Assert.Equal(0u, legacy.HeaderField1);
    }

    [Fact]
    public void Parse_RejectsCorruptedCellsAndRowChecksums()
    {
        byte[] payload = Build(2, 2, static (_, _) => new GameMapCell(3, 1, -1));

        byte[] changedCell = (byte[])payload.Clone();
        changedCell[278] ^= 1;
        Assert.Throws<InvalidDataException>(() => GameMapCellGrid.Parse(changedCell));

        byte[] changedChecksum = (byte[])payload.Clone();
        changedChecksum[276 + 2 * 6] ^= 1;
        Assert.Throws<InvalidDataException>(() => GameMapCellGrid.Parse(changedChecksum));
        Assert.Throws<InvalidDataException>(() => GameMapCellGrid.Parse(payload.AsSpan(0, payload.Length - 1)));
    }

    [Fact]
    public void Parse_RejectsOversizedDimensionsAndUnsafePuzzlePaths()
    {
        byte[] payload = Build(1, 1, static (_, _) => new GameMapCell(0, 0, 0));

        byte[] hugeGrid = (byte[])payload.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(hugeGrid.AsSpan(268), int.MaxValue);
        BinaryPrimitives.WriteInt32LittleEndian(hugeGrid.AsSpan(272), int.MaxValue);
        Assert.Throws<InvalidDataException>(() => GameMapCellGrid.Parse(hugeGrid));

        byte[] excessiveCells = (byte[])payload.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(excessiveCells.AsSpan(268), 2_097_153);
        BinaryPrimitives.WriteInt32LittleEndian(excessiveCells.AsSpan(272), 2);
        InvalidDataException limit = Assert.Throws<InvalidDataException>(() => GameMapCellGrid.Parse(excessiveCells));
        Assert.Contains("cell limit", limit.Message, StringComparison.Ordinal);

        byte[] unsafePath = (byte[])payload.Clone();
        unsafePath.AsSpan(8, 0x104).Clear();
        "../outside.pul"u8.CopyTo(unsafePath.AsSpan(8));
        Assert.Throws<InvalidDataException>(() => GameMapCellGrid.Parse(unsafePath));

        byte[] missingTerminator = (byte[])payload.Clone();
        missingTerminator.AsSpan(8, 0x104).Fill((byte)'x');
        Assert.Throws<InvalidDataException>(() => GameMapCellGrid.Parse(missingTerminator));
    }

    private static byte[] Build(int width, int height, Func<int, int, GameMapCell> createCell)
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
                    GameMapCell cell = createCell(x, y);
                    writer.Write(cell.PassabilityFlag);
                    writer.Write(cell.SurfaceId);
                    writer.Write(cell.Elevation);
                    checksum = unchecked(checksum
                        + ((uint)cell.SurfaceId + (uint)y + 1) * cell.PassabilityFlag
                        + ((uint)cell.SurfaceId + (uint)x + 1) * (uint)(cell.Elevation + 2));
                }

                writer.Write(checksum);
            }
        }

        return stream.ToArray();
    }
}
