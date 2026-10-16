using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tests.Startup;

public sealed class LoginRetailFlashArtworkTests
{
    [Fact]
    public void OriginalLogoSequenceHasTwentyFiveValidatedFrames()
    {
        for (int index = 0; index < LoginRetailFlashArtwork.LogoFrameCount; index++)
        {
            var image = LoginRetailFlashArtwork.LoadLogoFrame(index);
            Assert.Equal(248, image.Width);
            Assert.Equal(78, image.Height);
            Assert.Equal(checked(image.Width * image.Height * 4), image.Pixels.Length);
        }
    }

    [Fact]
    public void OriginalButtonsHaveThreeIndependentVisibleStates()
    {
        int count = 0;
        foreach (LoginRetailFlashArtwork.Button button in Enum.GetValues<LoginRetailFlashArtwork.Button>())
            foreach (LoginRetailFlashArtwork.State state in Enum.GetValues<LoginRetailFlashArtwork.State>())
            {
                var image = LoginRetailFlashArtwork.LoadButton(button, state);
                Assert.True(image.Width > 0 && image.Height > 0);
                Assert.Equal(checked(image.Width * image.Height * 4), image.Pixels.Length);
                count++;
            }
        Assert.Equal(18, count);
    }

    [Fact]
    public void InvalidIndicesFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailFlashArtwork.LoadLogoFrame(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailFlashArtwork.LoadLogoFrame(25));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailFlashArtwork.LoadButton((LoginRetailFlashArtwork.Button)10, LoginRetailFlashArtwork.State.Up));
    }
}
