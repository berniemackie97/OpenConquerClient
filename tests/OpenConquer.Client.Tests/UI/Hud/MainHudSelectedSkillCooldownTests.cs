using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudSelectedSkillCooldownTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(999, 1)]
    [InlineData(1000, 1)]
    [InlineData(1001, 2)]
    [InlineData(1999, 2)]
    [InlineData(2000, 2)]
    [InlineData(2001, 3)]
    public void GetDisplaySeconds_MatchesNativeUnsignedCeilingDivision(int remainingMilliseconds, int expectedSeconds)
    {
        uint actual = MainHudSelectedSkillCooldownRenderer.GetDisplaySeconds((uint)remainingMilliseconds);

        Assert.Equal((uint)expectedSeconds, actual);
    }

    [Theory]
    [InlineData(800, 600, 0, 0, 753, 555)]
    [InlineData(1024, 768, 0, 0, 753, 723)]
    [InlineData(800, 600, -3, 4, 750, 559)]
    [InlineData(1024, 768, 7, -5, 760, 718)]
    public void GetTextOrigin_AddsConfiguredOffsetsToSelectedControlOrigin(int width, int height, int offsetX, int offsetY, int expectedX, int expectedY)
    {
        MainHudSelectedSkillBounds bounds = MainHudSelectedSkillLayout.Create(new LogicalRenderSize(width, height)).GetBounds();

        (int x, int y) = MainHudSelectedSkillCooldownRenderer.GetTextOrigin(bounds, offsetX, offsetY);

        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
    }

    [Fact]
    public void CooldownState_PreservesProducerSnapshot()
    {
        MainHudSelectedSkillCooldownState state = new();

        Assert.Equal(0u, state.RemainingMilliseconds);

        state.SetRemainingMilliseconds(uint.MaxValue);

        Assert.Equal(uint.MaxValue, state.RemainingMilliseconds);

        state.SetRemainingMilliseconds(0);

        Assert.Equal(0u, state.RemainingMilliseconds);
    }
}
