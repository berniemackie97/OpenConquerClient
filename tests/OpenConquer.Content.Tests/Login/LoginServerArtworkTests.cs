using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginServerArtworkTests
{
    [Theory]
    [InlineData(LoginServerArtwork.Layer.Unopened, 170, 90, 170, 90, 1)]
    [InlineData(LoginServerArtwork.Layer.Default, 170, 90, 170, 90, 1)]
    [InlineData(LoginServerArtwork.Layer.Hover, 1600, 600, 200, 120, LoginServerArtwork.HoverFrameCount)]
    [InlineData(LoginServerArtwork.Layer.Selected, 1760, 720, 220, 120, LoginServerArtwork.SelectedFrameCount)]
    [InlineData(LoginServerArtwork.Layer.Status, 72, 18, 18, 18, LoginServerArtwork.StatusStateCount)]
    public void EveryLayer_MatchesItsSheet(LoginServerArtwork.Layer layer, int width, int height, int frameWidth, int frameHeight, int frames)
    {
        LoginArtworkSheet sheet = LoginServerArtwork.Describe(layer);
        RgbaImage image = LoginServerArtwork.Load(layer);

        Assert.Equal((width, height, frameWidth, frameHeight, frames), (sheet.Width, sheet.Height, sheet.FrameWidth, sheet.FrameHeight, sheet.Count));
        Assert.Equal((width, height), (image.Width, image.Height));
    }

    [Fact]
    public void SelectedLoop_RestartsInsideTheSequence()
    {
        Assert.InRange(LoginServerArtwork.SelectedLoopStart, 1, LoginServerArtwork.SelectedFrameCount - 1);
    }
}
