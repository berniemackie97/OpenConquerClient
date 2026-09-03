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
            "[DlgLogo]\nBgFormat=Data/Main/Startup%02d.bmp\n"
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

        Assert.Equal(StartupLogoConfiguration.DefaultBackgroundFormat, configuration.BackgroundFormat);
        Assert.Equal("Data/Main/Logo1.bmp", configuration.GetLogoPath(1));
    }

    [Fact]
    public void GetLogoPath_RejectsUnboundedFormatWidths()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%010d.bmp\n");

        StartupLogoConfiguration configuration = StartupLogoConfiguration.LoadOrDefault(
            new ClientContentRoot(temporaryDirectory.RootPath)
        );

        Assert.Throws<InvalidDataException>(() => configuration.GetLogoPath(1));
    }
}
