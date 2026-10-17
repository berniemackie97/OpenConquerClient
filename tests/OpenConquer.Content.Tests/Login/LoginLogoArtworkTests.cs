using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginLogoArtworkTests
{
    [Fact]
    public void Timeline_HoldsThePreviousPictureWhereNoneWasEncoded()
    {
        int[] pictures = [.. Enumerable.Range(0, LoginLogoArtwork.FrameCount).Select(LoginLogoArtwork.PictureAt)];

        // logo.swf encodes pictures at frames 0,1,2,3,4,6,8,10,11,12,13,15,16,18,19,21,22,24.
        Assert.Equal([0, 1, 2, 3, 4, 4, 5, 5, 6, 6, 7, 8, 9, 10, 10, 11, 12, 12, 13, 14, 14, 15, 16, 16, 17], pictures);
        Assert.Equal(18, LoginLogoArtwork.PictureCount);
    }

    [Fact]
    public void EveryPicture_HasTheVideoSize()
    {
        for (int picture = 0; picture < LoginLogoArtwork.PictureCount; picture++)
        {
            RgbaImage image = LoginLogoArtwork.LoadPicture(picture);
            Assert.Equal((LoginLogoArtwork.Width, LoginLogoArtwork.Height), (image.Width, image.Height));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => LoginLogoArtwork.LoadPicture(LoginLogoArtwork.PictureCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginLogoArtwork.PictureAt(LoginLogoArtwork.FrameCount));
    }
}
