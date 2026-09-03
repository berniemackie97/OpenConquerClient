using System.Buffers.Binary;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Tests.Images;

public sealed class WindowsBitmapReaderTests
{
    [Fact]
    public void Decode24Bit_ConvertsBottomUpBgrRowsToTopDownRgba()
    {
        byte[] bitmap = CreateTwoByTwoBitmap();

        RgbaImage image = WindowsBitmapReader.Decode24Bit(bitmap);

        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(
            [
                255, 0, 0, 255,
                0, 255, 0, 255,
                0, 0, 255, 255,
                255, 255, 255, 255,
            ],
            image.Pixels.ToArray()
        );
    }

    [Fact]
    public void Decode24Bit_RejectsTruncatedPixelPayload()
    {
        byte[] bitmap = CreateTwoByTwoBitmap()[..^1];

        Assert.Throws<InvalidDataException>(() => WindowsBitmapReader.Decode24Bit(bitmap));
    }

    internal static byte[] CreateTwoByTwoBitmap()
    {
        const int pixelOffset = 54;
        const int rowLength = 8;
        byte[] bitmap = new byte[pixelOffset + rowLength * 2];
        bitmap[0] = (byte)'B';
        bitmap[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(2), bitmap.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(10), pixelOffset);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(18), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(22), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bitmap.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bitmap.AsSpan(28), 24);

        // BMP rows are bottom-up. Each row is padded to four bytes.
        bitmap.AsSpan(pixelOffset, 6).CopyFrom([255, 0, 0, 255, 255, 255]);
        bitmap.AsSpan(pixelOffset + rowLength, 6).CopyFrom([0, 0, 255, 0, 255, 0]);

        return bitmap;
    }
}

internal static class SpanTestExtensions
{
    public static void CopyFrom(this Span<byte> destination, ReadOnlySpan<byte> source)
    {
        source.CopyTo(destination);
    }
}
