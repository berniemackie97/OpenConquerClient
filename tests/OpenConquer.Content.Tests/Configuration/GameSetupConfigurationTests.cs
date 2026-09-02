using OpenConquer.Content.Configuration;

namespace OpenConquer.Content.Tests.Configuration;

public sealed class GameSetupConfigurationTests
{
    [Theory]
    [InlineData(0, 800, 600)]
    [InlineData(1, 800, 600)]
    [InlineData(2, 1024, 768)]
    [InlineData(3, 1024, 768)]
    public void LoadMapsVerifiedScreenModesToLogicalResolution(
        int screenMode,
        int expectedWidthPixels,
        int expectedHeightPixels)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "ini/GameSetup.Ini",
            $"[ScreenModeRecord]\nScreenMode={screenMode}\n");

        GameSetupConfiguration configuration = GameSetupConfiguration.Load(
            new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(screenMode, configuration.ScreenMode);
        Assert.Equal(expectedWidthPixels, configuration.LogicalWidthPixels);
        Assert.Equal(expectedHeightPixels, configuration.LogicalHeightPixels);
    }

    [Fact]
    public void LoadResolvesConfigurationPathSectionAndKeyCaseInsensitively()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "INI/gamesetup.ini",
            "[screenmoderecord]\nscreenmode = 2\n");

        GameSetupConfiguration configuration = GameSetupConfiguration.Load(
            new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(2, configuration.ScreenMode);
        Assert.Equal(1024, configuration.LogicalWidthPixels);
        Assert.Equal(768, configuration.LogicalHeightPixels);
    }

    [Fact]
    public void LoadWhenGameSetupFileIsMissingThrowsFileNotFoundException()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        Assert.Throws<FileNotFoundException>(
            () => GameSetupConfiguration.Load(contentRoot));
    }

    [Fact]
    public void LoadWhenScreenModeIsMissingThrowsInvalidDataException()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "ini/GameSetup.Ini",
            "[ScreenModeRecord]\nOtherValue=2\n");

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(
            () => GameSetupConfiguration.Load(contentRoot));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("4")]
    [InlineData("invalid")]
    public void LoadWhenScreenModeIsInvalidThrowsInvalidDataException(
        string configuredValue)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "ini/GameSetup.Ini",
            $"[ScreenModeRecord]\nScreenMode={configuredValue}\n");

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(
            () => GameSetupConfiguration.Load(contentRoot));
    }
}
