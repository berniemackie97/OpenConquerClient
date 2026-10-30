using System.Text;
using OpenConquer.Client.UI.Role;

namespace OpenConquer.Client.Tests.UI.Role;

public sealed class RoleNameValidationTests
{
    [Theory]
    [InlineData("Abcd", RoleNameValidationResult.Valid)]
    [InlineData("ab=cd", RoleNameValidationResult.Valid)]
    [InlineData("ab.cd", RoleNameValidationResult.Valid)]
    [InlineData(".abcd", RoleNameValidationResult.InvalidGlyph)]
    [InlineData("abcd.", RoleNameValidationResult.InvalidGlyph)]
    [InlineData("abc", RoleNameValidationResult.InvalidLength)]
    [InlineData("abcdefghijklmnop", RoleNameValidationResult.InvalidLength)]
    [InlineData("abcd😀", RoleNameValidationResult.InvalidGlyph)]
    public void ValidationUsesRetailPixelGateAndEncodedByteLimits(string name, RoleNameValidationResult expected)
    {
        RoleNameValidationResult result = RoleNameValidation.Validate(name,
            static rune => rune.Value != '.' && rune.Value != ' ', out byte[] encoded);

        Assert.Equal(expected, result);
        if (expected == RoleNameValidationResult.Valid)
        {
            Assert.Equal(CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetBytes(name), encoded);
        }
    }

    [Fact]
    public void RejectsOrdinaryCharactersWhenGlyphInkIsInsufficient()
    {
        RoleNameValidationResult result = RoleNameValidation.Validate("ABcd",
            static rune => rune.Value != 'B', out _);

        Assert.Equal(RoleNameValidationResult.InvalidGlyph, result);
    }
}
