using OpenConquer.Client.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldRoleFrameClockTests
{
    [Fact]
    public void AdvancesInitialIdleAtRecordedFrameInterval()
    {
        WorldRoleFrameClock clock = new();
        Assert.Equal(0, clock.GetFrame(5000, 3));
        Assert.Equal(0, clock.GetFrame(5065, 3));
        Assert.Equal(1, clock.GetFrame(5066, 3));
        Assert.Equal(2, clock.GetFrame(5132, 3));
        Assert.Equal(0, clock.GetFrame(5198, 3));
    }

    [Fact]
    public void HandlesTickWraparoundAndMissingMotion()
    {
        WorldRoleFrameClock clock = new();
        Assert.Equal(0, clock.GetFrame(uint.MaxValue - 5, 0));
        Assert.Equal(0, clock.GetFrame(uint.MaxValue - 5, 4));
        Assert.Equal(1, clock.GetFrame(60, 4));
    }

    [Theory]
    [InlineData(2u, 2_000_110UL, (ushort)110)]
    [InlineData(1002u, 2_000_111UL, (ushort)111)]
    [InlineData(3u, 3_000_110UL, (ushort)110)]
    [InlineData(2u, 2_000_120UL, (ushort)120)]
    [InlineData(1002u, 2_000_121UL, (ushort)121)]
    public void GroundMotionKeyUsesTheBodySourceAndAction(uint appearance, ulong expected, ushort action)
    {
        Assert.Equal(expected, WorldRoleAssets.BodyMotionKey(appearance, action));
    }

    [Theory]
    [InlineData(2u, 2_000_100UL)]
    [InlineData(1002u, 2_000_100UL)]
    public void InitialBodyKeyUsesBodySourceRatherThanPackedAppearance(uint appearance, ulong expected)
    {
        Assert.Equal(expected, WorldRoleAssets.InitialBodyMotionKey(appearance));
    }
}
