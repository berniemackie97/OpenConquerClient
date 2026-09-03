using System.Text;
using OpenConquer.Content.Configuration;

namespace OpenConquer.Content.Tests.Configuration;

public sealed class IniDocumentTests
{
    private const int MaximumLength = 4096;

    [Fact]
    public void Parse_RequiresSectionHeaderAtFirstCharacter()
    {
        IniDocument document = Parse(" [Ignored]\n" + "Key=1\n" + "[Section]\n" + "Key=2\n");

        Assert.False(document.TryGetValue("Ignored", "Key", out _));

        Assert.True(document.TryGetValue("Section", "Key", out string? value));

        Assert.Equal("2", value);
    }

    [Fact]
    public void Parse_DoesNotTrimSectionNameAndUsesFirstClosingBracket()
    {
        IniDocument document = Parse(
            "[ Section ] ignored ]\n" + "First=1\n" + "[Section] trailing text\n" + "Second=2\n"
        );

        Assert.True(document.TryGetValue(" Section ", "First", out string? firstValue));

        Assert.Equal("1", firstValue);

        Assert.False(document.TryGetValue("Section", "First", out _));

        Assert.True(document.TryGetValue("Section", "Second", out string? secondValue));

        Assert.Equal("2", secondValue);
    }

    [Fact]
    public void Parse_RejectsVerifiedLeadingCandidateCharacters()
    {
        IniDocument document = Parse(
            "[Section]\n"
                + " Leading=1\n"
                + "\tTabbed=2\n"
                + "/Slash=3\n"
                + ";Semicolon=4\n"
                + "=Equals=5\n"
                + "\\Backslash=6\n"
                + "Valid=7\n"
        );

        Assert.False(document.TryGetValue("Section", "Leading", out _));

        Assert.False(document.TryGetValue("Section", "Tabbed", out _));

        Assert.False(document.TryGetValue("Section", "Slash", out _));

        Assert.False(document.TryGetValue("Section", "Semicolon", out _));

        Assert.False(document.TryGetValue("Section", "Equals", out _));

        Assert.False(document.TryGetValue("Section", "Backslash", out _));

        Assert.True(document.TryGetValue("Section", "Valid", out string? validValue));

        Assert.Equal("7", validValue);
    }

    [Fact]
    public void Parse_DoesNotTreatHashAsACommentMarker()
    {
        IniDocument document = Parse("[Section]\n" + "#HashKey=retained\n");

        Assert.True(document.TryGetValue("Section", "#HashKey", out string? value));

        Assert.Equal("retained", value);
    }

    [Fact]
    public void Parse_TrimsOnlyTrailingSpaceAndTabFromKey()
    {
        IniDocument document = Parse("[MiXeD]\n" + "MixedKey \t= value\n");

        Assert.True(document.TryGetValue("mixed", "mixedkey", out string? value));

        Assert.Equal("value", value);
    }

    [Fact]
    public void Parse_AppliesVerifiedValueWhitespaceAndTerminators()
    {
        IniDocument document = Parse(
            "[Section]\n"
                + "Semicolon= \talpha beta   ;ignored\n"
                + "Tab=alpha beta\tignored\n"
                + "TrailingSpaces=alpha beta   \n"
                + "Empty=    ;ignored\n"
        );

        Assert.True(document.TryGetValue("Section", "Semicolon", out string? semicolonValue));

        Assert.Equal("alpha beta   ", semicolonValue);

        Assert.True(document.TryGetValue("Section", "Tab", out string? tabValue));

        Assert.Equal("alpha beta", tabValue);

        Assert.True(
            document.TryGetValue("Section", "TrailingSpaces", out string? trailingSpacesValue)
        );

        Assert.Equal("alpha beta   ", trailingSpacesValue);

        Assert.True(document.TryGetValue("Section", "Empty", out string? emptyValue));

        Assert.Equal(string.Empty, emptyValue);
    }

    [Fact]
    public void Parse_UsesFirstEqualsDelimiter()
    {
        IniDocument document = Parse("[Section]\n" + "Value=left=right\n");

        Assert.True(document.TryGetValue("Section", "Value", out string? value));

        Assert.Equal("left=right", value);
    }

    private static IniDocument Parse(string contents)
    {
        using MemoryStream stream = new(Encoding.Latin1.GetBytes(contents), writable: false);

        return IniDocument.Load(stream, "ini/test.ini", MaximumLength);
    }
}
