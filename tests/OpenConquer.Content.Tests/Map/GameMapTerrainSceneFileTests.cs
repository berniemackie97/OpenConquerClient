using System.Buffers.Binary;
using System.Text;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapTerrainSceneFileTests
{
    [Fact]
    public void ParsesNativeTerrainObjectLayoutAndLowWordCellFields()
    {
        byte[] data = CreateFile();
        Assert.Equal(4 + 0x164 + 2 * 12, data.Length);

        GameMapTerrainObject obj = Assert.Single(GameMapTerrainSceneFile.Parse(data).Objects);
        Assert.Equal(("ani/MapScene.ani", "stand06.tga"), (obj.AnimationPath, obj.SectionName));
        Assert.Equal((-60, -48, 200), (obj.RenderOffsetX, obj.RenderOffsetY, obj.FrameIntervalMilliseconds));
        Assert.Equal((2, 1), (obj.GridWidth, obj.GridHeight));
        Assert.Equal(0xAABBCCDDu, obj.AuthoredColor);
        Assert.Equal((3, -4, 7), (obj.AnchorOffsetX, obj.AnchorOffsetY, obj.DrawParameter));
        Assert.Equal(
            [new GameMapTerrainCell(1, 0x8002, -1), new GameMapTerrainCell(3, 4, 5)],
            obj.Cells);
    }

    [Fact]
    public void RejectsTruncationTrailingDataUnsafePathsAndInvalidGrids()
    {
        byte[] valid = CreateFile();
        Assert.Throws<InvalidDataException>(() => GameMapTerrainSceneFile.Parse(valid[..^1]));
        Assert.Throws<InvalidDataException>(() => GameMapTerrainSceneFile.Parse([.. valid, 0]));

        byte[] negativeCount = (byte[])valid.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(negativeCount, -1);
        Assert.Throws<InvalidDataException>(() => GameMapTerrainSceneFile.Parse(negativeCount));

        byte[] badGrid = (byte[])valid.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(badGrid.AsSpan(4 + 0x140 + 12), int.MaxValue);
        Assert.Throws<InvalidDataException>(() => GameMapTerrainSceneFile.Parse(badGrid));

        byte[] unsafePath = (byte[])valid.Clone();
        unsafePath.AsSpan(4, 0x100).Clear();
        Encoding.Latin1.GetBytes("../outside.ani").CopyTo(unsafePath.AsSpan(4));
        Assert.Throws<InvalidDataException>(() => GameMapTerrainSceneFile.Parse(unsafePath));

        byte[] unterminated = (byte[])valid.Clone();
        unterminated.AsSpan(4, 0x100).Fill((byte)'a');
        Assert.Throws<InvalidDataException>(() => GameMapTerrainSceneFile.Parse(unterminated));
    }

    private static byte[] CreateFile()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true);
        writer.Write(1);
        WriteString(writer, @"ani\MapScene.ani", 0x100);
        WriteString(writer, "stand06.tga", 0x40);
        writer.Write(-60);
        writer.Write(-48);
        writer.Write(200);
        writer.Write(2);
        writer.Write(1);
        writer.Write(0xAABBCCDDu);
        writer.Write(3);
        writer.Write(-4);
        writer.Write(7);
        writer.Write(0x12340001);
        writer.Write(0xFEDC8002u);
        writer.Write(0xABCDFFFFu);
        writer.Write(3);
        writer.Write(4);
        writer.Write(5);
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteString(BinaryWriter writer, string value, int fieldLength)
    {
        byte[] data = Encoding.Latin1.GetBytes(value);
        writer.Write(data);
        writer.Write(new byte[fieldLength - data.Length]);
    }
}
