using OpenConquer.Content.Startup;
using OpenConquer.Content.Tests.Images;

namespace OpenConquer.Content.Tests.Startup;

public sealed class StartupLogoTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(101, 2)]
    public void Load_SelectsRetailVariantFromMonotonicTick(long tick, int expectedVariant)
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");
        temporaryDirectory.WriteFile("data/main/Logo1.bmp", WindowsBitmapReaderTests.CreateTwoByTwoBitmap());
        temporaryDirectory.WriteFile("data/main/Logo2.bmp", WindowsBitmapReaderTests.CreateTwoByTwoBitmap());

        StartupLogo logo = StartupLogo.Load(new ClientContentRoot(temporaryDirectory.RootPath), tick);

        Assert.Equal(expectedVariant, logo.VariantIndex);
        Assert.Equal($"Data/Main/Logo{expectedVariant}.bmp", logo.ContentPath);
        Assert.Equal(2, logo.Image.Width);
        Assert.Equal(2, logo.Image.Height);
    }
}
