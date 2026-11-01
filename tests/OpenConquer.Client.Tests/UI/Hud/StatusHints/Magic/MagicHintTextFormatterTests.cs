using System.Text;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints.Magic;

public sealed class MagicHintTextFormatterTests
{
    [Fact]
    public void Format_ResolvesLevelAndPreservesUnsignedThirtyTwoBitValues()
    {
        byte[] result = MagicHintTextFormatter.Format("Level: %u"u8, 255, uint.MaxValue);

        Assert.Equal("Level: 4294967295", Encoding.Latin1.GetString(result));
    }

    [Fact]
    public void Format_PreservesRequiredTrailingSpaceAndEscapedPercent()
    {
        byte[] result = MagicHintTextFormatter.Format("EXP: %0.3f%% "u8, 255, 12.345);

        Assert.Equal("EXP: 12.345% ", Encoding.Latin1.GetString(result));
    }

    [Fact]
    public void Format_HandlesSubprofessionRequirements()
    {
        byte[] result = MagicHintTextFormatter.Format(
            "Requires: P%d %s(Only P%d %s can use %s)"u8, 511,
            3, "Performer"u8.ToArray(), 3, "Performer"u8.ToArray(), "Dance"u8.ToArray());

        Assert.Equal(
            "Requires: P3 Performer(Only P3 Performer can use Dance)",
            Encoding.Latin1.GetString(result));
    }

    [Fact]
    public void Format_PreservesEncodedNameBytes()
    {
        byte[] name = [0xD6, 0xD0];

        byte[] result = MagicHintTextFormatter.Format("Steed: %s, Lineage Level: %d."u8, 255, name, 9);

        Assert.Equal(
            new byte[] { (byte)'S', (byte)'t', (byte)'e', (byte)'e', (byte)'d', (byte)':',
                (byte)' ', 0xD6, 0xD0, (byte)',', (byte)' ', (byte)'L',
                (byte)'i', (byte)'n', (byte)'e', (byte)'a', (byte)'g', (byte)'e',
                (byte)' ', (byte)'L', (byte)'e', (byte)'v', (byte)'e', (byte)'l',
                (byte)':', (byte)' ', (byte)'9', (byte)'.' },
            result);
    }

    [Fact]
    public void Format_TruncatesBeforeEscapeExpansion()
    {
        byte[] formatted = MagicHintTextFormatter.Format(
            "ABC\\nDEF"u8, 5);

        Assert.Equal("ABC\\n", Encoding.ASCII.GetString(formatted));
        Assert.Equal("ABC\n", Encoding.ASCII.GetString(MagicHintTextFormatter.TranslateEscapes(formatted)));
    }

    [Fact]
    public void TranslateEscapes_HandlesLineBreaksTabsAndLiteralBackslashes()
    {
        byte[] result = MagicHintTextFormatter.TranslateEscapes("A\\nB\\tC\\\\D\\"u8);

        Assert.Equal("A\nB\tC\\\\D\\"u8.ToArray(), result);
    }

    [Fact]
    public void TranslateEscapes_ReturnsIndependentCopyWhenNoEscapeIsPresent()
    {
        byte[] encoded = "A~B"u8.ToArray();

        byte[] result = MagicHintTextFormatter.TranslateEscapes(encoded);

        Assert.Equal(encoded, result);
        Assert.NotSame(encoded, result);
    }

    [Fact]
    public void Format_RejectsMismatchedDirectivesInsteadOfFabricatingValues()
    {
        Assert.Throws<InvalidDataException>(() => MagicHintTextFormatter.Format("%s"u8, 255));
        Assert.Throws<InvalidDataException>(() => MagicHintTextFormatter.Format("%d"u8, 255, "text"));
        Assert.Throws<InvalidDataException>(() => MagicHintTextFormatter.Format("%q"u8, 255, 1));
    }
}
