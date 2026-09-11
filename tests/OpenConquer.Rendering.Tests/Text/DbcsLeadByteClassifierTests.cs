using OpenConquer.Rendering.Text;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class DbcsLeadByteClassifierTests
{
    [Theory]
    [InlineData(0x80, false)]
    [InlineData(0x81, true)]
    [InlineData(0x9F, true)]
    [InlineData(0xA0, false)]
    [InlineData(0xDF, false)]
    [InlineData(0xE0, true)]
    [InlineData(0xFC, true)]
    [InlineData(0xFD, false)]
    public void IsLeadByte_CodePage932_UsesShiftJisLeadByteRanges(int value, bool expected)
    {
        Assert.Equal(expected, DbcsLeadByteClassifier.IsLeadByte(932, checked((byte)value)));
    }

    [Theory]
    [InlineData(936)]
    [InlineData(949)]
    [InlineData(950)]
    public void IsLeadByte_CodePagesWithSharedRange_Accepts81ThroughFe(int codePage)
    {
        Assert.False(DbcsLeadByteClassifier.IsLeadByte(codePage, 0x80));
        Assert.True(DbcsLeadByteClassifier.IsLeadByte(codePage, 0x81));
        Assert.True(DbcsLeadByteClassifier.IsLeadByte(codePage, 0xFE));
        Assert.False(DbcsLeadByteClassifier.IsLeadByte(codePage, 0xFF));
    }

    [Theory]
    [InlineData(0x83, false)]
    [InlineData(0x84, true)]
    [InlineData(0xD3, true)]
    [InlineData(0xD4, false)]
    [InlineData(0xD7, false)]
    [InlineData(0xD8, true)]
    [InlineData(0xDE, true)]
    [InlineData(0xDF, false)]
    [InlineData(0xE0, true)]
    [InlineData(0xF9, true)]
    [InlineData(0xFA, false)]
    public void IsLeadByte_CodePage1361_UsesJohabLeadByteRanges(int value, bool expected)
    {
        Assert.Equal(expected, DbcsLeadByteClassifier.IsLeadByte(1361, checked((byte)value)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1252)]
    [InlineData(65001)]
    public void IsLeadByte_NonDbcsOrUnsupportedCodePage_ReturnsFalse(int codePage)
    {
        for (int value = byte.MinValue; value <= byte.MaxValue; value++)
        {
            Assert.False(DbcsLeadByteClassifier.IsLeadByte(codePage, (byte)value));
        }
    }
}
