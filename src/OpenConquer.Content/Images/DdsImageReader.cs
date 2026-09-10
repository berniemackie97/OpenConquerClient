using System.Buffers.Binary;

namespace OpenConquer.Content.Images;

internal static class DdsImageReader
{
    private const int MagicLength = 4;
    private const int DdsHeaderLength = 124;
    private const int EncodedHeaderLength = MagicLength + DdsHeaderLength;
    private const int PixelFormatOffset = 72;
    private const int MaximumDimension = 16_384;
    private const int MaximumDecodedLength = 256 * 1024 * 1024;

    private const uint Magic = 0x20534444;
    private const uint HeaderSize = 124;
    private const uint PixelFormatSize = 32;

    private const uint DdsdCaps = 0x00000001;
    private const uint DdsdHeight = 0x00000002;
    private const uint DdsdWidth = 0x00000004;
    private const uint DdsdPixelFormat = 0x00001000;
    private const uint DdsdMipMapCount = 0x00020000;
    private const uint DdsdLinearSize = 0x00080000;
    private const uint DdsdDepth = 0x00800000;

    private const uint DdpfFourCc = 0x00000004;
    private const uint Dxt3FourCc = 0x33545844;

    private const uint DdsCapsComplex = 0x00000008;
    private const uint DdsCapsTexture = 0x00001000;
    private const uint DdsCapsMipMap = 0x00400000;

    public static RgbaImage DecodeDxt3(ReadOnlySpan<byte> payload)
    {
        DdsImageLayout layout = ReadDxt3Layout(payload);

        byte[] pixels = GC.AllocateUninitializedArray<byte>(layout.DecodedLength);

        DecodeDxt3Blocks(payload.Slice(EncodedHeaderLength, layout.EncodedLength), layout.Width, layout.Height, pixels);

        return new RgbaImage(layout.Width, layout.Height, pixels);
    }

    private static DdsImageLayout ReadDxt3Layout(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < EncodedHeaderLength)
        {
            throw new InvalidDataException("The DDS image header is truncated.");
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(payload);

        if (magic != Magic)
        {
            throw new InvalidDataException($"The DDS image magic is 0x{magic:X8}; expected 0x{Magic:X8}.");
        }

        ReadOnlySpan<byte> header = payload.Slice(MagicLength, DdsHeaderLength);

        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(header);

        if (headerSize != HeaderSize)
        {
            throw new InvalidDataException($"The DDS header size is {headerSize}; expected {HeaderSize}.");
        }

        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
        const uint requiredFlags = DdsdCaps | DdsdHeight | DdsdWidth | DdsdPixelFormat | DdsdLinearSize;

        if ((flags & requiredFlags) != requiredFlags)
        {
            throw new InvalidDataException("The DDS header does not declare the required texture dimensions, pixel format, and linear size.");
        }

        uint heightValue = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
        uint widthValue = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);

        if (widthValue is 0 or > MaximumDimension || heightValue is 0 or > MaximumDimension)
        {
            throw new InvalidDataException("The DDS dimensions are invalid or exceed the supported limit.");
        }

        int width = (int)widthValue;
        int height = (int)heightValue;

