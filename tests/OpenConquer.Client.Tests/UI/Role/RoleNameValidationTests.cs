using System.Text;
using OpenConquer.Client.UI.Role;

namespace OpenConquer.Client.Tests.UI.Role;

public sealed class RoleNameValidationTests
{
    [Theory]
    [InlineData("Abcd")]
    [InlineData("ab=cd")]
    [InlineData("ab.cd")]
    [InlineData("Åbcd")]
    public void ValidNamesUseNativeGlyphRuleAndWindows1252Encoding(string name)
    {
        RoleNameValidationResult result = RoleNameValidation.Validate(name,
            static rune => rune.Value != '.' && rune.Value != ' ', out byte[] encoded);

        Assert.Equal(RoleNameValidationResult.Valid, result);
        Assert.Equal(CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetBytes(name), encoded);
    }

    [Theory]
    [InlineData(".abcd")]
    [InlineData("abcd.")]
    [InlineData("abcd😀")]
    public void NonRenderingOrNonEncodableCharactersAreRejected(string name)
    {
        RoleNameValidationResult result = RoleNameValidation.Validate(name,
            static rune => rune.Value != '.' && rune.Value != ' ', out _);

        Assert.Equal(RoleNameValidationResult.InvalidGlyph, result);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcdefghijklmnop")]
    public void EncodedLengthMustBeFourThroughFifteenBytes(string name)
    {
        RoleNameValidationResult result = RoleNameValidation.Validate(name,
            static _ => true, out _);

        Assert.Equal(RoleNameValidationResult.InvalidLength, result);
    }

    [Fact]
    public void RejectsOrdinaryCharactersWhenGlyphInkIsInsufficient()
    {
        RoleNameValidationResult result = RoleNameValidation.Validate("ABcd",
            static rune => rune.Value != 'B', out _);

        Assert.Equal(RoleNameValidationResult.InvalidGlyph, result);
    }
}
