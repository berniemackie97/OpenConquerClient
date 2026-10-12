using System.Security.Cryptography;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tests.Startup;

public sealed class LoginArtworkTests
{
    [Fact]
    public void EmbeddedArtwork_PreservesRetailSourceDimensionsAndPixelHashes()
    {
        LoginArtworkSet artwork = LoginArtwork.Load();

        AssertImage(artwork.Backdrop, 1024, 637, "e33bf0450c7a386aa28d6d90a1f71a8b7d8b64e1e4ec92fdfad48d7f3880a2b9");
        AssertImage(artwork.Header, 1024, 116, "80fb919c05a73751f7418e26b0849f43a8a4b949dc9355f98b56aff04c68ce12");
        AssertImage(artwork.Footer, 1024, 140, "25b59064c644823574410e2277d1fbc67bc26965224e79e97de38975f98a981c");
        AssertImage(artwork.AccountPanel, 508, 98, "3a1e4698639f2c4af2eace1c9b48e81d4898d279831c128bbe65e76e3eb240c2");
        AssertImage(artwork.TopDragon, 494, 140, "b1e80e39a4a91d952510e826c9e8c0f0e0f83a353c6f546f322c970f3875d6b1");
        AssertImage(artwork.RegionCell, 154, 54, "45015f25884557fe7a279bc9f84f9632c24fb8db11b0cdbb4f9bf98d9b95559f");
        AssertImage(artwork.ServerCell, 158, 58, "b748e63779e1d35283bbc465a2ee111537dacb0a991f345fbb7bc144370cb48c");
    }

    [Fact]
    public void ReusedRegionAndServerCells_PreserveStraightAlpha()
    {
        LoginArtworkSet artwork = LoginArtwork.Load();

        Assert.True(HasTransparency(artwork.RegionCell));
        Assert.True(HasTransparency(artwork.ServerCell));
    }

    [Fact]
    public void HeaderFooterAndAccountPanel_PreserveTransparency()
    {
        LoginArtworkSet artwork = LoginArtwork.Load();

        Assert.True(HasTransparency(artwork.Header));
        Assert.True(HasTransparency(artwork.Footer));
        Assert.True(HasTransparency(artwork.AccountPanel));
    }

    private static bool HasTransparency(RgbaImage image)
    {
        for (int index = 3; index < image.Pixels.Length; index += 4)
        {
            if (image.Pixels.Span[index] != byte.MaxValue)
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertImage(RgbaImage image, int width, int height, string expectedSha256)
    {
        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        Assert.Equal(width * height * 4, image.Pixels.Length);
        Assert.Equal(expectedSha256, Convert.ToHexString(SHA256.HashData(image.Pixels.Span)).ToLowerInvariant());
    }
}
