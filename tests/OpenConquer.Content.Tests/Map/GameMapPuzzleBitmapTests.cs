using System.Buffers.Binary;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapPuzzleBitmapTests
{
    [Fact]
    public void RetailNewbieGroundAndBackdropRetainSeparateTileGrids()
    {
        GameMapPuzzleBitmap ground = GameMapPuzzleBitmap.Parse(Retail("newbie.pul"));
        Assert.Equal("ani/newbie.ani", ground.TextureIndexPath);
        Assert.Equal((15, 9), (ground.Columns, ground.Rows));
        Assert.Equal(135, ground.Tiles.Length);
        Assert.Equal((ushort)0xFFFF, ground.Tiles[0]);
        Assert.Equal((ushort)1, ground.Tiles[13]);
        Assert.Equal((ushort)2, ground.Tiles[14]);
        Assert.Equal((ushort)49, ground.Tiles[134]);

        GameMapPuzzleBitmap backdrop = GameMapPuzzleBitmap.Parse(Retail("newbiebg.pul"));
        Assert.Equal("ani/newbie.ani", backdrop.TextureIndexPath);
        Assert.Equal((6, 6), (backdrop.Columns, backdrop.Rows));
        Assert.Equal(36, backdrop.Tiles.Length);
        Assert.Equal((ushort)50, backdrop.Tiles[0]);
        Assert.Equal((ushort)85, backdrop.Tiles[35]);
    }

    [Fact]
    public void TruncatedTrailingAndOversizedGridsAreRejected()
    {
        byte[] retail = Retail("newbie.pul");
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse(retail.AsSpan(0, retail.Length - 1)));
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse([.. retail, 0]));

        byte[] impossible = (byte[])retail.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(impossible.AsSpan(264), int.MaxValue);
        BinaryPrimitives.WriteInt32LittleEndian(impossible.AsSpan(268), int.MaxValue);
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse(impossible));
    }

    [Fact]
    public void InvalidSignatureAndUnsafePathAreRejected()
    {
        byte[] retail = Retail("newbie.pul");
        byte[] invalidSignature = (byte[])retail.Clone();
        invalidSignature[0] = 0;
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse(invalidSignature));

        byte[] invalidPath = (byte[])retail.Clone();
        "../newbie.ani"u8.CopyTo(invalidPath.AsSpan(8));
        invalidPath[8 + "../newbie.ani"u8.Length] = 0;
        Assert.Throws<InvalidDataException>(() => GameMapPuzzleBitmap.Parse(invalidPath));
    }

    [Fact]
    public void Puzzle2ReadsSignedScrollVelocitiesAfterTileGrid()
    {
        byte[] retail = Retail("newbiebg.pul");
        byte[] scrolling = new byte[retail.Length + 8];
        retail.CopyTo(scrolling, 0);
        "PUZZLE2\0"u8.CopyTo(scrolling);
        BinaryPrimitives.WriteInt32LittleEndian(scrolling.AsSpan(retail.Length), -12);
        BinaryPrimitives.WriteInt32LittleEndian(scrolling.AsSpan(retail.Length + 4), 34);

        GameMapPuzzleBitmap puzzle = GameMapPuzzleBitmap.Parse(scrolling);
        Assert.Equal((6, 6), (puzzle.Columns, puzzle.Rows));
        Assert.Equal((-12, 34), (puzzle.ScrollVelocityX, puzzle.ScrollVelocityY));
    }

    private static byte[] Retail(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "puzzle", name));
}
