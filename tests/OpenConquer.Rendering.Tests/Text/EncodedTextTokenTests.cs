using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class EncodedTextTokenTests
{
    [Fact]
    public void CreateSingleByteGlyph_UsesByteAsNativeGlyphKey()
    {
        EncodedTextToken token = EncodedTextToken.CreateSingleByteGlyph(0x41);

        Assert.Equal(EncodedTextTokenKind.Glyph, token.Kind);
        Assert.Equal(0x0041, token.GlyphKey);
    }

    [Theory]
    [InlineData(0xB0, 0xD7, 0xB0D7)]
    [InlineData(0x81, 0x40, 0x8140)]
    [InlineData(0xFE, 0xFE, 0xFEFE)]
    public void CreateDoubleByteGlyph_UsesNativeLeadTrailKey(int leadByte, int trailByte, int expectedGlyphKey)
    {
        EncodedTextToken token = EncodedTextToken.CreateDoubleByteGlyph(
            checked((byte)leadByte),
            checked((byte)trailByte));

        Assert.Equal(EncodedTextTokenKind.Glyph, token.Kind);
        Assert.Equal(expectedGlyphKey, token.GlyphKey);
    }

    [Fact]
    public void NewLine_RepresentsNewLineWithoutGlyphOrDataIconValue()
    {
        EncodedTextToken token = EncodedTextToken.NewLine;

        Assert.Equal(EncodedTextTokenKind.NewLine, token.Kind);
        Assert.Throws<InvalidOperationException>(() => _ = token.GlyphKey);
        Assert.Throws<InvalidOperationException>(() => _ = token.DataIconIndex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(99)]
    public void CreateDataIcon_PreservesDecimalIndex(int index)
    {
        EncodedTextToken token = EncodedTextToken.CreateDataIcon(checked((byte)index));

        Assert.Equal(EncodedTextTokenKind.DataIcon, token.Kind);
        Assert.Equal(index, token.DataIconIndex);
    }

    [Fact]
    public void CreateDataIcon_RejectsIndexAboveTwoDigitRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EncodedTextToken.CreateDataIcon(100));
    }

    [Fact]
    public void GlyphToken_RejectsDataIconAccess()
    {
        EncodedTextToken token = EncodedTextToken.CreateSingleByteGlyph(0x41);

        Assert.Throws<InvalidOperationException>(() => _ = token.DataIconIndex);
    }

    [Fact]
    public void DataIconToken_RejectsGlyphAccess()
    {
        EncodedTextToken token = EncodedTextToken.CreateDataIcon(7);

        Assert.Throws<InvalidOperationException>(() => _ = token.GlyphKey);
    }

    [Fact]
    public void DefaultToken_DoesNotRepresentAValidSemanticKind()
    {
        EncodedTextToken token = default;

        Assert.Equal(0, (int)token.Kind);
        Assert.Throws<InvalidOperationException>(() => _ = token.GlyphKey);
        Assert.Throws<InvalidOperationException>(() => _ = token.DataIconIndex);
    }
}
