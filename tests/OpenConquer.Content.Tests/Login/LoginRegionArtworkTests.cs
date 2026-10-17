using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginRegionArtworkTests
{
    [Theory]
    [InlineData(LoginRegionArtwork.Layer.Tile, 151, 53, 1, 0, -1)]
    [InlineData(LoginRegionArtwork.Layer.Unopened, 151, 53, 1, 0, 0)]
    [InlineData(LoginRegionArtwork.Layer.Hover, 850, 130, LoginRegionArtwork.HoverFrameCount, -10, -6)]
    [InlineData(LoginRegionArtwork.Layer.Flame, 740, 250, LoginRegionArtwork.FlameFrameCount, 2, 0)]
    public void EveryLayer_MatchesItsSheetAndOffset(LoginRegionArtwork.Layer layer, int width, int height, int frames, int offsetX, int offsetY)
    {
        LoginArtworkSheet sheet = LoginRegionArtwork.Describe(layer);
        RgbaImage image = LoginRegionArtwork.Load(layer);

        Assert.Equal((width, height, frames), (sheet.Width, sheet.Height, sheet.Count));
        Assert.Equal((width, height), (image.Width, image.Height));
        Assert.Equal((offsetX, offsetY), LoginRegionArtwork.Offset(layer));
        Assert.True(sheet.FrameX(frames - 1) + sheet.FrameWidth <= sheet.Width);
        Assert.True(sheet.FrameY(frames - 1) + sheet.FrameHeight <= sheet.Height);
    }

    [Fact]
    public void Hover_HoldsAtTimelineFrame8AndEndsFullyTransparent()
    {
        LoginArtworkSheet sheet = LoginRegionArtwork.Describe(LoginRegionArtwork.Layer.Hover);
        RgbaImage atlas = LoginRegionArtwork.Load(LoginRegionArtwork.Layer.Hover);

        // The sheet holds timeline frames 2-8 then 10-12; frame 8 (index 6) is the held rollover.
        Assert.NotEqual(0, Alpha(atlas, sheet.FrameX(6) + sheet.FrameWidth / 2, sheet.FrameY(6) + sheet.FrameHeight / 2));

        // Frame 12 carries alphaMultTerm 0.
        int last = sheet.Count - 1;
        for (int y = 0; y < sheet.FrameHeight; y++)
        {
            for (int x = 0; x < sheet.FrameWidth; x++)
            {
                Assert.Equal(0, Alpha(atlas, sheet.FrameX(last) + x, sheet.FrameY(last) + y));
            }
        }
    }

    [Fact]
    public void SheetFrames_OutsideTheSheetAreRejected()
    {
        LoginArtworkSheet sheet = LoginRegionArtwork.Describe(LoginRegionArtwork.Layer.Flame);

        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.FrameX(sheet.Count));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.FrameY(-1));
    }

    private static byte Alpha(RgbaImage image, int x, int y) => image.Pixels.Span[(y * image.Width + x) * 4 + 3];
}
