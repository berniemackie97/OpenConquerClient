namespace OpenConquer.Rendering.Tests;

public sealed class SpriteColorTests
{
    [Fact]
    public void White_IsNeutralOpaqueModulation()
    {
        Assert.Equal(new SpriteColor(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue), SpriteColor.White);
    }
}
