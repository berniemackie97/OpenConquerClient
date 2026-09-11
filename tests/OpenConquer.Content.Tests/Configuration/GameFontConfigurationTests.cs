using OpenConquer.Content.Configuration;
using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests.Configuration;

public sealed class GameFontConfigurationTests
{
    [Fact]
    public void Load_ReadsVerifiedRetailConfiguration()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12");

        GameFontConfiguration configuration = GameFontConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal("Arial", configuration.FaceName);
        Assert.Equal(12, configuration.NominalPixelHeight);
    }

    [Theory]
    [InlineData("Courier New 19", "Courier New", 19)]
    [InlineData("Courier  New 19", "Courier  New", 19)]
    [InlineData("$simsun.ttf 12", "$simsun.ttf", 12)]
    public void Load_UsesLastAsciiSpaceAndPreservesFaceToken(string contents, string expectedFaceName, int expectedNominalPixelHeight)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, contents);

        GameFontConfiguration configuration = GameFontConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(expectedFaceName, configuration.FaceName);
        Assert.Equal(expectedNominalPixelHeight, configuration.NominalPixelHeight);
    }

    [Theory]
    [InlineData("Arial invalid")]
    [InlineData("Arial 0")]
    [InlineData("Arial ")]
    public void Load_UsesDefaultNominalPixelHeightWhenConfiguredHeightIsInvalid(string contents)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, contents);

        GameFontConfiguration configuration = GameFontConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal("Arial", configuration.FaceName);
        Assert.Equal(GameFontConfiguration.DefaultNominalPixelHeight, configuration.NominalPixelHeight);
    }

    [Fact]
    public void Load_PreservesNegativeNonZeroNominalPixelHeight()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial -1");

        GameFontConfiguration configuration = GameFontConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal("Arial", configuration.FaceName);
        Assert.Equal(-1, configuration.NominalPixelHeight);
    }

    [Fact]
    public void Load_StopsAtEmbeddedNullByte()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, "Arial 12\0Courier New 19"u8);

        GameFontConfiguration configuration = GameFontConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal("Arial", configuration.FaceName);
        Assert.Equal(12, configuration.NominalPixelHeight);
    }

    [Fact]
    public void Load_ResolvesConfigurationPathCaseInsensitively()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("INI/fOnT.InI", "Arial 12");

        GameFontConfiguration configuration = GameFontConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal("Arial", configuration.FaceName);
        Assert.Equal(12, configuration.NominalPixelHeight);
    }

    [Fact]
    public void Load_DoesNotFallBackToPackagedContent()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "ini.wdf\n");
        temporaryDirectory.WriteFile("ini.wdf", WdfTestArchiveBuilder.CreateSingleEntry(GameFontConfiguration.RelativePath, "Arial 12"u8));

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Throws<FileNotFoundException>(() => GameFontConfiguration.Load(contentSource));
    }

    [Fact]
    public void Load_ThrowsFileNotFoundExceptionWhenFontFileIsMissing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<FileNotFoundException>(() => GameFontConfiguration.Load(contentSource));
    }

    [Fact]
    public void Load_ThrowsInvalidDataExceptionWhenFontFileExceedsMaximumLength()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(GameFontConfiguration.RelativePath, new byte[257]);

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(() => GameFontConfiguration.Load(contentSource));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Arial")]
    [InlineData(" 12")]
    [InlineData("Arial\t12")]
    public void Parse_ThrowsInvalidDataExceptionWhenFaceHeightDelimiterIsMissingOrMalformed(string contents)
    {
        byte[] bytes = System.Text.Encoding.Latin1.GetBytes(contents);

        Assert.Throws<InvalidDataException>(() => GameFontConfiguration.Parse(bytes));
    }
}
