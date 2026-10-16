using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tests.Startup;

public sealed class LoginRetailSelectionArtworkTests
{
    [Theory]
    [InlineData(LoginRetailSelectionArtwork.Sequence.RegionFlame, 25)]
    [InlineData(LoginRetailSelectionArtwork.Sequence.RegionHoverBorder, 10)]
    [InlineData(LoginRetailSelectionArtwork.Sequence.ServerHover, 34)]
    [InlineData(LoginRetailSelectionArtwork.Sequence.ServerSelected, 47)]
    public void RetailAtlasesDecodeWithVerifiedBoundsAndContent(LoginRetailSelectionArtwork.Sequence sequence, int expectedCount)
    {
        LoginRetailSelectionArtwork.Sheet spec = LoginRetailSelectionArtwork.Describe(sequence);
        Assert.Equal(expectedCount, spec.Count);
        Assert.InRange(spec.Width, spec.FrameWidth, 2048);
        Assert.InRange(spec.Height, spec.FrameHeight, 1024);
        Assert.True((long)spec.Columns * spec.FrameWidth <= spec.Width);
        Assert.True((long)((spec.Count + spec.Columns - 1) / spec.Columns) * spec.FrameHeight <= spec.Height);
        var atlas = LoginRetailSelectionArtwork.Load(sequence);
        Assert.Equal(spec.Width, atlas.Width);
        Assert.Equal(spec.Height, atlas.Height);
        Assert.Equal(spec.Width * spec.Height * 4, atlas.Pixels.Length);
    }

    [Fact]
    public void RejectUnknownRetailSequences()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailSelectionArtwork.Load((LoginRetailSelectionArtwork.Sequence)999));
    }
}
