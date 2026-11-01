using OpenConquer.Client.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldPuzzleAnimationClockTests
{
    [Fact]
    public void FrameAdvancesOnlyAfterDrawAndAtMostOncePerEligibleRender()
    {
        WorldPuzzleAnimationClock clock = new(1000);

        Assert.Equal(0u, clock.FrameForRender(200));
        Assert.Equal(0u, clock.FrameForRender(1199));
        Assert.Equal(0u, clock.FrameForRender(1200));
        Assert.Equal(1u, clock.FrameForRender(11999));
        Assert.Equal(2u, clock.FrameForRender(12000));
    }

    [Fact]
    public void ClockUsesUnsignedNativeTimeDeltaAcrossWrap()
    {
        WorldPuzzleAnimationClock clock = new(16);
        Assert.Equal(0u, clock.FrameForRender(uint.MaxValue - 8));
        Assert.Equal(0u, clock.FrameForRender(4));
        Assert.Equal(0u, clock.FrameForRender(7));
        Assert.Equal(1u, clock.FrameForRender(8));
    }

    [Fact]
    public void FirstRenderAtZero_AdvancesOnItsFirstElapsedInterval()
    {
        WorldPuzzleAnimationClock clock = new(1000);

        Assert.Equal(0u, clock.FrameForRender(0));
        Assert.Equal(0u, clock.FrameForRender(999));
        Assert.Equal(0u, clock.FrameForRender(1000));
        Assert.Equal(1u, clock.FrameForRender(1001));
    }

    [Fact]
    public void IntervalMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldPuzzleAnimationClock(0));
    }
}