        uint linearSize = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
        uint depth = BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);
        uint mipMapCount = BinaryPrimitives.ReadUInt32LittleEndian(header[24..]);

        if ((flags & DdsdDepth) != 0 || depth != 0)
        {
            throw new InvalidDataException("Volume DDS textures are not supported by the retail DXT3 image path.");
        }

        if ((flags & DdsdMipMapCount) != 0)
        {
            if (mipMapCount != 1)
            {
                throw new InvalidDataException("Mipmapped DDS textures are not supported by the retail DXT3 image path.");
            }
        }
        else if (mipMapCount != 0)
        {
            throw new InvalidDataException("The DDS header contains a mip-map count without declaring the mip-map-count flag.");
        }

        ReadOnlySpan<byte> pixelFormat = header[PixelFormatOffset..];

        uint pixelFormatSize = BinaryPrimitives.ReadUInt32LittleEndian(pixelFormat);

        if (pixelFormatSize != PixelFormatSize)
        {
            throw new InvalidDataException($"The DDS pixel-format size is {pixelFormatSize}; expected {PixelFormatSize}.");
        }

        uint pixelFormatFlags = BinaryPrimitives.ReadUInt32LittleEndian(pixelFormat[4..]);

        if (pixelFormatFlags != DdpfFourCc)
        {
            throw new InvalidDataException($"The DDS pixel-format flags are 0x{pixelFormatFlags:X8}; expected the FourCC-only retail texture format.");
        }

        uint fourCc = BinaryPrimitives.ReadUInt32LittleEndian(pixelFormat[8..]);

        if (fourCc != Dxt3FourCc)
        {
            throw new InvalidDataException($"The DDS FourCC is 0x{fourCc:X8}; expected DXT3.");
        }

        uint caps = BinaryPrimitives.ReadUInt32LittleEndian(header[104..]);
        uint caps2 = BinaryPrimitives.ReadUInt32LittleEndian(header[108..]);

        if ((caps & DdsCapsTexture) == 0)
        {
            throw new InvalidDataException("The DDS header does not declare a texture surface.");
        }

        if ((caps & (DdsCapsComplex | DdsCapsMipMap)) != 0)
        {
            throw new InvalidDataException("Complex or mipmapped DDS textures are not supported by the retail DXT3 image path.");
        }

        if (caps2 != 0)
        {
            throw new InvalidDataException("Cubemap and volume DDS texture capabilities are not supported by the retail DXT3 image path.");
        }

        int blockCountX = (width + 3) / 4;
        int blockCountY = (height + 3) / 4;
        int encodedLength = checked(blockCountX * blockCountY * 16);

        if (linearSize != (uint)encodedLength)
        {
            throw new InvalidDataException($"The DDS linear size is {linearSize} bytes; expected {encodedLength} bytes for the declared DXT3 dimensions.");
        }

        long decodedLength = checked((long)width * height * 4);

        if (decodedLength > MaximumDecodedLength)
        {
            throw new InvalidDataException("The decoded DDS image exceeds the supported memory limit.");
        }

        int expectedPayloadLength = checked(EncodedHeaderLength + encodedLength);

        if (payload.Length != expectedPayloadLength)
        {
            throw new InvalidDataException(payload.Length < expectedPayloadLength
                ? "The DDS DXT3 payload is truncated."
                : "The DDS image contains unsupported trailing data.");
        }

        return new DdsImageLayout(width, height, encodedLength, (int)decodedLength);
    }

    private static void DecodeDxt3Blocks(ReadOnlySpan<byte> encodedBlocks, int width, int height, Span<byte> destination)
    {
        int blockCountX = (width + 3) / 4;
        int blockCountY = (height + 3) / 4;
        int sourceOffset = 0;

        for (int blockY = 0; blockY < blockCountY; blockY++)
        {
            for (int blockX = 0; blockX < blockCountX; blockX++)
            {
                DecodeDxt3Block(encodedBlocks.Slice(sourceOffset, 16), blockX, blockY, width, height, destination);
                sourceOffset += 16;
            }
        }
    }

    private static void DecodeDxt3Block(ReadOnlySpan<byte> block, int blockX, int blockY, int width, int height, Span<byte> destination)
    {
        ulong alphaBits = BinaryPrimitives.ReadUInt64LittleEndian(block);
        ushort color0 = BinaryPrimitives.ReadUInt16LittleEndian(block[8..]);
        ushort color1 = BinaryPrimitives.ReadUInt16LittleEndian(block[10..]);
        uint colorSelectors = BinaryPrimitives.ReadUInt32LittleEndian(block[12..]);

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

            int paletteIndex = (int)((colorSelectors >> (pixelIndex * 2)) & 0x03);
            byte alpha = (byte)(((alphaBits >> (pixelIndex * 4)) & 0x0F) * 17);
            int destinationOffset = checked(((y * width) + x) * 4);

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

    private readonly record struct DdsImageLayout(int Width, int Height, int EncodedLength, int DecodedLength);
}
