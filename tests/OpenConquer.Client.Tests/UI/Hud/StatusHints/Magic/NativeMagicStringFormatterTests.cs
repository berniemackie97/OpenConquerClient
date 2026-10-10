using System.Text;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints.Magic;

public sealed class NativeMagicStringFormatterTests
{
    [Fact]
    public void Format_ResolvesLevelAndPreservesUnsignedThirtyTwoBitValues()
    {
        byte[] result = NativeMagicStringFormatter.Format("Level: %u"u8, 255, uint.MaxValue);

        Assert.Equal("Level: 4294967295", Encoding.Latin1.GetString(result));
    }

    [Fact]
    public void Format_PreservesRequiredTrailingSpaceAndEscapedPercent()
    {
        byte[] result = NativeMagicStringFormatter.Format("EXP: %0.3f%% "u8, 255, 12.345);

        Assert.Equal("EXP: 12.345% ", Encoding.Latin1.GetString(result));
    }

    [Fact]
    public void Format_HandlesNativeSubprofessionRequirements()
    {
        byte[] result = NativeMagicStringFormatter.Format(
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

        byte[] result = NativeMagicStringFormatter.Format("Steed: %s, Lineage Level: %d."u8, 255, name, 9);

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
        byte[] formatted = NativeMagicStringFormatter.Format(
            "ABC\\nDEF"u8, 5);

        Assert.Equal("ABC\\n", Encoding.ASCII.GetString(formatted));
        Assert.Equal("ABC\n", Encoding.ASCII.GetString(NativeMagicStringFormatter.TranslateEscapes(formatted)));
    }

    [Fact]
    public void Format_RejectsMismatchedDirectivesInsteadOfFabricatingValues()
    {
        Assert.Throws<InvalidDataException>(() => NativeMagicStringFormatter.Format("%s"u8, 255));
        Assert.Throws<InvalidDataException>(() => NativeMagicStringFormatter.Format("%d"u8, 255, "text"));
        Assert.Throws<InvalidDataException>(() => NativeMagicStringFormatter.Format("%q"u8, 255, 1));
    }
}
