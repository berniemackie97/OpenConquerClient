using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginButtonArtworkTests
{
    [Theory]
    [InlineData(LoginButton.Register, 82, 21)]
    [InlineData(LoginButton.Purchase, 109, 21)]
    [InlineData(LoginButton.Help, 57, 23)]
    [InlineData(LoginButton.Accounts, 17, 15)]
    [InlineData(LoginButton.Save, 22, 19)]
    [InlineData(LoginButton.Clear, 20, 21)]
    [InlineData(LoginButton.Enter, 132, 40)]
    [InlineData(LoginButton.Exit, 132, 40)]
    [InlineData(LoginButton.RegionPagePrevious, 38, 34)]
    [InlineData(LoginButton.RegionPageNext, 40, 34)]
    public void EveryButton_HasThreeDistinctStatesOfOneSize(LoginButton button, int width, int height)
    {
        RgbaImage up = LoginButtonArtwork.Load(button, LoginButtonState.Up);
        RgbaImage over = LoginButtonArtwork.Load(button, LoginButtonState.Over);
        RgbaImage down = LoginButtonArtwork.Load(button, LoginButtonState.Down);

        Assert.All([up, over, down], image => Assert.Equal((width, height), (image.Width, image.Height)));
        Assert.False(up.Pixels.Span.SequenceEqual(over.Pixels.Span));
        Assert.False(over.Pixels.Span.SequenceEqual(down.Pixels.Span));
    }

    [Fact]
    public void UnknownButtonsAndStates_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginButtonArtwork.Load((LoginButton)99, LoginButtonState.Up));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginButtonArtwork.Load(LoginButton.Enter, (LoginButtonState)9));
    }
}
