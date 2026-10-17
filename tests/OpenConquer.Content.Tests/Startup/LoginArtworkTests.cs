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
        AssertImage(artwork.AccountPanel, 508, 98, "3a1e4698639f2c4af2eace1c9b48e81d4898d279831c128bbe65e76e3eb240c2");
    }

    [Fact]
    public void AccountPanel_PreservesTransparency()
    {
        Assert.True(HasTransparency(LoginArtwork.Load().AccountPanel));
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
