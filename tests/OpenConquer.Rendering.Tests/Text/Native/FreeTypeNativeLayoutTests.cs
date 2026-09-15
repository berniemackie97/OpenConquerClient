using System.Runtime.InteropServices;
using OpenConquer.Rendering.Text.Native;

namespace OpenConquer.Rendering.Tests.Text.Native;

public sealed class FreeTypeNativeLayoutTests
{
    [Fact]
    public void CNativeLongWidths_MatchPlatformAbi()
    {
        int expectedLongSize = IntPtr.Size == 4 || OperatingSystem.IsWindows() ? 4 : 8;

        Assert.Equal(expectedLongSize, Marshal.SizeOf<CLong>());
        Assert.Equal(expectedLongSize, Marshal.SizeOf<CULong>());
    }

    [Fact]
    public void CLongBasedValueTypes_MatchPlatformAbi()
    {
        int longSize = IntPtr.Size == 4 || OperatingSystem.IsWindows() ? 4 : 8;

        Assert.Equal(IntPtr.Size * 2, Marshal.SizeOf<FreeTypeGeneric>());
        Assert.Equal(longSize * 2, Marshal.SizeOf<FreeTypeVector>());
        Assert.Equal(longSize * 4, Marshal.SizeOf<FreeTypeBoundingBox>());
        Assert.Equal(longSize * 8, Marshal.SizeOf<FreeTypeGlyphMetrics>());
    }

    [Fact]
    public void FreeTypeBitmap_MatchesPublicAbi()
    {
        if (IntPtr.Size == 4)
        {
            Assert.Equal(24, Marshal.SizeOf<FreeTypeBitmap>());
            AssertOffset<FreeTypeBitmap>(nameof(FreeTypeBitmap.Buffer), 12);
            AssertOffset<FreeTypeBitmap>(nameof(FreeTypeBitmap.GrayLevels), 16);
            AssertOffset<FreeTypeBitmap>(nameof(FreeTypeBitmap.PixelMode), 18);
            AssertOffset<FreeTypeBitmap>(nameof(FreeTypeBitmap.PaletteMode), 19);
            AssertOffset<FreeTypeBitmap>(nameof(FreeTypeBitmap.Palette), 20);
            return;
        }

        Assert.Equal(40, Marshal.SizeOf<FreeTypeBitmap>());
        AssertOffset<FreeTypeBitmap>(nameof(FreeTypeBitmap.Buffer), 16);
        AssertOffset<FreeTypeBitmap>(nameof(FreeTypeBitmap.GrayLevels), 24);
        AssertOffset<FreeTypeBitmap>(nameof(FreeTypeBitmap.PixelMode), 26);
        AssertOffset<FreeTypeBitmap>(nameof(FreeTypeBitmap.PaletteMode), 27);
        AssertOffset<FreeTypeBitmap>(nameof(FreeTypeBitmap.Palette), 32);
    }

    [Fact]
    public void FreeTypeGlyphSlotRecord_MatchesPublicPrefixAbi()
    {
        if (IntPtr.Size == 4)
        {
            Assert.Equal(108, Marshal.SizeOf<FreeTypeGlyphSlotRecord>());
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.GlyphIndex), 12);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Generic), 16);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Metrics), 24);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Advance), 64);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Bitmap), 76);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.BitmapLeft), 100);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.BitmapTop), 104);
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(152, Marshal.SizeOf<FreeTypeGlyphSlotRecord>());
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.GlyphIndex), 24);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Generic), 32);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Metrics), 48);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Advance), 88);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Bitmap), 104);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.BitmapLeft), 144);
            AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.BitmapTop), 148);
            return;
        }

        Assert.Equal(200, Marshal.SizeOf<FreeTypeGlyphSlotRecord>());
        AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.GlyphIndex), 24);
        AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Generic), 32);
        AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Metrics), 48);
        AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Advance), 128);
        AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.Bitmap), 152);
        AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.BitmapLeft), 192);
        AssertOffset<FreeTypeGlyphSlotRecord>(nameof(FreeTypeGlyphSlotRecord.BitmapTop), 196);
    }

    [Fact]
    public void FreeTypeFaceRecord_MatchesPublicPrefixAbi()
    {
        if (IntPtr.Size == 4)
        {
            Assert.Equal(88, Marshal.SizeOf<FreeTypeFaceRecord>());
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.FamilyName), 20);
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.AvailableSizes), 32);
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.CharacterMaps), 40);
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.Generic), 44);
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.BoundingBox), 52);
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.Glyph), 84);
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(128, Marshal.SizeOf<FreeTypeFaceRecord>());
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.FamilyName), 24);
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.AvailableSizes), 48);
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.CharacterMaps), 64);
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.Generic), 72);
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.BoundingBox), 88);
            AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.Glyph), 120);
            return;
        }

        Assert.Equal(160, Marshal.SizeOf<FreeTypeFaceRecord>());
        AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.FamilyName), 40);
        AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.AvailableSizes), 64);
        AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.CharacterMaps), 80);
        AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.Generic), 88);
        AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.BoundingBox), 104);
        AssertOffset<FreeTypeFaceRecord>(nameof(FreeTypeFaceRecord.Glyph), 152);
    }

    private static void AssertOffset<T>(string fieldName, int expected)
        where T : struct
    {
        Assert.Equal(expected, Marshal.OffsetOf<T>(fieldName).ToInt32());
    }
}
