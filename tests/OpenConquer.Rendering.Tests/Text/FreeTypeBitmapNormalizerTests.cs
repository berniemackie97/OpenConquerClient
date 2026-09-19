using System.Runtime.InteropServices;
using OpenConquer.Rendering.Text;
using OpenConquer.Rendering.Text.Fonts.FreeType;
using OpenConquer.Rendering.Text.Native;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class FreeTypeBitmapNormalizerTests
{
    [Fact]
    public void CopyCoverage_GrayscaleWithPaddedPitch_ReturnsTightlyPackedRows()
    {
        byte[] source =
        [
            10, 20, 0xEE,
            30, 40, 0xEE,
        ];

        byte[] coverage = WithBuffer(source, buffer => FreeTypeBitmapNormalizer.CopyCoverage(buffer, widthPixels: 2, heightPixels: 2, pitch: 3, FreeTypePixelMode.Gray, grayLevels: 256));

        Assert.Equal([10, 20, 30, 40], coverage);
    }

    [Fact]
    public void CopyCoverage_GrayscaleWithNegativePitch_PreservesLogicalRowOrder()
    {
        byte[] source =
        [
            30, 40, 0xEE,
            10, 20, 0xEE,
        ];

        byte[] coverage = WithBuffer(source, buffer => FreeTypeBitmapNormalizer.CopyCoverage(buffer, widthPixels: 2, heightPixels: 2, pitch: -3, FreeTypePixelMode.Gray, grayLevels: 256));

        Assert.Equal([10, 20, 30, 40], coverage);
    }

    [Fact]
    public void CopyCoverage_GrayscaleWithReducedLevels_NormalizesToByteRange()
    {
        byte[] source = [0, 1, 2, 3];

        byte[] coverage = WithBuffer(source, buffer => FreeTypeBitmapNormalizer.CopyCoverage(buffer, widthPixels: 4, heightPixels: 1, pitch: 4, FreeTypePixelMode.Gray, grayLevels: 4));

        Assert.Equal([0, 85, 170, 255], coverage);
    }

    [Fact]
    public void CopyCoverage_GrayscaleWithInvalidLevelCount_Throws()
    {
        byte[] source = [0];

        WithBuffer(source, buffer =>
        {
            Assert.Throws<InvalidOperationException>(() => FreeTypeBitmapNormalizer.CopyCoverage(buffer, widthPixels: 1, heightPixels: 1, pitch: 1, FreeTypePixelMode.Gray, grayLevels: 1));

            Assert.Throws<InvalidOperationException>(() => FreeTypeBitmapNormalizer.CopyCoverage(buffer, widthPixels: 1, heightPixels: 1, pitch: 1, FreeTypePixelMode.Gray, grayLevels: 257));
        });
    }

    [Fact]
    public void CopyCoverage_GrayscaleValueOutsideDeclaredRange_Throws()
    {
        byte[] source = [4];

        WithBuffer(source, buffer => Assert.Throws<InvalidOperationException>(() => FreeTypeBitmapNormalizer.CopyCoverage(buffer, widthPixels: 1, heightPixels: 1, pitch: 1, FreeTypePixelMode.Gray, grayLevels: 4)));
    }

    [Fact]
    public void CopyCoverage_Monochrome_ExpandsMostSignificantBitsAndIgnoresPadding()
    {
        byte[] source =
        [
            0b1010_0101, 0b1000_0000, 0xEE,
            0b0101_1010, 0b0100_0000, 0xEE,
        ];

        byte[] coverage = WithBuffer(source, buffer => FreeTypeBitmapNormalizer.CopyCoverage(buffer, widthPixels: 10, heightPixels: 2, pitch: 3, FreeTypePixelMode.Mono, grayLevels: 2));

        byte[] expected =
        [
            255, 0, 255, 0, 0, 255, 0, 255, 255, 0,
            0, 255, 0, 255, 255, 0, 255, 0, 0, 255,
        ];

        Assert.Equal(expected, coverage);
    }

    [Fact]
    public void CopyCoverage_MonochromeWithNegativePitch_PreservesLogicalRowOrder()
    {
        byte[] source =
        [
            0b0101_1010, 0b0100_0000, 0xEE,
            0b1010_0101, 0b1000_0000, 0xEE,
        ];

        byte[] coverage = WithBuffer(source, buffer => FreeTypeBitmapNormalizer.CopyCoverage(buffer, widthPixels: 10, heightPixels: 2, pitch: -3, FreeTypePixelMode.Mono, grayLevels: 2));

        byte[] expected =
        [
            255, 0, 255, 0, 0, 255, 0, 255, 255, 0,
            0, 255, 0, 255, 255, 0, 255, 0, 0, 255,
        ];

        Assert.Equal(expected, coverage);
    }

    [Fact]
    public void CopyCoverage_EmptyBitmap_AllowsNullBufferAndZeroPitch()
    {
        byte[] coverage = FreeTypeBitmapNormalizer.CopyCoverage(nint.Zero, widthPixels: 0, heightPixels: 4, pitch: 0, FreeTypePixelMode.Gray, grayLevels: 0);

        Assert.Empty(coverage);
    }

    [Fact]
    public void CopyCoverage_NonEmptyBitmapWithNullBuffer_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => FreeTypeBitmapNormalizer.CopyCoverage(nint.Zero, widthPixels: 1, heightPixels: 1, pitch: 1, FreeTypePixelMode.Gray, grayLevels: 256));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public void CopyCoverage_GrayscaleWithInvalidPitch_Throws(int pitch)
    {
        byte[] source = [0, 0];

        WithBuffer(source, buffer => Assert.Throws<InvalidOperationException>(() => FreeTypeBitmapNormalizer.CopyCoverage(buffer, widthPixels: 2, heightPixels: 1, pitch, FreeTypePixelMode.Gray, grayLevels: 256)));
    }

    [Fact]
    public void CopyCoverage_UnsupportedPixelMode_Throws()
    {
        byte[] source = [0];

        WithBuffer(source, buffer => Assert.Throws<InvalidOperationException>(() => FreeTypeBitmapNormalizer.CopyCoverage(buffer, widthPixels: 1, heightPixels: 1, pitch: 1, (FreeTypePixelMode)byte.MaxValue, grayLevels: 256)));
    }

    [Fact]
    public void CopyCoverage_BitmapAreaOverflow_Throws()
    {
        Assert.Throws<OverflowException>(() => FreeTypeBitmapNormalizer.CopyCoverage(nint.Zero, widthPixels: int.MaxValue, heightPixels: 2, pitch: int.MaxValue, FreeTypePixelMode.Gray, grayLevels: 256));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void CopyCoverage_NegativeDimensions_Throw(int widthPixels, int heightPixels)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FreeTypeBitmapNormalizer.CopyCoverage(nint.Zero, widthPixels, heightPixels, pitch: 1, FreeTypePixelMode.Gray, grayLevels: 256));
    }

    private static T WithBuffer<T>(byte[] source, Func<nint, T> operation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(operation);

        nint allocation = Marshal.AllocHGlobal(source.Length);

        try
        {
            Marshal.Copy(source, 0, allocation, source.Length);
            return operation(allocation);
        }
        finally
        {
            Marshal.FreeHGlobal(allocation);
        }
    }

    private static void WithBuffer(byte[] source, Action<nint> operation)
    {
        WithBuffer(source, buffer =>
        {
            operation(buffer);
            return true;
        });
    }
}
