using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tests.Startup;

public sealed class LoginRetailStageArtworkTests
{
    [Theory]
    [InlineData(LoginRetailStageArtwork.Layer.StageBorder, 1024, 768, 1)]
    [InlineData(LoginRetailStageArtwork.Layer.RegionPanel, 340, 434, 1)]
    [InlineData(LoginRetailStageArtwork.Layer.RegionTile, 151, 53, 1)]
    [InlineData(LoginRetailStageArtwork.Layer.RegionUnopened, 151, 53, 1)]
    [InlineData(LoginRetailStageArtwork.Layer.RegionHover, 850, 130, LoginRetailStageArtwork.RegionHoverFrames)]
    [InlineData(LoginRetailStageArtwork.Layer.RegionFlame, 740, 250, LoginRetailStageArtwork.RegionFlameFrames)]
    [InlineData(LoginRetailStageArtwork.Layer.RegionPagePrevious, 114, 34, LoginRetailStageArtwork.ButtonStates)]
    [InlineData(LoginRetailStageArtwork.Layer.RegionPageNext, 120, 34, LoginRetailStageArtwork.ButtonStates)]
    public void EveryLayerDecodesToItsVerifiedDimensions(LoginRetailStageArtwork.Layer layer, int width, int height, int frames)
    {
        LoginRetailStageArtwork.Sheet sheet = LoginRetailStageArtwork.Describe(layer);
        RgbaImage image = LoginRetailStageArtwork.Load(layer);

        Assert.Equal((width, height, frames), (sheet.Width, sheet.Height, sheet.Count));
        Assert.Equal((width, height), (image.Width, image.Height));
        Assert.True((long)sheet.Columns * sheet.FrameWidth <= sheet.Width);
        Assert.True((long)((sheet.Count + sheet.Columns - 1) / sheet.Columns) * sheet.FrameHeight <= sheet.Height);
    }

    [Fact]
    public void StageBorder_IsOpaqueAtTheBandsAndOpenOverThePanels()
    {
        RgbaImage border = LoginRetailStageArtwork.Load(LoginRetailStageArtwork.Layer.StageBorder);

        Assert.Equal(byte.MaxValue, Alpha(border, 10, 10));
        Assert.Equal(byte.MaxValue, Alpha(border, 10, 760));
        Assert.Equal(0, Alpha(border, 512, 384));
    }

    [Fact]
    public void RegionPanel_CarriesSprite183AlphaTransform()
    {
        RgbaImage panel = LoginRetailStageArtwork.Load(LoginRetailStageArtwork.Layer.RegionPanel);
        int maximum = 0;
        for (int index = 3; index < panel.Pixels.Length; index += 4)
        {
            maximum = Math.Max(maximum, panel.Pixels.Span[index]);
        }

        // Sprite 183's own fill peaks at alpha 78; the CXFORM alphaMultTerm 195 scales it to at most 59.
        Assert.InRange(maximum, 1, 78 * 195 / 256);
    }

    [Fact]
    public void RegionHover_HoldsAtFrame8AndEndsFullyTransparent()
    {
        LoginRetailStageArtwork.Sheet sheet = LoginRetailStageArtwork.Describe(LoginRetailStageArtwork.Layer.RegionHover);
        RgbaImage atlas = LoginRetailStageArtwork.Load(LoginRetailStageArtwork.Layer.RegionHover);

        // Atlas order is defaultR root frames 2-8 then 10-12; frame 8 (index 6) is the held rollover.
        (int heldX, int heldY) = Origin(sheet, 6);
        Assert.NotEqual(0, Alpha(atlas, heldX + sheet.FrameWidth / 2, heldY + sheet.FrameHeight / 2));

        // Frame 12 carries alphaMultTerm 0.
        (int lastX, int lastY) = Origin(sheet, sheet.Count - 1);
        for (int y = 0; y < sheet.FrameHeight; y++)
        {
            for (int x = 0; x < sheet.FrameWidth; x++)
            {
                Assert.Equal(0, Alpha(atlas, lastX + x, lastY + y));
            }
        }
    }

    [Fact]
    public void RejectsUnknownLayers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailStageArtwork.Load((LoginRetailStageArtwork.Layer)99));
    }

    private static (int X, int Y) Origin(LoginRetailStageArtwork.Sheet sheet, int frame) =>
        ((frame % sheet.Columns) * sheet.FrameWidth, (frame / sheet.Columns) * sheet.FrameHeight);

    private static byte Alpha(RgbaImage image, int x, int y) => image.Pixels.Span[(y * image.Width + x) * 4 + 3];
}
