using OpenConquer.Content.Configuration;
using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests.Configuration;

public sealed class ClientFontSizeConfigurationTests
{
    [Fact]
    public void Load_ReadsVerifiedRetailNormalFontHeight()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(ClientFontSizeConfiguration.RelativePath,
            """
            [FontSize]
            Size=12
            Width=6
            """);

        ClientFontSizeConfiguration configuration = ClientFontSizeConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(12, configuration.NormalFontHeightPixels);
    }

    [Fact]
    public void Load_WhenConfigurationIsMissing_UsesNativeDefault()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        ClientFontSizeConfiguration configuration = ClientFontSizeConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(ClientFontSizeConfiguration.DefaultNormalFontHeightPixels, configuration.NormalFontHeightPixels);
    }

    [Theory]
    [InlineData(
        """
        [OtherSection]
        Size=12
        """)]
    [InlineData(
        """
        [FontSize]
        Width=6
        """)]
    [InlineData(
        """
        [FontSize]
        Size=invalid
        """)]
    public void Load_WhenNormalFontHeightIsUnavailableOrInvalid_UsesNativeDefault(string contents)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(ClientFontSizeConfiguration.RelativePath, contents);

        ClientFontSizeConfiguration configuration = ClientFontSizeConfiguration.Load(new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(ClientFontSizeConfiguration.DefaultNormalFontHeightPixels, configuration.NormalFontHeightPixels);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("-1", -1)]
    [InlineData("+19", 19)]
    [InlineData(" 20 ", 20)]
    public void Load_PreservesValidIntegerValues(string value, int expected)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(ClientFontSizeConfiguration.RelativePath,
            $"""
            [FontSize]
            Size={value}
            """);

        ClientFontSizeConfiguration configuration = ClientFontSizeConfiguration.Load(
            new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(expected, configuration.NormalFontHeightPixels);
    }

    [Fact]
    public void Load_ResolvesPathSectionAndKeyCaseInsensitively()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("INI/InFo.InI",
            """
            [fOnTsIzE]
            sIzE=15
            """);

        ClientFontSizeConfiguration configuration = ClientFontSizeConfiguration.Load(
            new ClientContentRoot(temporaryDirectory.RootPath));

        Assert.Equal(15, configuration.NormalFontHeightPixels);
    }

    [Fact]
    public void Load_DoesNotFallBackToPackagedContent()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "ini.wdf\n");
        temporaryDirectory.WriteFile("ini.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry(ClientFontSizeConfiguration.RelativePath,
                """
                [FontSize]
                Size=19
                """u8));

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath);

        using (contentSource.OpenRequiredRead(ClientFontSizeConfiguration.RelativePath, ContentLookupMode.PackageOnly))
        {
        }

        ClientFontSizeConfiguration configuration = ClientFontSizeConfiguration.Load(contentSource);

        Assert.Equal(ClientFontSizeConfiguration.DefaultNormalFontHeightPixels, configuration.NormalFontHeightPixels);
    }

    [Fact]
    public void Load_RejectsConfigurationBeyondSafetyLimit()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(ClientFontSizeConfiguration.RelativePath, new byte[(64 * 1024) + 1]);

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(() => ClientFontSizeConfiguration.Load(contentSource));
    }
}
