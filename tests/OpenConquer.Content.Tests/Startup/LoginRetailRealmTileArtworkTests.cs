using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tests.Startup;

public sealed class LoginRetailRealmTileArtworkTests
{
    [Fact]
    public void IdleFrameUsesOriginalSourceCropAndVerifiedPixels()
    {
        var tile = LoginRetailRealmTileArtwork.Load();
        Assert.Equal(155, tile.Width);
        Assert.Equal(59, tile.Height);
        Assert.Equal(155 * 59 * 4, tile.Pixels.Length);
    }

    [Fact]
    public void NativeStatusLightsHaveExactlyFourUnmodifiedEighteenPixelFrames()
    {
        LoginRetailSelectionArtwork.Sheet sheet = LoginRetailSelectionArtwork.Describe(LoginRetailSelectionArtwork.Sequence.RealmStatus);
        Assert.Equal(18, sheet.FrameWidth);
        Assert.Equal(18, sheet.FrameHeight);
        Assert.Equal(4, sheet.Count);
        Assert.Equal(4 * 18, sheet.Width);
        Assert.Equal(18, sheet.Height);
        var pixels = LoginRetailSelectionArtwork.Load(LoginRetailSelectionArtwork.Sequence.RealmStatus);
        Assert.Equal(sheet.Width * sheet.Height * 4, pixels.Pixels.Length);
    }
}
