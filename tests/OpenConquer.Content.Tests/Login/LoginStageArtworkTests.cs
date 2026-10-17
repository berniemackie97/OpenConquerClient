using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginStageArtworkTests
{
    [Theory]
    [InlineData(LoginStageArtwork.Layer.Backdrop, 1024, 637)]
    [InlineData(LoginStageArtwork.Layer.Border, 1024, 768)]
    [InlineData(LoginStageArtwork.Layer.AccountPanel, 508, 98)]
    [InlineData(LoginStageArtwork.Layer.RegionPanel, 340, 434)]
    public void EveryLayer_DecodesToItsVerifiedDimensions(LoginStageArtwork.Layer layer, int width, int height)
    {
        RgbaImage image = LoginStageArtwork.Load(layer);

        Assert.Equal((width, height), (image.Width, image.Height));
    }

    [Fact]
    public void Border_IsOpaqueAcrossItsBandsAndOpenOverThePanels()
    {
        RgbaImage border = LoginStageArtwork.Load(LoginStageArtwork.Layer.Border);

        Assert.Equal(byte.MaxValue, Alpha(border, 10, 10));
        Assert.Equal(byte.MaxValue, Alpha(border, 10, 760));
        Assert.Equal(0, Alpha(border, 512, 384));
    }

    [Fact]
    public void RegionPanel_CarriesSprite183AlphaTransform()
    {
        RgbaImage panel = LoginStageArtwork.Load(LoginStageArtwork.Layer.RegionPanel);
        int maximum = 0;
        for (int index = 3; index < panel.Pixels.Length; index += 4)
        {
            maximum = Math.Max(maximum, panel.Pixels.Span[index]);
        }

        // Sprite 183's own fill peaks at alpha 78; the panel's alphaMultTerm 195 scales it to at most 59.
        Assert.InRange(maximum, 1, 78 * 195 / 256);
    }

    [Fact]
    public void AccountPanel_KeepsItsTransparentSurround()
    {
        Assert.Equal(0, Alpha(LoginStageArtwork.Load(LoginStageArtwork.Layer.AccountPanel), 0, 0));
    }

    [Fact]
    public void UnknownLayers_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginStageArtwork.Load((LoginStageArtwork.Layer)99));
    }

    private static byte Alpha(RgbaImage image, int x, int y) => image.Pixels.Span[(y * image.Width + x) * 4 + 3];
}
