using System.Text;
using OpenConquer.Content.Magic;

namespace OpenConquer.Content.Tests.Magic;

public sealed class MagicEffectFileTests
{
    [Fact]
    public void Parse_ResolvesNativeBaseAndLevelSpecificText()
    {
        MagicEffectFile file = Parse(
            "[10000]\r\n" +
            "Name=Thunder\r\n" +
            "Desc=Upgradable\r\n" +
            "DescEx=Magic~attack\r\n" +
            "[10001]\r\n" +
            "Desc=Upgrade~after~lvl~10\r\n" +
            "DescEx=Magic~attack\r\n");

        Assert.Equal("Thunder", GetText(file, 1000, 0, MagicEffectTextKind.Name));
        Assert.Equal("Thunder", GetText(file, 1000, 1, MagicEffectTextKind.Name));
        Assert.Equal("Upgradable", GetText(file, 1000, 0, MagicEffectTextKind.Description));
        Assert.Equal("Upgrade after lvl 10", GetText(file, 1000, 1, MagicEffectTextKind.Description));
        Assert.Equal("Magic attack", GetText(file, 1000, 1, MagicEffectTextKind.ExtendedDescription));
    }

    [Fact]
    public void GetEncoded_FallsBackOnMissingOrEmptyLevelOverride()
    {
        MagicEffectFile file = Parse(
            "[10000]\n" +
            "Name=BaseName\n" +
            "Desc=BaseDescription\n" +
            "DescEx=BaseExtended\n" +
            "[10001]\n" +
            "Name=\n" +
            "Desc=    ;empty override\n" +
            "[10002]\n" +
            "Name=NewName\n");

        Assert.Equal("BaseName", GetText(file, 1000, 1, MagicEffectTextKind.Name));
        Assert.Equal("BaseDescription", GetText(file, 1000, 1, MagicEffectTextKind.Description));
        Assert.Equal("BaseExtended", GetText(file, 1000, 1, MagicEffectTextKind.ExtendedDescription));

        Assert.Equal("NewName", GetText(file, 1000, 2, MagicEffectTextKind.Name));
        Assert.Equal("BaseDescription", GetText(file, 1000, 2, MagicEffectTextKind.Description));
        Assert.Equal("BaseName", GetText(file, 1000, 3, MagicEffectTextKind.Name));
    }

    [Fact]
    public void Parse_UsesCaseInsensitiveKeysAndLastDuplicateValue()
    {
        MagicEffectFile file = Parse(
            "[30000]\n" +
            "Name=Fire\n" +
            "[30000]\n" +
            "nAmE=HumanMessenger\n" +
            "dEsCeX=Magic~attack\n");

        Assert.Equal("HumanMessenger", GetText(file, 3000, 0, MagicEffectTextKind.Name));
        Assert.Equal("Magic attack", GetText(file, 3000, 0, MagicEffectTextKind.ExtendedDescription));
    }

    [Fact]
    public void Parse_PreservesNativeValueWhitespaceAndTerminators()
    {
        MagicEffectFile file = Parse(
            "[10000]\n" +
            "Name= \tThunder  ;comment\n" +
            "DescEx=Magic~attack\tignored\n" +
            "Desc=Text=Value\n");

        Assert.Equal("Thunder  ", GetText(file, 1000, 0, MagicEffectTextKind.Name));
        Assert.Equal("Magic attack", GetText(file, 1000, 0, MagicEffectTextKind.ExtendedDescription));
        Assert.Equal("Text=Value", GetText(file, 1000, 0, MagicEffectTextKind.Description));
    }

    [Fact]
    public void GetEncoded_PreservesNonAsciiBytesWithoutExpandingEscapes()
    {
        byte[] source = Encoding.Latin1.GetBytes("[10000]\nName=\u00D6\u00D0~line\\n\n");
        MagicEffectFile file = MagicEffectFile.Parse(source);

        Assert.Equal(
            new byte[] { 0xD6, 0xD0, (byte)' ', (byte)'l', (byte)'i', (byte)'n', (byte)'e', (byte)'\\', (byte)'n' },
            file.GetEncoded(1000, 0, MagicEffectTextKind.Name).ToArray());
    }

    [Fact]
    public void GetEncoded_UsesSignedLowThirtyTwoBitSectionKeys()
    {
        MagicEffectFile file = Parse(
            "[-10]\n" +
            "Name=Base\n" +
            "[-8]\n" +
            "Name=Override\n");

        Assert.Equal("Base", GetText(file, uint.MaxValue, 0, MagicEffectTextKind.Name));
        Assert.Equal("Override", GetText(file, uint.MaxValue, 2, MagicEffectTextKind.Name));
        Assert.Equal("Base", GetText(file, uint.MaxValue, 3, MagicEffectTextKind.Name));
    }

    [Fact]
    public void GetEncoded_ReturnsEmptyWhenSourceOrKeyIsMissing()
    {
        MagicEffectFile file = Parse("[10000]\nName=Thunder\n");

        Assert.True(file.GetEncoded(1000, 0, MagicEffectTextKind.Description).IsEmpty);
        Assert.True(file.GetEncoded(2000, 0, MagicEffectTextKind.Name).IsEmpty);
        Assert.True(file.GetEncoded(2000, 1, MagicEffectTextKind.ExtendedDescription).IsEmpty);
    }

    [Fact]
    public void GetEncoded_RejectsUnknownNativeTextKind()
    {
        MagicEffectFile file = Parse("[10000]\nName=Thunder\n");

        Assert.Throws<ArgumentOutOfRangeException>(() => file.GetEncoded(1000, 0, (MagicEffectTextKind)2));
        Assert.Throws<ArgumentOutOfRangeException>(() => file.GetEncoded(1000, 0, (MagicEffectTextKind)255));
    }

    [Fact]
    public void Load_UsesCaseInsensitiveLooseContent()
    {
        using TemporaryContentDirectory directory = new();
        directory.WriteFile("INI/MagicEffect.INI", "[10000]\nName=Thunder\n");

        MagicEffectFile file = MagicEffectFile.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal("Thunder", GetText(file, 1000, 0, MagicEffectTextKind.Name));
    }

    [Fact]
    public void Load_MissingFileReturnsEmptyNativeDefaults()
    {
        using TemporaryContentDirectory directory = new();

        MagicEffectFile file = MagicEffectFile.Load(new ClientContentRoot(directory.RootPath));

        Assert.True(file.GetEncoded(1000, 0, MagicEffectTextKind.Name).IsEmpty);
        Assert.True(file.GetEncoded(1000, 1, MagicEffectTextKind.Description).IsEmpty);
    }

    [Fact]
    public void Parse_RejectsOversizedContent()
    {
        Assert.Throws<InvalidDataException>(() => MagicEffectFile.Parse(new byte[1024 * 1024 + 1]));
    }

    private static MagicEffectFile Parse(string text) => MagicEffectFile.Parse(Encoding.Latin1.GetBytes(text));

    private static string GetText(MagicEffectFile file, uint type, uint level, MagicEffectTextKind kind)
    {
        return Encoding.Latin1.GetString(file.GetEncoded(type, level, kind).Span);
    }
}
