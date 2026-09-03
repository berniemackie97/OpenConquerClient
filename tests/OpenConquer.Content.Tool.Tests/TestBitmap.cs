using System.Buffers.Binary;

namespace OpenConquer.Content.Tool.Tests;

/// <summary>
/// Builds the smallest uncompressed 24-bit Windows bitmap the content readers accept.
/// </summary>
internal static class TestBitmap
{
    private const int FileHeaderLength = 14;
    private const int InfoHeaderLength = 40;

    public static byte[] CreateTwoByTwo()
    {
        const int Width = 2;
        const int Height = 2;
        const int RowLength = (Width * 3 + 3) & ~3;

        byte[] bitmap = new byte[FileHeaderLength + InfoHeaderLength + (RowLength * Height)];

        bitmap[0] = (byte)'B';
        bitmap[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(bitmap.AsSpan(2), (uint)bitmap.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bitmap.AsSpan(10), FileHeaderLength + InfoHeaderLength);
        BinaryPrimitives.WriteUInt32LittleEndian(bitmap.AsSpan(14), InfoHeaderLength);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(18), Width);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(22), Height);
        BinaryPrimitives.WriteUInt16LittleEndian(bitmap.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bitmap.AsSpan(28), 24);

        return bitmap;
    }
}
