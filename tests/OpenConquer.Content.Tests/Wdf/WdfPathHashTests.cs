using OpenConquer.Content.Wdf;

namespace OpenConquer.Content.Tests.Wdf;

public sealed class WdfPathHashTests
{
    [Fact]
    public void Compute_MatchesKnownRetailVectorAndNormalizesPath()
    {
        const uint expected = 0x048AEF45;

        Assert.Equal(expected, WdfPathHash.Compute("c3/0003/560/130.C3"));
        Assert.Equal(expected, WdfPathHash.Compute(@"C3\0003\560\130.c3"));
    }
}
