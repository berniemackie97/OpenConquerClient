using OpenConquer.Client.UI.Login.Stage;

namespace OpenConquer.Client.Tests.UI.Login.Stage;

public sealed class LoginStageClockTests
{
    [Fact]
    public void FirstAdvance_OnlyStartsTheClock()
    {
        LoginStageClock clock = new();

        Assert.Equal(0, clock.Advance(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, clock.Frame);
        Assert.Equal(TimeSpan.Zero, clock.Step);
    }

    [Fact]
    public void Advance_CountsWholeThirtiethsAndCarriesTheRemainder()
    {
        LoginStageClock clock = new();
        clock.Advance(TimeSpan.Zero);

        Assert.Equal(0, clock.Advance(TimeSpan.FromMilliseconds(20)));
        Assert.Equal(1, clock.Advance(TimeSpan.FromMilliseconds(20)));
        Assert.Equal(1, clock.Advance(TimeSpan.FromMilliseconds(30)));
        Assert.Equal(2, clock.Frame);
    }

    [Fact]
    public void Stalls_CatchUpAtMostAQuarterSecond()
    {
        LoginStageClock clock = new();
        clock.Advance(TimeSpan.Zero);

        Assert.Equal(7, clock.Advance(TimeSpan.FromSeconds(10)));
        Assert.Equal(TimeSpan.FromMilliseconds(250), clock.Step);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromTicks(-1)));
    }
}
