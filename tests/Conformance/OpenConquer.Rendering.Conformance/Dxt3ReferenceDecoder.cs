using System.Buffers.Binary;

namespace OpenConquer.Rendering.Conformance;

/// <summary>
/// Independently decodes a verified single-level DXT3 DDS for rendering conformance.
/// </summary>
internal static class Dxt3ReferenceDecoder
{
    private const int HeaderLength = 128;
    private const uint Magic = 0x20534444;
    private const uint HeaderSize = 124;
    private const uint PixelFormatSize = 32;
    private const uint FourCcFlag = 0x00000004;
    private const uint Dxt3FourCc = 0x33545844;

    public static byte[] Decode(ReadOnlySpan<byte> dds, int expectedWidth, int expectedHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedHeight);

        if (dds.Length < HeaderLength)
        {
            throw new InvalidDataException("The reference DDS header is truncated.");
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(dds);
        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(dds[4..]);
        int height = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(dds[12..]));
        int width = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(dds[16..]));
        uint linearSize = BinaryPrimitives.ReadUInt32LittleEndian(dds[20..]);
        uint pixelFormatSize = BinaryPrimitives.ReadUInt32LittleEndian(dds[76..]);
        uint pixelFormatFlags = BinaryPrimitives.ReadUInt32LittleEndian(dds[80..]);
        uint fourCc = BinaryPrimitives.ReadUInt32LittleEndian(dds[84..]);

        if (magic != Magic || headerSize != HeaderSize || pixelFormatSize != PixelFormatSize)
        {
            throw new InvalidDataException("The reference DDS does not contain a standard DDS/DXT header.");
        }

        if (width != expectedWidth || height != expectedHeight)
        {
            throw new InvalidDataException($"The reference DDS is {width}x{height}; expected {expectedWidth}x{expectedHeight}.");
        }

        if (pixelFormatFlags != FourCcFlag || fourCc != Dxt3FourCc)
        {
            throw new InvalidDataException($"The reference DDS pixel format is flags 0x{pixelFormatFlags:X8}, FourCC 0x{fourCc:X8}; expected DXT3.");
        }

        int blockCountX = (width + 3) / 4;
        int blockCountY = (height + 3) / 4;
        int encodedLength = checked(blockCountX * blockCountY * 16);

        if (linearSize != (uint)encodedLength || dds.Length != checked(HeaderLength + encodedLength))
        {
            throw new InvalidDataException("The reference DDS does not contain exactly one complete DXT3 level.");
        }

        byte[] rgba = new byte[checked(width * height * 4)];
        int sourceOffset = HeaderLength;

        for (int blockY = 0; blockY < blockCountY; blockY++)
        {
            for (int blockX = 0; blockX < blockCountX; blockX++)
            {
                DecodeBlock(dds.Slice(sourceOffset, 16), blockX, blockY, width, height, rgba);
                sourceOffset += 16;
            }
        }

        return rgba;
    }

    private static void DecodeBlock(ReadOnlySpan<byte> block, int blockX, int blockY, int width, int height, Span<byte> destination)
    {
        ulong alphaBits = BinaryPrimitives.ReadUInt64LittleEndian(block);
        ushort color0 = BinaryPrimitives.ReadUInt16LittleEndian(block[8..]);
        ushort color1 = BinaryPrimitives.ReadUInt16LittleEndian(block[10..]);
        uint selectors = BinaryPrimitives.ReadUInt32LittleEndian(block[12..]);

        Span<byte> red = stackalloc byte[4];
        Span<byte> green = stackalloc byte[4];
        Span<byte> blue = stackalloc byte[4];

        DecodeRgb565(color0, out red[0], out green[0], out blue[0]);
        DecodeRgb565(color1, out red[1], out green[1], out blue[1]);

        red[2] = InterpolateTwoThirds(red[0], red[1]);
        green[2] = InterpolateTwoThirds(green[0], green[1]);
        blue[2] = InterpolateTwoThirds(blue[0], blue[1]);

        red[3] = InterpolateTwoThirds(red[1], red[0]);
        green[3] = InterpolateTwoThirds(green[1], green[0]);
        blue[3] = InterpolateTwoThirds(blue[1], blue[0]);

        for (int pixelIndex = 0; pixelIndex < 16; pixelIndex++)
        {
            int x = (blockX * 4) + (pixelIndex & 3);
            int y = (blockY * 4) + (pixelIndex >> 2);

            if (x >= width || y >= height)
            {
                continue;
            }

            int paletteIndex = (int)((selectors >> (pixelIndex * 2)) & 0x03);
            byte alpha = (byte)(((alphaBits >> (pixelIndex * 4)) & 0x0F) * 17);
            int destinationOffset = ((y * width) + x) * 4;

            destination[destinationOffset] = red[paletteIndex];
            destination[destinationOffset + 1] = green[paletteIndex];
            destination[destinationOffset + 2] = blue[paletteIndex];
            destination[destinationOffset + 3] = alpha;
        }
    }

    private static void DecodeRgb565(ushort packedColor, out byte red, out byte green, out byte blue)
    {
        int red5 = (packedColor >> 11) & 0x1F;
        int green6 = (packedColor >> 5) & 0x3F;
        int blue5 = packedColor & 0x1F;

        red = (byte)((red5 << 3) | (red5 >> 2));
        green = (byte)((green6 << 2) | (green6 >> 4));
        blue = (byte)((blue5 << 3) | (blue5 >> 2));
    }

    private static byte InterpolateTwoThirds(byte primary, byte secondary)
    {
        return (byte)(((2 * primary) + secondary) / 3);
    }
}
