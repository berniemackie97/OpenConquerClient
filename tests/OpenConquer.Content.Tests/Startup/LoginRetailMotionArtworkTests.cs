using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tests.Startup;

public sealed class LoginRetailMotionArtworkTests
{
    [Fact]
    public void OpeningSequenceIncludesVideoAndTheThreeOriginalTransitionFrames()
    {
        Assert.Equal(27, LoginRetailMotionArtwork.OpeningFrameCount);
        Assert.Equal(30, LoginRetailMotionArtwork.FramesPerSecond);
        for (int frame = 0; frame < LoginRetailMotionArtwork.OpeningFrameCount; frame++)
        {
            var image = LoginRetailMotionArtwork.LoadOpening(frame);
            Assert.Equal(1024, image.Width);
            Assert.Equal(768, image.Height);
            Assert.Equal(1024 * 768 * 4, image.Pixels.Length);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailMotionArtwork.LoadOpening(27));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailMotionArtwork.LoadOpening(-1));
    }

    [Fact]
    public void BirdAtlasContainsAll602FramesWithBoundedSourceRegions()
    {
        var atlas = LoginRetailMotionArtwork.LoadBirdAtlas();
        Assert.Equal(1024, atlas.Width);
        Assert.Equal(1024, atlas.Height);
        int visible = 0;
        for (int frame = 0; frame < LoginRetailMotionArtwork.BirdFrameCount; frame++)
        {
            var glyph = LoginRetailMotionArtwork.BirdAt(frame);
            if (!glyph.IsVisible)
            {
                continue;
            }

            visible++;
            Assert.True(glyph.Width > 0 && glyph.Height > 0);
            Assert.InRange(glyph.AtlasX, 0, 1024 - glyph.Width);
            Assert.InRange(glyph.AtlasY, 0, 1024 - glyph.Height);
            Assert.InRange(glyph.StageX, -1024, 2048);
            Assert.InRange(glyph.StageY, -1024, 2048);
        }
        Assert.Equal(448, visible);
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailMotionArtwork.BirdAt(602));
    }
}
