using OpenConquer.Content.Map;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapArchiveTests
{
    [Fact]
    public void RetailNewbieArchive_DecodesOriginalDMapAndSceneSections()
    {
        byte[] decoded = GameMapArchive.Decode(Retail(), "map/map/newbie.7z");

        Assert.Equal(118644, decoded.Length);
        GameMapCellGrid grid = GameMapCellGrid.Parse(decoded);
        Assert.Equal((132, 132), (grid.Width, grid.Height));
        Assert.Equal(105348, grid.BytesConsumed);
        Assert.Equal("map/puzzle/newbie.pul", grid.PuzzlePath);

        GameMapSceneSections scene = GameMapSceneSections.Parse(decoded, grid.BytesConsumed);
        Assert.Empty(scene.ExitMarkers);
        Assert.Equal(38, scene.Scenery.Count);
        Assert.Equal("map/puzzle/newbiebg.pul",
            Assert.IsType<GameMapPuzzleOverlayEntry>(
                Assert.Single(Assert.Single(scene.PositionedOverlays).Entries)).PuzzlePath);
    }

    [Fact]
    public void Decode_RejectsInvalidSignatureAndWrongEntryIdentity()
    {
        byte[] archive = Retail();
        byte[] invalidSignature = (byte[])archive.Clone();
        invalidSignature[0] = 0;

        Assert.Throws<InvalidDataException>(() => GameMapArchive.Decode(invalidSignature, "map/map/newbie.7z"));
        Assert.Throws<InvalidDataException>(() => GameMapArchive.Decode(archive.AsSpan(0, 31), "map/map/newbie.7z"));
        Assert.Throws<InvalidDataException>(() => GameMapArchive.Decode(archive, "map/map/newplain.7z"));
        Assert.Throws<ArgumentException>(() => GameMapArchive.Decode(archive, "../newbie.7z"));
        Assert.Throws<ArgumentException>(() => GameMapArchive.Decode(archive, "map/map/newbie.DMap"));
    }

    [Fact]
    public void Decode_RejectsOversizeCompressedInputBeforeCodec()
    {
        byte[] noise = new byte[256 * 1024];
        new Random(12345).NextBytes(noise);

        using MemoryStream output = new();
        using (Stream data = new MemoryStream(noise, writable: false))
        using (var writer = WriterFactory.OpenWriter(output, ArchiveType.SevenZip,
                   new SevenZipWriterOptions(CompressionType.LZMA2) { CompressHeader = true }))
        {
            writer.Write("newbie.DMap", data, DateTime.UnixEpoch);
        }

        byte[] oversized = output.ToArray();
        Assert.True(oversized.Length > 128 * 1024);
        Assert.Throws<InvalidDataException>(() => GameMapArchive.Decode(oversized, "map/map/newbie.7z"));
    }

    private static byte[] Retail() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "newbie.7z"));
}
