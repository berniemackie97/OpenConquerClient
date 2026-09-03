using OpenConquer.Content.Configuration;

namespace OpenConquer.Content.Tests.Configuration;

public sealed class StartupLogoConfigurationTests
{
    [Fact]
    public void LoadOrDefault_ReadsDeclaredBackgroundFormat()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "INI/INFO.INI",
            "[DlgLogo]\n" + "BgFormat=Data/Main/Startup%02d.bmp\n"
        );

        StartupLogoConfiguration configuration = StartupLogoConfiguration.LoadOrDefault(
            new ClientContentRoot(temporaryDirectory.RootPath)
        );

        Assert.Equal("Data/Main/Startup%02d.bmp", configuration.BackgroundFormat);

        Assert.Equal("Data/Main/Startup01.bmp", configuration.GetLogoPath(1));

        Assert.Equal("Data/Main/Startup02.bmp", configuration.GetLogoPath(2));
    }

    [Fact]
    public void LoadOrDefault_UsesVerifiedDefaultWhenInfoIsMissing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        StartupLogoConfiguration configuration = StartupLogoConfiguration.LoadOrDefault(
            new ClientContentRoot(temporaryDirectory.RootPath)
        );

        Assert.Equal(
            StartupLogoConfiguration.DefaultBackgroundFormat,
            configuration.BackgroundFormat
        );

        Assert.Equal("Data/Main/Logo1.bmp", configuration.GetLogoPath(1));
    }

    [Fact]
    public void LoadOrDefault_UsesVerifiedDefaultForEmptyValue()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\n" + "BgFormat=    ;comment\n");

        StartupLogoConfiguration configuration = StartupLogoConfiguration.LoadOrDefault(
            new ClientContentRoot(temporaryDirectory.RootPath)
        );

        Assert.Equal(
            StartupLogoConfiguration.DefaultBackgroundFormat,
            configuration.BackgroundFormat
        );
    }

    [Fact]
    public void LoadOrDefault_PreservesOrdinaryTrailingValueSpaces()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "ini/info.ini",
            "[DlgLogo]\n" + "BgFormat=  Data/Main/Logo%d.bmp   ;comment\n"
        );

        StartupLogoConfiguration configuration = StartupLogoConfiguration.LoadOrDefault(
            new ClientContentRoot(temporaryDirectory.RootPath)
        );

        Assert.Equal("Data/Main/Logo%d.bmp   ", configuration.BackgroundFormat);

        Assert.Equal("Data/Main/Logo1.bmp   ", configuration.GetLogoPath(1));
    }

    [Fact]
    public void GetLogoPath_AppliesSpacePaddedWidth()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "ini/info.ini",
            "[DlgLogo]\n" + "BgFormat=Data/Main/Logo%2d.bmp\n"
        );

        StartupLogoConfiguration configuration = StartupLogoConfiguration.LoadOrDefault(
            new ClientContentRoot(temporaryDirectory.RootPath)
        );

        Assert.Equal("Data/Main/Logo 1.bmp", configuration.GetLogoPath(1));

        Assert.Equal("Data/Main/Logo 2.bmp", configuration.GetLogoPath(2));
    }

    [Fact]
    public void GetLogoPath_RejectsUnboundedFormatWidths()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(
            "ini/info.ini",
            "[DlgLogo]\n" + "BgFormat=Data/Main/Logo%010d.bmp\n"
        );

        StartupLogoConfiguration configuration = StartupLogoConfiguration.LoadOrDefault(
            new ClientContentRoot(temporaryDirectory.RootPath)
        );

        Assert.Throws<InvalidDataException>(() => configuration.GetLogoPath(1));
    }
}
