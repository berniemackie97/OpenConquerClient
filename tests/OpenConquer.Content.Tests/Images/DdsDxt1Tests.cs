using System.Buffers.Binary;
using OpenConquer.Content.Images;
using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests.Images;

public sealed class DdsDxt1Tests
{
    private const int PixelDataOffset = 128;

    [Fact]
    public void Decode_FourColorModeInterpolatesOpaquePalette()
    {
        byte[] payload = CreateDxt1(4, 4);
        WriteBlock(payload.AsSpan(PixelDataOffset), 0xF800, 0x001F, 0xE4E4E4E4);

        RgbaImage image = DdsImageReader.Decode(payload);

        Assert.Equal((4, 4), (image.Width, image.Height));
        byte[] row =
        [
            255, 0, 0, 255,
            0, 0, 255, 255,
            170, 0, 85, 255,
            85, 0, 170, 255,
        ];

        for (int y = 0; y < 4; y++)
        {
            Assert.Equal(row, image.Pixels.Span.Slice(y * 16, 16).ToArray());
        }
    }

    [Fact]
    public void Decode_ThreeColorModeUsesTransparentFourthEntry()
    {
        byte[] payload = CreateDxt1(4, 4);
        WriteBlock(payload.AsSpan(PixelDataOffset), 0x001F, 0xF800, 0xE4E4E4E4);

        RgbaImage image = DdsImageReader.Decode(payload);

        byte[] row =
        [
            0, 0, 255, 255,
            255, 0, 0, 255,
            127, 0, 127, 255,
            0, 0, 0, 0,
        ];

        for (int y = 0; y < 4; y++)
        {
            Assert.Equal(row, image.Pixels.Span.Slice(y * 16, 16).ToArray());
        }
    }

    [Fact]
    public void Decode_ClipsEdgeBlocksToDeclaredDimensions()
    {
        byte[] payload = CreateDxt1(5, 1);
        WriteBlock(payload.AsSpan(PixelDataOffset), 0xF800, 0, 0);
        WriteBlock(payload.AsSpan(PixelDataOffset + 8), 0x001F, 0, 0);

        RgbaImage image = DdsImageReader.Decode(payload);

        Assert.Equal((5, 1), (image.Width, image.Height));
        Assert.Equal(
        [
            255, 0, 0, 255,
            255, 0, 0, 255,
            255, 0, 0, 255,
            255, 0, 0, 255,
            0, 0, 255, 255,
        ], image.Pixels.ToArray());
    }

    [Fact]
    public void Decode_OriginalNewbie005BlockMatchesIndependentRgbaReference()
    {
        // First 8 bytes at DDS offset 128 in retail data.wdf: data/map/puzzle/newbie/newbie005.dds.
        byte[] payload = CreateDxt1(4, 4);
        Convert.FromHexString("6822E61109818B3D").CopyTo(payload, PixelDataOffset);

        RgbaImage image = DdsImageReader.Decode(payload);
        ReadOnlySpan<byte> pixels = image.Pixels.Span;

        Assert.Equal([16, 60, 49, 255], pixels[..4].ToArray());
        Assert.Equal([27, 71, 60, 255], pixels.Slice(4, 4).ToArray());
        Assert.Equal([33, 77, 66, 255], pixels.Slice(8, 4).ToArray());
        Assert.Equal([21, 65, 54, 255], pixels.Slice(8 * 4, 4).ToArray());
    }

    [Fact]
    public void Load_PackageOnly_DecodesDxt1ThroughExistingWdfContentSource()
    {
        const string contentPath = "data/map/puzzle/newbie/newbie005.dds";
        byte[] payload = CreateDxt1(4, 4);
        WriteBlock(payload.AsSpan(PixelDataOffset), 0xF800, 0x001F, 0);

        using TemporaryContentDirectory root = new();
        root.WriteFile("ini/package.ini", "data.wdf\n");
        root.WriteFile("data.wdf", WdfTestArchiveBuilder.CreateSingleEntry(contentPath, payload));

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(root.RootPath);
        RgbaImage image = DdsImageLoader.Load(contentSource, contentPath, ContentLookupMode.PackageOnly);

        Assert.Equal((4, 4), (image.Width, image.Height));
        Assert.Equal([255, 0, 0, 255], image.Pixels.Span[..4].ToArray());
    }

    [Fact]
    public void Decode_RejectsInvalidDxt1PayloadWithoutFallback()
    {
        byte[] payload = CreateDxt1(4, 4);
        Assert.Throws<InvalidDataException>(() => DdsImageReader.Decode(payload.AsSpan(0, 127)));
        Assert.Throws<InvalidDataException>(() => DdsImageReader.Decode(payload.AsSpan(0, payload.Length - 1)));
        Assert.Throws<InvalidDataException>(() => DdsImageReader.Decode([.. payload, 0]));

        byte[] invalidLinearSize = (byte[])payload.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(invalidLinearSize.AsSpan(20), 16);
        Assert.Throws<InvalidDataException>(() => DdsImageReader.Decode(invalidLinearSize));

        byte[] unsupportedFourCc = (byte[])payload.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(unsupportedFourCc.AsSpan(84), 0x35545844);
        Assert.Throws<InvalidDataException>(() => DdsImageReader.Decode(unsupportedFourCc));

        Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(payload));
    }

    private static byte[] CreateDxt1(int width, int height)
    {
        int blockCountX = (width + 3) / 4;
        int blockCountY = (height + 3) / 4;
        int encodedLength = checked(blockCountX * blockCountY * 8);
        byte[] dds = new byte[PixelDataOffset + encodedLength];

        BinaryPrimitives.WriteUInt32LittleEndian(dds, 0x20534444);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(8), 0x00081007);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(12), (uint)height);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(20), (uint)encodedLength);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(76), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(80), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(84), 0x31545844);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(108), 0x00001000);

        return dds;
    }

    private static void WriteBlock(Span<byte> block, ushort color0, ushort color1, uint selectors)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(block, color0);
        BinaryPrimitives.WriteUInt16LittleEndian(block[2..], color1);
        BinaryPrimitives.WriteUInt32LittleEndian(block[4..], selectors);
    }
}
