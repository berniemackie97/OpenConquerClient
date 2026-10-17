using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginBirdArtworkTests
{
    [Fact]
    public void Flight_Has602FramesInsideOneAtlas()
    {
        RgbaImage atlas = LoginBirdArtwork.LoadAtlas();
        int visible = 0;

        for (int index = 0; index < LoginBirdArtwork.FrameCount; index++)
        {
            LoginBirdArtwork.BirdFrame frame = LoginBirdArtwork.FrameAt(index);
            if (!frame.IsVisible)
            {
                continue;
            }

            visible++;
            Assert.InRange(frame.AtlasX, 0, atlas.Width - frame.Width);
            Assert.InRange(frame.AtlasY, 0, atlas.Height - frame.Height);
        }

        Assert.Equal(448, visible);
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginBirdArtwork.FrameAt(LoginBirdArtwork.FrameCount));
    }
}
