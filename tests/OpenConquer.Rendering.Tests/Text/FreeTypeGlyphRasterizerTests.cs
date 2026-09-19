using System.Text;
using OpenConquer.Rendering.Text;
using OpenConquer.Rendering.Text.Fonts;
using OpenConquer.Rendering.Text.Fonts.FreeType;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class FreeTypeGlyphRasterizerTests
{
    private static string FontFilePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Fonts", "Knewave-Regular.ttf");
    private static ResolvedFont Font => new(FontFilePath, faceIndex: 0);

    [Fact]
    public void Constructor_WithAntialiasing_RasterizesGlyph()
    {
        using FreeTypeLibrary library = new();
        using FreeTypeGlyphRasterizer rasterizer = new(library, Font, nominalPixelHeight: 16, antialiasEnabled: true);

        Assert.True(rasterizer.TryRasterizeGlyph(new Rune('A'), out RasterizedGlyph? glyph));

        Assert.NotNull(glyph);
        Assert.True(glyph.WidthPixels > 0);
        Assert.True(glyph.HeightPixels > 0);
        Assert.True(glyph.AdvancePixels > 0);
        Assert.Equal(glyph.WidthPixels * glyph.HeightPixels, glyph.Coverage.Length);
        Assert.Contains(glyph.Coverage.Span.ToArray(), value => value is > 0 and < byte.MaxValue);
    }

    [Fact]
    public void Constructor_WithoutAntialiasing_RasterizesBinaryCoverage()
    {
        using FreeTypeLibrary library = new();
        using FreeTypeGlyphRasterizer rasterizer = new(library, Font, nominalPixelHeight: 16, antialiasEnabled: false);

        Assert.True(rasterizer.TryRasterizeGlyph(new Rune('A'), out RasterizedGlyph? glyph));

        Assert.NotNull(glyph);
        Assert.True(glyph.WidthPixels > 0);
        Assert.True(glyph.HeightPixels > 0);
        Assert.True(glyph.AdvancePixels > 0);
        Assert.Equal(glyph.WidthPixels * glyph.HeightPixels, glyph.Coverage.Length);

        foreach (byte coverage in glyph.Coverage.Span)
        {
            Assert.True(coverage is byte.MinValue or byte.MaxValue);
        }

        Assert.Contains(byte.MaxValue, glyph.Coverage.Span.ToArray());
    }

    [Fact]
    public void TryRasterizeGlyph_MissingGlyph_ReturnsFalse()
    {
        using FreeTypeLibrary library = new();
        using FreeTypeGlyphRasterizer rasterizer = new(library, Font, nominalPixelHeight: 16, antialiasEnabled: true);

        Assert.False(rasterizer.TryRasterizeGlyph(new Rune(0x10FFFF), out RasterizedGlyph? glyph));
        Assert.Null(glyph);
    }

    [Fact]
    public void TryRasterizeGlyph_Space_PreservesAdvanceWithEmptyBitmap()
    {
        using FreeTypeLibrary library = new();
        using FreeTypeGlyphRasterizer rasterizer = new(library, Font, nominalPixelHeight: 16, antialiasEnabled: true);

        Assert.True(rasterizer.TryRasterizeGlyph(new Rune(' '), out RasterizedGlyph? glyph));

        Assert.NotNull(glyph);
        Assert.Equal(0, glyph.WidthPixels);
        Assert.Equal(0, glyph.HeightPixels);
        Assert.True(glyph.AdvancePixels > 0);
        Assert.True(glyph.Coverage.IsEmpty);
    }

    [Fact]
    public void Rasterizer_RemainsValidAfterLibraryOwnerIsDisposed()
    {
        FreeTypeLibrary library = new();
        using FreeTypeGlyphRasterizer rasterizer = new(library, Font, nominalPixelHeight: 16, antialiasEnabled: true);

        library.Dispose();

        Assert.True(rasterizer.TryRasterizeGlyph(new Rune('A'), out RasterizedGlyph? glyph));
        Assert.NotNull(glyph);
    }

    [Fact]
    public void TryRasterizeGlyph_AfterDispose_Throws()
    {
        using FreeTypeLibrary library = new();
        FreeTypeGlyphRasterizer rasterizer = new(library, Font, nominalPixelHeight: 16, antialiasEnabled: true);

        rasterizer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => rasterizer.TryRasterizeGlyph(new Rune('A'), out _));
    }

    [Fact]
    public void Dispose_CanBeCalledMoreThanOnce()
    {
        using FreeTypeLibrary library = new();
        FreeTypeGlyphRasterizer rasterizer = new(library, Font, nominalPixelHeight: 16, antialiasEnabled: true);

        rasterizer.Dispose();
        rasterizer.Dispose();
    }

    [Fact]
    public void Constructor_EmptyFontFile_ThrowsInvalidDataException()
    {
        string temporaryFilePath = Path.GetTempFileName();

        try
        {
            using FreeTypeLibrary library = new();
            ResolvedFont font = new(temporaryFilePath, faceIndex: 0);

            Assert.Throws<InvalidDataException>(() =>
                new FreeTypeGlyphRasterizer(library, font, nominalPixelHeight: 16, antialiasEnabled: true));
        }
        finally
        {
            File.Delete(temporaryFilePath);
        }
    }

    [Fact]
    public void Constructor_NonFontData_ThrowsFontFaceCreationException()
    {
        string temporaryFilePath = Path.GetTempFileName();

        try
        {
            File.WriteAllBytes(temporaryFilePath, [0x01, 0x02, 0x03, 0x04]);

            using FreeTypeLibrary library = new();
            ResolvedFont font = new(temporaryFilePath, faceIndex: 0);

            Assert.Throws<FreeTypeFaceCreationException>(() =>
                new FreeTypeGlyphRasterizer(library, font, nominalPixelHeight: 16, antialiasEnabled: true));
        }
        finally
        {
            File.Delete(temporaryFilePath);
        }
    }

    [Fact]
    public void Constructor_InvalidFaceIndex_ThrowsFontFaceCreationException()
    {
        using FreeTypeLibrary library = new();
        ResolvedFont font = new(FontFilePath, faceIndex: 1);

        Assert.Throws<FreeTypeFaceCreationException>(() =>
            new FreeTypeGlyphRasterizer(library, font, nominalPixelHeight: 16, antialiasEnabled: true));
    }

    [Fact]
    public void Constructor_ZeroNominalHeight_Throws()
    {
        using FreeTypeLibrary library = new();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FreeTypeGlyphRasterizer(library, Font, nominalPixelHeight: 0, antialiasEnabled: true));
    }

    [Fact]
    public void Constructor_NegativeNominalHeight_IsAccepted()
    {
        using FreeTypeLibrary library = new();
        using FreeTypeGlyphRasterizer rasterizer = new(library, Font, nominalPixelHeight: -1, antialiasEnabled: true);
    }

    [Fact]
    public void Constructor_HeightOverflow_PropagatesOverflowException()
    {
        using FreeTypeLibrary library = new();

        Assert.Throws<OverflowException>(() =>
            new FreeTypeGlyphRasterizer(library, Font, nominalPixelHeight: int.MaxValue, antialiasEnabled: true));
    }
}
