using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class EncodedTextReaderTests
{
    [Fact]
    public void TryRead_EmptyInput_ReturnsFalse()
    {
        EncodedTextReader reader = new([], 936, recognizeDataIcons: false);

        Assert.False(reader.TryRead(out EncodedTextToken token));
        Assert.Equal(default, token);
    }

    [Fact]
    public void TryRead_NullOnlyInput_ReturnsFalse()
    {
        EncodedTextReader reader = new([0], 936, recognizeDataIcons: false);

        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void TryRead_SingleByteGlyph_UsesByteAsGlyphKey()
    {
        EncodedTextReader reader = new([(byte)'A'], 936, recognizeDataIcons: false);

        Assert.True(reader.TryRead(out EncodedTextToken token));
        Assert.Equal(EncodedTextTokenKind.Glyph, token.Kind);
        Assert.Equal(0x0041, token.GlyphKey);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void TryRead_DbcsCharacter_ConsumesLeadAndFollowingByte()
    {
        EncodedTextReader reader = new([0xB0, 0xD7], 936, recognizeDataIcons: false);

        Assert.True(reader.TryRead(out EncodedTextToken token));
        Assert.Equal(EncodedTextTokenKind.Glyph, token.Kind);
        Assert.Equal(0xB0D7, token.GlyphKey);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void TryRead_DbcsLeadByteAtEnd_IsSingleByteGlyph()
    {
        EncodedTextReader reader = new([0xB0], 936, recognizeDataIcons: false);

        Assert.True(reader.TryRead(out EncodedTextToken token));
        Assert.Equal(0x00B0, token.GlyphKey);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void TryRead_DbcsLeadByteBeforeNull_IsSingleByteGlyph()
    {
        EncodedTextReader reader = new([0xB0, 0x00, 0xD7], 936, recognizeDataIcons: false);

        Assert.True(reader.TryRead(out EncodedTextToken token));
        Assert.Equal(0x00B0, token.GlyphKey);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void TryRead_SingleByteCodePage_DoesNotConsumeFollowingByte()
    {
        EncodedTextReader reader = new([0xB0, 0xD7], 1252, recognizeDataIcons: false);

        Assert.True(reader.TryRead(out EncodedTextToken first));
        Assert.True(reader.TryRead(out EncodedTextToken second));

        Assert.Equal(0x00B0, first.GlyphKey);
        Assert.Equal(0x00D7, second.GlyphKey);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void TryRead_NewLine_ReturnsNewLineToken()
    {
        EncodedTextReader reader = new([(byte)'\n'], 936, recognizeDataIcons: false);

        Assert.True(reader.TryRead(out EncodedTextToken token));
        Assert.Equal(EncodedTextTokenKind.NewLine, token.Kind);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void TryRead_MixedText_PreservesNativeUnitOrder()
    {
        EncodedTextReader reader = new([(byte)'A', 0xB0, 0xD7, (byte)'\n', (byte)'B'], 936, recognizeDataIcons: false);

        Assert.True(reader.TryRead(out EncodedTextToken first));
        Assert.True(reader.TryRead(out EncodedTextToken second));
        Assert.True(reader.TryRead(out EncodedTextToken third));
        Assert.True(reader.TryRead(out EncodedTextToken fourth));
        Assert.False(reader.TryRead(out _));

        Assert.Equal(0x0041, first.GlyphKey);
        Assert.Equal(0xB0D7, second.GlyphKey);
        Assert.Equal(EncodedTextTokenKind.NewLine, third.Kind);
        Assert.Equal(0x0042, fourth.GlyphKey);
    }

    [Theory]
    [InlineData('0', '0', 0)]
    [InlineData('0', '7', 7)]
    [InlineData('4', '2', 42)]
    [InlineData('9', '9', 99)]
    public void TryRead_RecognizedDataIcon_ConsumesExactlyThreeBytes(char firstDigit, char secondDigit, int expectedIndex)
    {
        byte[] bytes = [(byte)'#', (byte)firstDigit, (byte)secondDigit, (byte)'A'];
        EncodedTextReader reader = new(bytes, 936, recognizeDataIcons: true);

        Assert.True(reader.TryRead(out EncodedTextToken icon));
        Assert.Equal(EncodedTextTokenKind.DataIcon, icon.Kind);
        Assert.Equal(expectedIndex, icon.DataIconIndex);

        Assert.True(reader.TryRead(out EncodedTextToken glyph));
        Assert.Equal(0x0041, glyph.GlyphKey);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void TryRead_DataIconEndingAtFinalByte_IsRecognized()
    {
        EncodedTextReader reader = new("#07"u8, 936, recognizeDataIcons: true);

        Assert.True(reader.TryRead(out EncodedTextToken token));
        Assert.Equal(EncodedTextTokenKind.DataIcon, token.Kind);
        Assert.Equal(7, token.DataIconIndex);
        Assert.False(reader.TryRead(out _));
    }

    [Theory]
    [InlineData("#")]
    [InlineData("#0")]
    [InlineData("#7x")]
    [InlineData("#ab")]
    [InlineData("##1")]
    public void TryRead_InvalidOrTruncatedDataIcon_StartsAsNormalGlyph(string text)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(text);
        EncodedTextReader reader = new(bytes, 936, recognizeDataIcons: true);

        Assert.True(reader.TryRead(out EncodedTextToken token));
        Assert.Equal(EncodedTextTokenKind.Glyph, token.Kind);
        Assert.Equal((ushort)'#', token.GlyphKey);
    }

    [Fact]
    public void TryRead_DataIconRecognitionDisabled_TreatsSequenceAsGlyphs()
    {
        EncodedTextReader reader = new("#07"u8, 936, recognizeDataIcons: false);

        Assert.True(reader.TryRead(out EncodedTextToken first));
        Assert.True(reader.TryRead(out EncodedTextToken second));
        Assert.True(reader.TryRead(out EncodedTextToken third));
        Assert.False(reader.TryRead(out _));

        Assert.Equal((ushort)'#', first.GlyphKey);
        Assert.Equal((ushort)'0', second.GlyphKey);
        Assert.Equal((ushort)'7', third.GlyphKey);
    }

    [Fact]
    public void TryRead_NullTerminatesTextBeforeFollowingBytes()
    {
        EncodedTextReader reader = new([(byte)'A', 0, (byte)'B'], 936, recognizeDataIcons: false);

        Assert.True(reader.TryRead(out EncodedTextToken token));
        Assert.Equal(0x0041, token.GlyphKey);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void TryRead_NullPreventsDataIconRecognitionAcrossTerminator()
    {
        EncodedTextReader reader = new([(byte)'#', (byte)'0', 0, (byte)'7'], 936, recognizeDataIcons: true);

        Assert.True(reader.TryRead(out EncodedTextToken first));
        Assert.True(reader.TryRead(out EncodedTextToken second));
        Assert.False(reader.TryRead(out _));

        Assert.Equal((ushort)'#', first.GlyphKey);
        Assert.Equal((ushort)'0', second.GlyphKey);
    }
}
