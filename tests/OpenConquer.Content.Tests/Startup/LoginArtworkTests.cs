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

    [Fact]
    public void EmbeddedTopDragon_MatchesReferenceDimensionsAndPixels()
    {
        RgbaImage image = LoginArtwork.LoadTopDragon();
        Assert.Equal(494, image.Width);
        Assert.Equal(140, image.Height);
        Assert.Equal("b1e80e39a4a91d952510e826c9e8c0f0e0f83a353c6f546f322c970f3875d6b1", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.Pixels.Span)).ToLowerInvariant());
        Assert.Contains(image.Pixels.Span.ToArray().Chunk(4), static pixel => pixel[3] == 0);
        Assert.Contains(image.Pixels.Span.ToArray().Chunk(4), static pixel => pixel[3] > 0);
    }
}
