using OpenConquer.Content.Configuration;

namespace OpenConquer.Content.Tests.Configuration;

public sealed class GameSetupConfigurationTests
{
    [Theory]
    [InlineData(0, 800, 600)]
    [InlineData(1, 800, 600)]
    [InlineData(2, 1024, 768)]
    [InlineData(3, 1024, 768)]
    public void Load_MapsVerifiedScreenModesToLogicalResolution(
        int screenMode,
        int expectedWidthPixels,
        int expectedHeightPixels)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "ini/GameSetup.Ini",
            $"[ScreenMode]\nScreenModeRecord={screenMode}\n");

        GameSetupConfiguration configuration = GameSetupConfiguration.Load(
            new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(screenMode, configuration.ScreenMode);
        Assert.Equal(expectedWidthPixels, configuration.LogicalWidthPixels);
        Assert.Equal(expectedHeightPixels, configuration.LogicalHeightPixels);
    }

    [Fact]
    public void Load_ResolvesConfigurationPathSectionAndKeyCaseInsensitively()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "INI/gamesetup.ini",
            "[screenmode]\nscreenmoderecord = 2\n");

        GameSetupConfiguration configuration = GameSetupConfiguration.Load(
            new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(2, configuration.ScreenMode);
        Assert.Equal(1024, configuration.LogicalWidthPixels);
        Assert.Equal(768, configuration.LogicalHeightPixels);
    }

    [Fact]
    public void Load_ThrowsFileNotFoundExceptionWhenGameSetupFileIsMissing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        Assert.Throws<FileNotFoundException>(
            () => GameSetupConfiguration.Load(contentRoot));
    }

    [Fact]
    public void Load_ThrowsInvalidDataExceptionWhenScreenModeIsMissing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "ini/GameSetup.Ini",
            "[ScreenMode]\nOtherValue=2\n");

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(
            () => GameSetupConfiguration.Load(contentRoot));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("4")]
    [InlineData("invalid")]
    public void Load_ThrowsInvalidDataExceptionWhenScreenModeIsInvalid(
        string configuredValue)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "ini/GameSetup.Ini",
            $"[ScreenMode]\nScreenModeRecord={configuredValue}\n");

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(
            () => GameSetupConfiguration.Load(contentRoot));
    }
}
