using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Rendering.Tests.Text;

public sealed class NativeTextRenderStyleTests
{
    [Fact]
    public void ValuesMatchVerifiedNativeContract()
    {
        Assert.Equal(0, (int)NativeTextRenderStyle.Normal);
        Assert.Equal(1, (int)NativeTextRenderStyle.ShadowOffset);
        Assert.Equal(2, (int)NativeTextRenderStyle.MultiOffsetOutline);
        Assert.Equal(3, (int)NativeTextRenderStyle.OffsetTrail);
        Assert.Equal(4, (int)NativeTextRenderStyle.PerCornerColor);
    }
}
