using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginOpeningArtworkTests
{
    [Fact]
    public void Opening_HasTwentySevenFullStageFrames()
    {
        for (int frame = 0; frame < LoginOpeningArtwork.FrameCount; frame++)
        {
            RgbaImage image = LoginOpeningArtwork.LoadFrame(frame);
            Assert.Equal((1024, 768), (image.Width, image.Height));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => LoginOpeningArtwork.LoadFrame(LoginOpeningArtwork.FrameCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginOpeningArtwork.LoadFrame(-1));
    }

    [Fact]
    public void Opening_ShowsTheGoldDragonsRatherThanAPlaceholder()
    {
        ReadOnlySpan<byte> rgba = LoginOpeningArtwork.LoadFrame(7).Pixels.Span;
        bool gold = false;
        for (int pixel = 0; pixel < rgba.Length && !gold; pixel += 4)
        {
            gold = rgba[pixel] > 200 && rgba[pixel + 1] > 80 && rgba[pixel + 2] < 160;
        }

        Assert.True(gold);
    }
}
