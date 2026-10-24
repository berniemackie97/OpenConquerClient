using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginErrorArtworkTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeldPicturesVerifyTheirEmbeddedHashesAndRetailPosition(bool quit)
    {
        RgbaImage image = LoginErrorArtwork.Load(quit);
        Assert.Equal((296, 192), (image.Width, image.Height));
        Assert.Equal((360, 259), LoginErrorArtwork.Position);
        Assert.Equal(296 * 192 * 4, image.Pixels.Length);
    }

    [Fact]
    public void ErrorAndQuitAreDifferentRetailSymbols()
    {
        Assert.False(LoginErrorArtwork.Load(false).Pixels.Span.SequenceEqual(LoginErrorArtwork.Load(true).Pixels.Span));
    }
    [Theory]
    [InlineData(false, LoginDialogButton.Ok)]
    [InlineData(true, LoginDialogButton.Ok)]
    [InlineData(true, LoginDialogButton.Cancel)]
    public void CompleteButtonStatesHaveVerifiedDistinctArtwork(bool quit, LoginDialogButton button)
    {
        RgbaImage up = LoginErrorArtwork.Load(quit, button, LoginButtonState.Up);
        RgbaImage over = LoginErrorArtwork.Load(quit, button, LoginButtonState.Over);
        RgbaImage down = LoginErrorArtwork.Load(quit, button, LoginButtonState.Down);
        Assert.Equal((296, 192), (over.Width, over.Height));
        Assert.Equal(up.Pixels.Length, down.Pixels.Length);
        Assert.False(up.Pixels.Span.SequenceEqual(over.Pixels.Span));
        Assert.False(over.Pixels.Span.SequenceEqual(down.Pixels.Span));
    }

}
