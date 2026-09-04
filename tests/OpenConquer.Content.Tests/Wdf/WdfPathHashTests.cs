using OpenConquer.Content.Wdf;

namespace OpenConquer.Content.Tests.Wdf;

public sealed class WdfPathHashTests
{
    [Theory]
    [InlineData("c3/0003/560/130.C3", 0x048AEF45u)]
    [InlineData("c3/0003/611/130.C3", 0x35302C75u)]
    [InlineData("c3/0003/741/130.C3", 0xDE9758F1u)]
    [InlineData("c3/0003/500/130.C3", 0xEE52708Eu)]
    [InlineData("c3/0003/410/130.C3", 0x050B1174u)]
    public void Compute_MatchesVerifiedNativeVector(string contentPath, uint expected)
    {
        Assert.Equal(expected, WdfPathHash.Compute(contentPath));
    }

    [Fact]
    public void Compute_NormalizesAsciiCaseAndDirectorySeparators()
    {
        const uint expected = 0x048AEF45;

        Assert.Equal(expected, WdfPathHash.Compute("c3/0003/560/130.C3"));

        Assert.Equal(expected, WdfPathHash.Compute(@"C3\0003\560\130.c3"));
    }
}
