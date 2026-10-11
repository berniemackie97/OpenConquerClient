using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tests.Startup;

public sealed class LoginArtworkTests
{
    [Fact]
    public void EmbeddedBackdrop_DecodesIntoExpectedLogicalReferenceImage()
    {
        RgbaImage image = LoginArtwork.LoadBackdrop();

        Assert.Equal(LoginArtwork.ReferenceWidth, image.Width);
        Assert.Equal(LoginArtwork.ReferenceHeight, image.Height);
        Assert.Equal(LoginArtwork.ReferenceWidth * LoginArtwork.ReferenceHeight * 4, image.Pixels.Length);
        Assert.Equal(byte.MaxValue, image.Pixels.Span[3]);
        Assert.Equal(byte.MaxValue, image.Pixels.Span[^1]);
    }

    [Fact]
    public void EmbeddedBackdrop_HasDistinctUpperAndLowerRegions()
    {
        RgbaImage image = LoginArtwork.LoadBackdrop();
        ReadOnlySpan<byte> pixels = image.Pixels.Span;
        int upper = (100 * image.Width + 100) * 4;
        int lower = (740 * image.Width + 100) * 4;

        Assert.NotEqual(pixels[upper], pixels[lower]);
    }
}
