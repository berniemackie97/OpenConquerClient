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
}
