using System.Security.Cryptography;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tests.Startup;

public sealed class LoginNativeSelectionArtworkTests
{
    [Fact]
    public void BundledSelectionImages_ExactlyMatchDecodedClean5517Pixels()
    {
        LoginArtworkSet artwork = LoginArtwork.Load();
        AssertImage(artwork.SelectedRegion, 150, 50, "a45876d402185d925ee78e0b91037078a45e6e35a7115511d650715fd256661d");
        AssertImage(artwork.SelectedServerGlow, 200, 100, "380ae2e578206b0eff23cacfd87dd37b8294b3f7ed1212eb78cba9d8b92fc9a5");
        Assert.True(artwork.SelectedServerGlow.Pixels.Span.IndexOf((byte)0) >= 0);
    }

    private static void AssertImage(RgbaImage image, int width, int height, string hash)
    {
        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        Assert.Equal(width * height * 4, image.Pixels.Length);
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(image.Pixels.Span)).ToLowerInvariant());
    }
}
