using OpenConquer.Launcher.Play;

namespace OpenConquer.Launcher.Tests.Play;

public sealed class PlayArtworkTests
{
    public static TheoryData<string, int, int> Images => new()
    {
        { PlayArtwork.Main.Path, 800, 600 },
        { PlayArtwork.Side.Path, 225, 600 },
        { PlayArtwork.VersionLabel.Path, 67, 21 },
        { PlayArtwork.Logo.Path, 198, 106 },
        { PlayArtwork.Hint.Path, 397, 207 },
    };

    [Fact]
    public void EveryImage_DecodesToItsDeclaredSizeAndHash()
    {
        List<PlayImage> images = [PlayArtwork.Main, PlayArtwork.Side, PlayArtwork.VersionLabel, PlayArtwork.Logo, PlayArtwork.Hint, .. PlayArtwork.CursorFrames];
        foreach (PlayButton button in Enum.GetValues<PlayButton>())
        {
            foreach (PlayButtonState state in Enum.GetValues<PlayButtonState>())
            {
                images.Add(PlayArtwork.Button(button, state));
                if (button <= PlayButton.Low)
                {
                    images.Add(PlayArtwork.Button(button, state, faded: true));
                }
            }
        }

        Assert.Equal(43, images.Distinct().Count());
        foreach (PlayImage image in images)
        {
            Assert.Equal(image.Width * image.Height * 4, PlayArtwork.Load(image).Length);
        }
    }

    [Theory]
    [MemberData(nameof(Images))]
    public void Pictures_KeepTheirRetailSize(string path, int width, int height)
    {
        PlayImage image = new[] { PlayArtwork.Main, PlayArtwork.Side, PlayArtwork.VersionLabel, PlayArtwork.Logo, PlayArtwork.Hint }.Single(candidate => candidate.Path == path);

        Assert.Equal((width, height), (image.Width, image.Height));
    }

    [Theory]
    [InlineData((int)PlayButton.High, (int)PlayButtonState.Down, false, "Play/Buttons/HighDown.rgba.br")]
    [InlineData((int)PlayButton.Standard, (int)PlayButtonState.Over, true, "Play/Buttons/StandardOverFaded.rgba.br")]
    [InlineData((int)PlayButton.Low, (int)PlayButtonState.Up, true, "Play/Buttons/LowUpFaded.rgba.br")]
    [InlineData((int)PlayButton.Continue, (int)PlayButtonState.Up, false, "Play/Buttons/ContinueUp.rgba.br")]
    [InlineData((int)PlayButton.Register, (int)PlayButtonState.Down, false, "Play/Buttons/RegisterDown.rgba.br")]
    public void Button_SelectsTheStateArtwork(int button, int state, bool faded, string path) =>
        Assert.Equal(path, PlayArtwork.Button((PlayButton)button, (PlayButtonState)state, faded).Path);

    [Fact]
    public void FadedArtwork_ExistsOnlyForTheStartButtons()
    {
        Assert.Throws<ArgumentException>(() => PlayArtwork.Button(PlayButton.Exit, PlayButtonState.Up, faded: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayArtwork.Button((PlayButton)99, PlayButtonState.Up));
    }

    [Fact]
    public void MainArt_IsTransparentExactlyOutsideMainRgn()
    {
        byte[] pixels = PlayArtwork.Load(PlayArtwork.Main);
        int opaque = 0;
        for (int index = 3; index < pixels.Length; index += 4)
        {
            Assert.True(pixels[index] is 0 or 255);
            opaque += pixels[index] == 255 ? 1 : 0;
        }

        // Play/Main.rgn: 299 rectangles covering 463,815 pixels.
        Assert.Equal(463_815, opaque);
    }
}
