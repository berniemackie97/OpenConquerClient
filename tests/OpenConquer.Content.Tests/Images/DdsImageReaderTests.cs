using System.Buffers.Binary;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Tests.Images;

public sealed class DdsImageReaderTests
{
    private const int PixelDataOffset = 128;

    [Fact]
    public void DecodeDxt3_DecodesExplicitAlphaAndFourColorPalette()
    {
        byte[] dds = CreateDxt3Dds(4, 4);

        WriteDxt3Block(dds.AsSpan(PixelDataOffset, 16), 0xCD83DC72EB61FA50, 0x001F, 0xF800, 0xFFAA1BE4);

        RgbaImage image = DdsImageReader.DecodeDxt3(dds);

        byte[] expected =
        [
            0, 0, 255, 0, 255, 0, 0, 85, 85, 0, 170, 170, 170, 0, 85, 255,
            170, 0, 85, 17, 85, 0, 170, 102, 255, 0, 0, 187, 0, 0, 255, 238,
            85, 0, 170, 34, 85, 0, 170, 119, 85, 0, 170, 204, 85, 0, 170, 221,
            170, 0, 85, 51, 170, 0, 85, 136, 170, 0, 85, 221, 170, 0, 85, 204,
        ];

        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(expected, image.Pixels.ToArray());
    }

    [Fact]
    public void DecodeDxt3_ClipsPartialEdgeBlocks()
    {
        byte[] dds = CreateDxt3Dds(5, 1);

        WriteDxt3Block(dds.AsSpan(PixelDataOffset, 16), ulong.MaxValue, 0x07E0, 0, 0);
        WriteDxt3Block(dds.AsSpan(PixelDataOffset + 16, 16), ulong.MaxValue, 0xF800, 0, 0);

        RgbaImage image = DdsImageReader.DecodeDxt3(dds);

        Assert.Equal(5, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(
            [
                0, 255, 0, 255,
                0, 255, 0, 255,
                0, 255, 0, 255,
                0, 255, 0, 255,
                255, 0, 0, 255,
            ],
            image.Pixels.ToArray()
        );
    }

    [Fact]
    public void DecodeDxt3_AllowsVerifiedRetailUnusedFields()
    {
        byte[] dds = CreateDxt3Dds(16, 16);

        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(88), 0x00000100);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(116), 0x58534444);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(120), uint.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(124), uint.MaxValue);

        RgbaImage image = DdsImageReader.DecodeDxt3(dds);

        Assert.Equal(16, image.Width);
        Assert.Equal(16, image.Height);
        Assert.Equal(16 * 16 * 4, image.Pixels.Length);
    }

    [Fact]
    public void DecodeDxt3_RejectsTruncatedHeader()
    {
        Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(new byte[PixelDataOffset - 1]));
    }

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(4, 123u)]
    [InlineData(8, 0x00080007u)]
    [InlineData(76, 31u)]
    [InlineData(80, 0u)]
    [InlineData(84, 0x35545844u)]
    [InlineData(108, 0u)]
    public void DecodeDxt3_RejectsInvalidRequiredHeaderFields(int offset, uint value)
    {
        byte[] dds = CreateDxt3Dds(4, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(offset), value);

        Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(dds));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(16_385, 4)]
    [InlineData(4, 16_385)]
    public void DecodeDxt3_RejectsInvalidDimensions(int width, int height)
    {
        byte[] dds = CreateDxt3Dds(width, height);

        Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(dds));
    }

    [Fact]
    public void DecodeDxt3_RejectsDecodedImageBeyondMemoryLimit()
    {
        byte[] dds = CreateDxt3Dds(4, 4);

        const int dimension = 8_193;
        int blockCount = (dimension + 3) / 4;
        int encodedLength = checked(blockCount * blockCount * 16);

        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(12), dimension);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(16), dimension);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(20), (uint)encodedLength);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(dds));

        Assert.Contains("memory limit", exception.Message);
    }

    [Fact]
    public void DecodeDxt3_RejectsLinearSizeMismatch()
    {
        byte[] dds = CreateDxt3Dds(4, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(20), 15);

        Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(dds));
    }

    [Fact]
    public void DecodeDxt3_RejectsMipmappedTexture()
    {
        byte[] dds = CreateDxt3Dds(4, 4);

        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(8), 0x000A1007);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(28), 2);

        Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(dds));
    }

    [Fact]
    public void DecodeDxt3_RejectsVolumeTexture()
    {
        byte[] dds = CreateDxt3Dds(4, 4);

        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(8), 0x00881007);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(24), 1);

        Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(dds));
    }

    [Theory]
    [InlineData(0x00001008u)]
    [InlineData(0x00401000u)]
    public void DecodeDxt3_RejectsComplexTextureCaps(uint caps)
    {
        byte[] dds = CreateDxt3Dds(4, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(108), caps);

        Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(dds));
    }

    [Fact]
    public void DecodeDxt3_RejectsSecondaryTextureCaps()
    {
        byte[] dds = CreateDxt3Dds(4, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(112), 1);

        Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(dds));
    }

    [Fact]
    public void DecodeDxt3_RejectsTruncatedLevelZero()
    {
        byte[] dds = CreateDxt3Dds(4, 4);

        Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(dds.AsSpan()[..^1]));
    }

    [Fact]
    public void DecodeDxt3_RejectsTrailingData()
    {
        byte[] dds = CreateDxt3Dds(4, 4);
        Array.Resize(ref dds, dds.Length + 1);

        Assert.Throws<InvalidDataException>(() => DdsImageReader.DecodeDxt3(dds));
    }

    private static byte[] CreateDxt3Dds(int width, int height)
    {
        int blockCountX = (width + 3) / 4;
        int blockCountY = (height + 3) / 4;
        int encodedLength = checked(blockCountX * blockCountY * 16);
        byte[] dds = new byte[PixelDataOffset + encodedLength];

        BinaryPrimitives.WriteUInt32LittleEndian(dds, 0x20534444);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(8), 0x00081007);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(12), (uint)height);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(20), (uint)encodedLength);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(76), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(80), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(84), 0x33545844);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(108), 0x00001000);

        return dds;
    }

    private static void WriteDxt3Block(Span<byte> block, ulong alphaBits, ushort color0, ushort color1, uint colorSelectors)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(block, alphaBits);
        BinaryPrimitives.WriteUInt16LittleEndian(block[8..], color0);
        BinaryPrimitives.WriteUInt16LittleEndian(block[10..], color1);
        BinaryPrimitives.WriteUInt32LittleEndian(block[12..], colorSelectors);
    }
}
