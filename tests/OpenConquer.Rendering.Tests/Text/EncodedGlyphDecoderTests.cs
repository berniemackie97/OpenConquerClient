using System.Text;
using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class EncodedGlyphDecoderTests
{
    [Fact]
    public void TryDecode_AsciiGlyph_ReturnsUnicodeScalar()
    {
        EncodedGlyphDecoder decoder = new(936);

        Assert.True(decoder.TryDecode(0x0041, out Rune character));
        Assert.Equal(new Rune('A'), character);
    }

    [Fact]
    public void TryDecode_Cp936DoubleByteGlyph_ReturnsUnicodeScalar()
    {
        EncodedGlyphDecoder decoder = new(936);

        Assert.True(decoder.TryDecode(0xB0D7, out Rune character));
        Assert.Equal(new Rune('白'), character);
    }

    [Fact]
    public void TryDecode_Cp932DoubleByteGlyph_ReturnsUnicodeScalar()
    {
        EncodedGlyphDecoder decoder = new(932);

        Assert.True(decoder.TryDecode(0x82A0, out Rune character));
        Assert.Equal(new Rune('あ'), character);
    }

    [Fact]
    public void TryDecode_SingleByteCodePage_ReturnsMappedUnicodeScalar()
    {
        EncodedGlyphDecoder decoder = new(1252);

        Assert.True(decoder.TryDecode(0x0080, out Rune character));
        Assert.Equal(new Rune('€'), character);
    }

    [Fact]
    public void TryDecode_DanglingDbcsLeadByte_ReturnsFalse()
    {
        EncodedGlyphDecoder decoder = new(936);

        Assert.False(decoder.TryDecode(0x0081, out Rune character));
        Assert.Equal(default, character);
    }

    [Fact]
    public void TryDecode_InvalidDbcsPair_ReturnsFalse()
    {
        EncodedGlyphDecoder decoder = new(936);

        Assert.False(decoder.TryDecode(0x817F, out Rune character));
        Assert.Equal(default, character);
    }

    [Fact]
    public void TryDecode_WhenEncodedBytesProduceMultipleScalars_ReturnsFalse()
    {
        EncodedGlyphDecoder decoder = new(1252);

        Assert.False(decoder.TryDecode(0x4142, out Rune character));
        Assert.Equal(default, character);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void TryDecode_UnavailableCodePage_ReturnsFalse(int effectiveCodePage)
    {
        EncodedGlyphDecoder decoder = new(effectiveCodePage);

        Assert.False(decoder.TryDecode(0x0041, out Rune character));
        Assert.Equal(default, character);
    }
}
