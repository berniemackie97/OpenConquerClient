using OpenConquer.Content.Startup;
using OpenConquer.Content.Tests.Images;
using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests.Startup;

public sealed class StartupLogoTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(101, 2)]
    public void Load_SelectsVariantFromMonotonicTick(long tick, int expectedVariant)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");
        temporaryDirectory.WriteFile(
            "data/main/Logo1.bmp",
            WindowsBitmapReaderTests.CreateTwoByTwoBitmap()
        );
        temporaryDirectory.WriteFile(
            "data/main/Logo2.bmp",
            WindowsBitmapReaderTests.CreateTwoByTwoBitmap()
        );

        StartupLogo logo = StartupLogo.Load(
            new ClientContentRoot(temporaryDirectory.RootPath),
            tick
        );

        Assert.Equal(expectedVariant, logo.VariantIndex);
        Assert.Equal($"Data/Main/Logo{expectedVariant}.bmp", logo.ContentPath);
        Assert.Null(logo.UnavailableReason);
        Assert.NotNull(logo.Image);
        Assert.Equal(2, logo.Image.Width);
        Assert.Equal(2, logo.Image.Height);
    }

    [Fact]
    public void Load_WhenBitmapIsMissing_ReportsUnavailableWithoutThrowing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");

        StartupLogo logo = StartupLogo.Load(
            new ClientContentRoot(temporaryDirectory.RootPath),
            monotonicTickMilliseconds: 0
        );

        Assert.Equal(1, logo.VariantIndex);
        Assert.Equal("Data/Main/Logo1.bmp", logo.ContentPath);
        Assert.Null(logo.Image);
        Assert.Contains("was not found as a loose file", logo.UnavailableReason);
    }

    [Fact]
    public void Load_WhenBitmapCannotBeDecoded_ReportsUnavailableWithoutThrowing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");
        temporaryDirectory.WriteFile("data/main/Logo1.bmp", "not a bitmap");

        StartupLogo logo = StartupLogo.Load(
            new ClientContentRoot(temporaryDirectory.RootPath),
            monotonicTickMilliseconds: 0
        );

        Assert.Equal("Data/Main/Logo1.bmp", logo.ContentPath);
        Assert.Null(logo.Image);
        Assert.Contains("could not be loaded", logo.UnavailableReason);
    }

    [Fact]
    public void Load_WhenBackgroundFormatIsMalformed_ReportsUnavailableWithoutThrowing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%s.bmp\n");

        StartupLogo logo = StartupLogo.Load(
            new ClientContentRoot(temporaryDirectory.RootPath),
            monotonicTickMilliseconds: 0
        );

        Assert.Equal(1, logo.VariantIndex);
        Assert.Null(logo.ContentPath);
        Assert.Null(logo.Image);
        Assert.Contains("configuration could not be used", logo.UnavailableReason);
    }

    [Fact]
    public void Load_WhenResolvedPathIsUnsafe_ReportsUnavailableWithoutThrowing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=../Logo%d.bmp\n");

        StartupLogo logo = StartupLogo.Load(
            new ClientContentRoot(temporaryDirectory.RootPath),
            monotonicTickMilliseconds: 0
        );

        Assert.Equal(1, logo.VariantIndex);
        Assert.Equal("../Logo1.bmp", logo.ContentPath);
        Assert.Null(logo.Image);
        Assert.Contains("could not be loaded", logo.UnavailableReason);
    }

    [Fact]
    public void Load_DoesNotResolveTheBitmapFromAPackage()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry(
                "data/main/logo1.bmp",
                WindowsBitmapReaderTests.CreateTwoByTwoBitmap()
            )
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.True(
            source.TryOpenRead(
                "data/main/logo1.bmp",
                ContentLookupMode.PackageOnly,
                out Stream? packagedStream
            )
        );

        packagedStream.Dispose();

        StartupLogo logo = StartupLogo.Load(source, monotonicTickMilliseconds: 0);

        Assert.Null(logo.Image);
        Assert.Contains("was not found as a loose file", logo.UnavailableReason);
    }
}
