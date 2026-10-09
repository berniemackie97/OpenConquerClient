using System.Text;
using OpenConquer.Content.Text;

namespace OpenConquer.Content.Tests.Text;

public sealed class ClientKeyedStringResourcesTests
{
    [Fact]
    public void Parse_PreservesNativeRequirementFormats()
    {
        ClientKeyedStringResources resources = Parse(
            "STR_MAGIC_REQ_SUBPRO_ACCORD=Requires: P%d %s\r\n" +
            "STR_MAGIC_REQ_SUBPRO_UNACCORD_STEP=Requires: P%d %s(Only P%d %s can use %s)\r\n" +
            "STR_MAGIC_SUBPRO_UNACCORD_SWITCH=Requires: P%d %s(Switch to P%d %s to use %s)\r\n");

        Assert.Equal("Requires: P%d %s", GetText(resources, "STR_MAGIC_REQ_SUBPRO_ACCORD"));
        Assert.Equal("Requires: P%d %s(Only P%d %s can use %s)", GetText(resources, "STR_MAGIC_REQ_SUBPRO_UNACCORD_STEP"));
        Assert.Equal("Requires: P%d %s(Switch to P%d %s to use %s)", GetText(resources, "STR_MAGIC_SUBPRO_UNACCORD_SWITCH"));
    }

    [Fact]
    public void Parse_PreservesEncodedBytesAndLiteralEscapes()
    {
        byte[] encoded =
        [
            (byte)'M', (byte)'o', (byte)'u', (byte)'n', (byte)'t', (byte)'=',
            0xD6, 0xD0, (byte)'=', (byte)'\\', (byte)'n',
            (byte)';', (byte)' ', (byte)'X', (byte)'\r', (byte)'\n'
        ];

        ClientKeyedStringResources resources = ClientKeyedStringResources.Parse(encoded);

        Assert.Equal(
            new byte[] { 0xD6, 0xD0, (byte)'=', (byte)'\\', (byte)'n', (byte)';', (byte)' ', (byte)'X' },
            resources.GetEncoded("Mount").ToArray());
    }

    [Fact]
    public void Parse_PreservesLeadingAndTrailingValueWhitespace()
    {
        ClientKeyedStringResources resources = Parse(
            "Leading=  value\n" +
            "Trailing=value  \r\n" +
            "Tabs=\tvalue\t\r\n");

        Assert.Equal("  value", GetText(resources, "Leading"));
        Assert.Equal("value  ", GetText(resources, "Trailing"));
        Assert.Equal("\tvalue\t", GetText(resources, "Tabs"));
    }

    [Fact]
    public void Parse_UsesCaseSensitiveKeys()
    {
        ClientKeyedStringResources resources = Parse(
            "STR_TEST=First\n" +
            "str_test=Second\n");

        Assert.Equal(2, resources.Count);
        Assert.Equal("First", GetText(resources, "STR_TEST"));
        Assert.Equal("Second", GetText(resources, "str_test"));
        Assert.False(resources.TryGetEncoded("Str_Test", out _));
    }

    [Fact]
    public void Parse_ReplacesDuplicateKeyValues()
    {
        ClientKeyedStringResources resources = Parse(
            "STR_TEST=First\n" +
            "STR_TEST=Second\n" +
            "STR_TEST=Final\n");

        Assert.Equal(1, resources.Count);
        Assert.Equal("Final", GetText(resources, "STR_TEST"));
    }

    [Fact]
    public void Parse_IgnoresLinesWithoutKeysOrEquals()
    {
        ClientKeyedStringResources resources = Parse(
            ";Ignored=Comment\n" +
            "NoSeparator\n" +
            "=NoKey\n" +
            "Valid=Retained\n");

        Assert.Equal(1, resources.Count);
        Assert.Equal("Retained", GetText(resources, "Valid"));
    }

    [Fact]
    public void Parse_DistinguishesEmptyValueFromMissingKey()
    {
        ClientKeyedStringResources resources = Parse("Empty=\n");

        Assert.True(resources.TryGetEncoded("Empty", out ReadOnlyMemory<byte> value));
        Assert.True(value.IsEmpty);
        Assert.False(resources.TryGetEncoded("Missing", out _));
        Assert.True(resources.GetEncoded("Missing").IsEmpty);
    }

    [Fact]
    public void Parse_RespectsNativeFgetsBufferBoundary()
    {
        ClientKeyedStringResources resources = Parse(
            "Long=" + new string('X', 1040) + "\n" +
            "Next=Retained\n");

        Assert.Equal(2, resources.Count);
        Assert.Equal(new string('X', 1018), GetText(resources, "Long"));
        Assert.Equal("Retained", GetText(resources, "Next"));
    }

    [Fact]
    public void Load_UsesConfiguredLanguageStringFileAndLooseContent()
    {
        using TemporaryContentDirectory directory = new();

        directory.WriteFile("INI/info.ini", "[Language]\nStringFile=ini\\cn_Res.ini\n");
        directory.WriteFile("INI/Cn_Res.INI", "STR_MAGIC_REQ_SUBPRO_ACCORD=Requires: P%d %s\n");

        ClientKeyedStringResources resources = ClientKeyedStringResources.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal(1, resources.Count);
        Assert.Equal("Requires: P%d %s", GetText(resources, "STR_MAGIC_REQ_SUBPRO_ACCORD"));
    }

    [Fact]
    public void Load_MissingConfigurationReturnsEmptyResources()
    {
        using TemporaryContentDirectory directory = new();

        ClientKeyedStringResources resources = ClientKeyedStringResources.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal(0, resources.Count);
        Assert.True(resources.GetEncoded("STR_MAGIC_REQ_SUBPRO_ACCORD").IsEmpty);
    }

    [Fact]
    public void Load_MissingStringFileSettingReturnsEmptyResources()
    {
        using TemporaryContentDirectory directory = new();

        directory.WriteFile("ini/info.ini", "[Language]\nOtherKey=value\n");

        ClientKeyedStringResources resources = ClientKeyedStringResources.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal(0, resources.Count);
    }

    [Fact]
    public void Load_MissingConfiguredResourceReturnsEmptyResources()
    {
        using TemporaryContentDirectory directory = new();

        directory.WriteFile("ini/info.ini", "[Language]\nStringFile=ini\\Missing.ini\n");

        ClientKeyedStringResources resources = ClientKeyedStringResources.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal(0, resources.Count);
    }

    [Theory]
    [InlineData("../outside.ini")]
    [InlineData("/outside.ini")]
    [InlineData("C:\\outside.ini")]
    public void Load_RejectsUnsafeConfiguredPaths(string path)
    {
        using TemporaryContentDirectory directory = new();

        directory.WriteFile("ini/info.ini", $"[Language]\nStringFile={path}\n");

        Assert.Throws<ArgumentException>(() =>
            ClientKeyedStringResources.Load(new ClientContentRoot(directory.RootPath)));
    }

    [Fact]
    public void Parse_RejectsOversizedResource()
    {
        Assert.Throws<InvalidDataException>(() =>
            ClientKeyedStringResources.Parse(new byte[1024 * 1024 + 1]));
    }

    private static ClientKeyedStringResources Parse(string text)
    {
        return ClientKeyedStringResources.Parse(Encoding.Latin1.GetBytes(text));
    }

    private static string GetText(ClientKeyedStringResources resources, string key)
    {
        return Encoding.Latin1.GetString(resources.GetEncoded(key).Span);
    }
}
