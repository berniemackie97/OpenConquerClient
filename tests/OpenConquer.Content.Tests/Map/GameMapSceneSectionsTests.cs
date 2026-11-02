using System.Buffers.Binary;
using System.IO.Compression;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapSceneSectionsTests
{
    private const int GridEnd = 105348;
    private const int NewbieSceneLength = 13296;

    [Fact]
    public void NewbieScene_ParsesExactSourceSectionsAndRenderOrder()
    {
        byte[] decoded = LoadScene();
        GameMapSceneSections scene = GameMapSceneSections.Parse(decoded, 0, 1004);

        Assert.Empty(scene.ExitMarkers);
        Assert.Equal(38, scene.Scenery.Count);
        Assert.Equal(18, scene.Scenery.Count(x => x.TypeCode == 4));
        Assert.Equal(20, scene.Scenery.Count(x => x.TypeCode == 1));
        Assert.Equal("ani/MapScene.ani", scene.Scenery[0].ResourcePath);
        Assert.Equal("newbie17.tga", scene.Scenery[0].PartName);
        Assert.Equal((91, 35), (scene.Scenery[0].TileX, scene.Scenery[0].TileY));
        Assert.Equal("map/Scene/stand06.scene", scene.Scenery[18].ResourcePath);

        GameMapPositionedOverlayRecord overlay = Assert.Single(scene.PositionedOverlays);
        Assert.Equal(0, overlay.InsertIndex);
        Assert.Equal((18, 35), (overlay.HeaderInt0, overlay.HeaderInt1));
        GameMapPuzzleOverlayEntry puzzle = Assert.IsType<GameMapPuzzleOverlayEntry>(Assert.Single(overlay.Entries));
        Assert.Equal("map/puzzle/newbiebg.pul", puzzle.PuzzlePath);

        byte[] completeMap = new byte[GridEnd + decoded.Length];
        decoded.CopyTo(completeMap, GridEnd);
        GameMapSceneSections withGrid = GameMapSceneSections.Parse(completeMap, GridEnd, 1004);
        Assert.Equal(scene.Scenery.Count, withGrid.Scenery.Count);
    }

    [Fact]
    public void MalformedCountsAndUnknownSceneryTypes_FailClosed()
    {
        byte[] original = LoadScene();
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse(original.AsSpan(0, 3), 0, 1004));

        byte[] excessiveCount = (byte[])original.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(excessiveCount.AsSpan(4), int.MaxValue);
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse(excessiveCount, 0, 1004));

        byte[] unknownType = (byte[])original.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(unknownType.AsSpan(8), 0x1234);
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse(unknownType, 0, 1004));
    }

    [Fact]
    public void TruncatedTrailingAndUnsafeOverlayResource_FailClosed()
    {
        byte[] original = LoadScene();
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse(original.AsSpan(0, original.Length - 1), 0, 1004));
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse([.. original, 0], 0, 1004));

        byte[] unsafePath = (byte[])original.Clone();
        int overlayPathOffset = 13036;
        unsafePath.AsSpan(overlayPathOffset, 0x104).Clear();
        "../outside.pul"u8.CopyTo(unsafePath.AsSpan(overlayPathOffset));
        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse(unsafePath, 0, 1004));
    }

    [Theory]
    [InlineData(1004u, false)]
    [InlineData(1005u, true)]
    public void SceneryType4_ConsumesTheEncodedRecordLength(uint headerField0, bool extended)
    {
        byte[] encoded = CreateSceneryRecords(extended);

        GameMapSceneSections scene = GameMapSceneSections.Parse(encoded, 0, headerField0);

        Assert.Empty(scene.ExitMarkers);
        Assert.Empty(scene.PositionedOverlays);
        Assert.Equal(2, scene.Scenery.Count);
        Assert.All(scene.Scenery, record =>
        {
            Assert.Equal(4, record.TypeCode);
            Assert.Equal("ani/mapscene02.ani", record.ResourcePath);
            Assert.Equal("icecrypt36.tga", record.PartName);
            Assert.Equal(extended ? 0x1A4 : 0x1A0, record.EncodedPayload.Length);
            if (extended)
            {
                Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(record.EncodedPayload.Span[^4..]));
            }
        });
        Assert.Equal((58, 372), (scene.Scenery[0].TileX, scene.Scenery[0].TileY));
        Assert.Equal((112, 422), (scene.Scenery[1].TileX, scene.Scenery[1].TileY));
    }

    [Theory]
    [InlineData(1004u, true)]
    [InlineData(1005u, false)]
    public void SceneryType4_RejectsMismatchedRecordLengths(uint headerField0, bool extended)
    {
        byte[] encoded = CreateSceneryRecords(extended);

        Assert.Throws<InvalidDataException>(() => GameMapSceneSections.Parse(encoded, 0, headerField0));
    }

    [Fact]
    public void ExtendedScenery_RejectsTruncationAndTrailingBytes()
    {
        byte[] encoded = CreateSceneryRecords(extended: true);

        Assert.Throws<InvalidDataException>(() =>
            GameMapSceneSections.Parse(encoded.AsSpan(0, encoded.Length - 1), 0, 1005));
        Assert.Throws<InvalidDataException>(() =>
            GameMapSceneSections.Parse([.. encoded, 0], 0, 1005));
    }

    private static byte[] CreateSceneryRecords(bool extended)
    {
        int payloadLength = extended ? 0x1A4 : 0x1A0;
        byte[] encoded = new byte[12 + 2 * (sizeof(int) + payloadLength)];
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(4), 2);

        for (int index = 0; index < 2; index++)
        {
            int offset = 8 + index * (sizeof(int) + payloadLength);
            BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(offset), 4);
            Span<byte> payload = encoded.AsSpan(offset + sizeof(int), payloadLength);
            "ani/mapscene02.ani"u8.CopyTo(payload);
            "icecrypt36.tga"u8.CopyTo(payload[0x104..]);

            int[] fields = [index == 0 ? 58 : 112, index == 0 ? 372 : 422, 3, 6, 261, 492, 100];
            for (int field = 0; field < fields.Length; field++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(payload[(0x184 + field * sizeof(int))..], fields[field]);
            }

            if (extended)
            {
                BinaryPrimitives.WriteInt32LittleEndian(payload[0x1A0..], 4);
            }
        }

        return encoded;
    }

    private static byte[] LoadScene()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "newbie.scene.zlib");
        using FileStream source = File.OpenRead(path);
        using ZLibStream decoded = new(source, CompressionMode.Decompress);
        byte[] bytes = new byte[NewbieSceneLength];
        decoded.ReadExactly(bytes);
        Assert.Equal(-1, decoded.ReadByte());
        return bytes;
    }
}
