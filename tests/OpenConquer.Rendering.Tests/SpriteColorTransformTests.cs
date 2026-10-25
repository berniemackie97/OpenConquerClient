using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Tests;

public sealed class SpriteColorTransformTests
{
    [Fact]
    public void FromSwf_ConvertsFixedPointMultipliersAndByteOffsets()
    {
        SpriteColorTransform transform = SpriteColorTransform.FromSwf(256, 128, 0, 64, 0, 255, 51, 0);

        Assert.Equal(new SpriteColorTransform(1f, 0.5f, 0f, 0.25f, 0f, 1f, 0.2f, 0f), transform);
        Assert.True(SpriteColorTransform.FromSwf(256, 256, 256, 256, 0, 0, 0, 0).IsIdentity);
    }

    [Fact]
    public void Concatenate_AppliesTheInnerTransformFirst()
    {
        SpriteColorTransform parent = new(0.5f, 1f, 1f, 0.5f, 0.1f, 0f, 0f, 0f);
        SpriteColorTransform child = new(2f, 1f, 1f, 1f, 0.2f, 0f, 0f, 0.1f);

        SpriteColorTransform combined = parent.Concatenate(child);

        // red: (r * 2 + 0.2) * 0.5 + 0.1; alpha: (a * 1 + 0.1) * 0.5.
        Assert.Equal(1f, combined.RedMultiplier, 5);
        Assert.Equal(0.2f, combined.RedOffset, 5);
        Assert.Equal(0.5f, combined.AlphaMultiplier, 5);
        Assert.Equal(0.05f, combined.AlphaOffset, 5);
    }
}
