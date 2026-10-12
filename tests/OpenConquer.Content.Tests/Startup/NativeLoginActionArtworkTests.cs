using System.Security.Cryptography;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tests.Startup;

public sealed class NativeLoginActionArtworkTests
{
    [Fact]
    public void EmbeddedRetailActionArtwork_MatchesVerifiedPixelHashes()
    {
        AssertImage(LoginArtwork.LoadEnterUp(), "41FDEA3A98E653CB2C991CE9B656D41AC97D3E828CE32E1F5362174A51D3E404");
        AssertImage(LoginArtwork.LoadEnterHover(), "8CA3DFFE0F2F1850EA61872E137003E54EC8C6A16D68606157AD6CBD85946B83");
        AssertImage(LoginArtwork.LoadEnterPressed(), "5056F3F7098A2D935AF7FAF550CD3CFF1CE5A4957832935C5FC2CDCA5DB212A8");
        AssertImage(LoginArtwork.LoadExitUp(), "57121511C04031B7CDE18EFC93A23728ADC19BAE695280F4D6A9678DE6283730");
        AssertImage(LoginArtwork.LoadExitHover(), "FE32A09DB879DDA1B73E8C5012CA8FAB5AA45B9FA845CCF4A03508660D89D794");
        AssertImage(LoginArtwork.LoadExitPressed(), "37442D367079C3F6B1AD3E6C9E8FB7EABC615F10B10491AD631E4B9518A3BD1A");
    }

    [Fact]
    public void CombinedLoginArtworkSet_HasAllActionStates()
    {
        LoginArtworkSet artwork = LoginArtwork.Load();
        Assert.NotNull(artwork.EnterUp);
        Assert.NotNull(artwork.EnterHover);
        Assert.NotNull(artwork.EnterPressed);
        Assert.NotNull(artwork.ExitUp);
        Assert.NotNull(artwork.ExitHover);
        Assert.NotNull(artwork.ExitPressed);
    }

    private static void AssertImage(RgbaImage image, string sha256)
    {
        Assert.Equal(132, image.Width);
        Assert.Equal(40, image.Height);
        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(image.Pixels.Span)));
    }
}
