using System.Buffers.Binary;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapPuzzleBitmapTests
{
    [Fact]
    public void RetailNewbieGroundAndBackdrop_PreserveAuthoredTileIndicesAndAniPath()
    {
        GameMapPuzzleBitmap ground = GameMapPuzzleBitmap.Parse(ReadFixture("newbie.pul"));
        GameMapPuzzleBitmap backdrop = GameMapPuzzleBitmap.Parse(ReadFixture("newbiebg.pul"));

        Assert.Equal((15, 9), (ground.Columns, ground.Rows));
        Assert.Equal((6, 6), (backdrop.Columns, backdrop.Rows));
        Assert.Equal("ani/newbie.ani", ground.TextureIndexPath);
        Assert.Equal("ani/newbie.ani", backdrop.TextureIndexPath);
        Assert.Equal(ushort.MaxValue, ground.Tiles[0]);
        Assert.Equal((ushort)50, backdrop.Tiles[0]);
        Assert.Equal((ushort)85, backdrop.Tiles[^1]);
    }

    [Fact]
    public void RetailPuzzle2_PreservesGridAndScrollFields()
    {
        GameMapPuzzleBitmap bitmap = GameMapPuzzleBitmap.Parse(ReadFixture("jokul01.pul"));

        Assert.Equal((4, 5), (bitmap.Columns, bitmap.Rows));
        Assert.Equal("ani/color3map.ani", bitmap.TextureIndexPath);
        Assert.Equal((ushort)90, bitmap.Tiles[4]);
        Assert.Equal((0, 0), (bitmap.ScrollVelocityX, bitmap.ScrollVelocityY));
    }

    [Fact]
    public void MalformedRetailPuzzle_FailsAtContentBoundary()
    {
        byte[] retail = ReadFixture("newbiebg.pul");
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse(retail.AsSpan(0, 271)));
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse(retail.AsSpan(0, retail.Length - 1)));
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse([.. retail, 0]));

        byte[] invalidSignature = (byte[])retail.Clone();
        invalidSignature[0] = 0;
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse(invalidSignature));

        byte[] excessiveDimensions = (byte[])retail.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(excessiveDimensions.AsSpan(264), int.MaxValue);
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse(excessiveDimensions));

        byte[] unsafePath = (byte[])retail.Clone();
        unsafePath[8] = (byte)'.';
        unsafePath[9] = (byte)'.';
        unsafePath[10] = (byte)'\\';
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse(unsafePath));

        byte[] puzzle2 = ReadFixture("jokul01.pul");
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse(puzzle2.AsSpan(0, puzzle2.Length - 1)));
    }

    private static byte[] ReadFixture(string fileName) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "puzzle", fileName));
}
