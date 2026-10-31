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

        Assert.Equal((15, 9), (ground.Width, ground.Height));
        Assert.Equal((6, 6), (backdrop.Width, backdrop.Height));
        Assert.Equal("ani/newbie.ani", ground.TextureIndexPath);
        Assert.Equal("ani/newbie.ani", backdrop.TextureIndexPath);
        Assert.False(ground.IsPuzzle2);
        Assert.False(backdrop.IsPuzzle2);
        Assert.Equal(ushort.MaxValue, ground.GetTileIndex(0, 0));
        Assert.Equal((ushort)50, backdrop.GetTileIndex(0, 0));
        Assert.Equal((ushort)85, backdrop.GetTileIndex(5, 5));
    }

    [Fact]
    public void RetailPuzzle2_PreservesGridAndScrollFields()
    {
        GameMapPuzzleBitmap bitmap = GameMapPuzzleBitmap.Parse(ReadFixture("jokul01.pul"));

        Assert.True(bitmap.IsPuzzle2);
        Assert.Equal((4, 5), (bitmap.Width, bitmap.Height));
        Assert.Equal("ani/color3map.ani", bitmap.TextureIndexPath);
        Assert.Equal((ushort)90, bitmap.GetTileIndex(0, 1));
        Assert.Equal((0, 0), (bitmap.ScrollVelocityX, bitmap.ScrollVelocityY));
        Assert.Throws<ArgumentOutOfRangeException>(() => bitmap.GetTileIndex(4, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => bitmap.GetTileIndex(0, -1));
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
