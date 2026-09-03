namespace OpenConquer.Content.Tool.Tests;

public sealed class ContentPathTests
{
    [Theory]
    [InlineData("ini/package.ini")]
    [InlineData("data/main/Logo1.bmp")]
    [InlineData("version.dat")]
    [InlineData("data/interface/Style01/Log/Log2BG.dds")]
    [InlineData("data/map/mapobj/family/obj/Family01.dds")]
    public void Validate_AcceptsWindowsEraRelativePaths(string sourcePath)
    {
        ContentPath.Validate(sourcePath);
    }

    /// <summary>
    /// The rejection set is fixed rather than platform-derived, so these must fail identically on
    /// Windows and Unix.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/ini/package.ini")]
    [InlineData("ini\\package.ini")]
    [InlineData("../package.ini")]
    [InlineData("ini/../../package.ini")]
    [InlineData("ini/./package.ini")]
    [InlineData("ini//package.ini")]
    [InlineData("C:/ini/package.ini")]
    [InlineData("ini/pack:age.ini")]
    [InlineData("ini/pack*age.ini")]
    [InlineData("ini/pack?age.ini")]
    [InlineData("ini/pack|age.ini")]
    [InlineData("ini/pack\"age.ini")]
    [InlineData("ini/pack<age>.ini")]
    [InlineData("ini/package.ini.")]
    [InlineData("ini/package.ini ")]
    public void Validate_RejectsPathsThatCouldEscapeOrBreakAHost(string sourcePath)
    {
        Assert.Throws<InvalidDataException>(() => ContentPath.Validate(sourcePath));
    }

    [Fact]
    public void Validate_RejectsAPathBeyondTheLengthBudget()
    {
        Assert.Throws<InvalidDataException>(() => ContentPath.Validate($"ini/{new string('a', 300)}.ini"));
    }

    [Fact]
    public void Validate_RejectsAnEmbeddedNullCharacter()
    {
        Assert.Throws<InvalidDataException>(() => ContentPath.Validate("ini/pack\0age.ini"));
    }

    [Fact]
    public void ToKey_FoldsCaseInvariantly()
    {
        Assert.Equal("data/main/logo1.bmp", ContentPath.ToKey("Data/Main/Logo1.bmp"));
    }

    [Fact]
    public void ToHostRelativePath_UsesThePlatformSeparator()
    {
        Assert.Equal(
            Path.Combine("ini", "package.ini"),
            ContentPath.ToHostRelativePath("ini/package.ini")
        );
    }

    [Fact]
    public void ToHostRelativePath_ValidatesBeforeConverting()
    {
        Assert.Throws<InvalidDataException>(() => ContentPath.ToHostRelativePath("../escape.ini"));
    }
}
