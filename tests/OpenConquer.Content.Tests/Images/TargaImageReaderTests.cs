using System.Buffers.Binary;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Tests.Images;

public sealed class TargaImageReaderTests
{
    [Fact]
    public void DecodeRle32Bit_DecodesRetailRleBgraToTopDownRgba()
    {
        byte[] targa = CreateTwoByTwoTarga();

        RgbaImage image = TargaImageReader.DecodeRle32Bit(targa);

        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(
            [
                255, 0, 0, 64,
                0, 255, 0, 128,
                0, 0, 255, 255,
                0, 0, 255, 255,
            ],
            image.Pixels.ToArray()
        );
    }

    [Fact]
    public void DecodeRle32Bit_RejectsTruncatedHeader()
    {
        Assert.Throws<InvalidDataException>(() => TargaImageReader.DecodeRle32Bit(new byte[17]));
    }

    [Fact]
    public void DecodeRle32Bit_RejectsUnsupportedImageType()
    {
        byte[] targa = CreateTwoByTwoTarga();
        targa[2] = 2;

        Assert.Throws<InvalidDataException>(() => TargaImageReader.DecodeRle32Bit(targa));
    }

    [Fact]
    public void DecodeRle32Bit_RejectsUnsupportedPixelDepth()
    {
        byte[] targa = CreateTwoByTwoTarga();
        targa[16] = 24;

        Assert.Throws<InvalidDataException>(() => TargaImageReader.DecodeRle32Bit(targa));
    }

    [Fact]
    public void DecodeRle32Bit_RejectsUnsupportedDescriptor()
    {
        byte[] targa = CreateTwoByTwoTarga();
        targa[17] = 0x28;

        Assert.Throws<InvalidDataException>(() => TargaImageReader.DecodeRle32Bit(targa));
    }

    [Fact]
    public void DecodeRle32Bit_RejectsPacketBeyondDeclaredImageSize()
    {
        byte[] targa = CreateTwoByTwoTarga();
        targa[18] = 0x84;

        Assert.Throws<InvalidDataException>(() => TargaImageReader.DecodeRle32Bit(targa));
    }

    [Fact]
    public void DecodeRle32Bit_RejectsTruncatedRlePixel()
    {
        byte[] targa = CreateTwoByTwoTarga()[..21];

        Assert.Throws<InvalidDataException>(() => TargaImageReader.DecodeRle32Bit(targa));
    }

    private static byte[] CreateTwoByTwoTarga()
    {
        const int headerLength = 18;

        byte[] targa = new byte[headerLength + 1 + 4 + 1 + 8];

        targa[2] = 10;
        BinaryPrimitives.WriteUInt16LittleEndian(targa.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(targa.AsSpan(14), 2);
        targa[16] = 32;
        targa[17] = 0x08;

        int offset = headerLength;

        targa[offset++] = 0x81;
        targa.AsSpan(offset, 4).CopyFrom([255, 0, 0, 255]);
        offset += 4;

        targa[offset++] = 0x01;
        targa.AsSpan(offset, 8).CopyFrom(
            [
                0, 0, 255, 64,
                0, 255, 0, 128,
            ]
        );

        return targa;
    }
}
