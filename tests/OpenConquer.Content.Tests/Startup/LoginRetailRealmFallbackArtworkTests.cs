using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tests.Startup;

public sealed class LoginRetailRealmFallbackArtworkTests
{
    [Fact]
    public void EmptySlotUsesOriginalUnopenedSMovieFrame()
    {
        var image = LoginRetailRealmFallbackArtwork.LoadUnopened();
        Assert.Equal(170, image.Width);
        Assert.Equal(90, image.Height);
        Assert.Equal(170 * 90 * 4, image.Pixels.Length);
    }

    [Fact]
    public void PopulatedSlotWithoutNamedPictureUsesOriginalDefaultSMovieFrame()
    {
        var image = LoginRetailRealmFallbackArtwork.LoadDefault();
        Assert.Equal(170, image.Width);
        Assert.Equal(90, image.Height);
        Assert.Equal(170 * 90 * 4, image.Pixels.Length);
        Assert.Equal(156 * 56 * 4, image.Pixels.Length);
    }
}
