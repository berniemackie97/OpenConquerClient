using OpenConquer.Launcher.Play;

namespace OpenConquer.Launcher.Tests.Play;

public sealed class GameModeTests
{
    [Theory]
    [InlineData(0, 0, 0, (int)GameMode.Standard)]
    [InlineData(1, 0, 0, (int)GameMode.High)]
    [InlineData(0, 1, 0, (int)GameMode.Standard)]
    [InlineData(0, 0, 1, (int)GameMode.Low)]
    [InlineData(2, 2, 1, (int)GameMode.Standard)]
    [InlineData(3, 2, 3, (int)GameMode.High)]
    [InlineData(1, 2, 3, (int)GameMode.Low)]
    [InlineData(5, 2, 4, (int)GameMode.High)]
    [InlineData(1, 4, 4, (int)GameMode.Standard)]
    public void MostUsed_FollowsCPlayDlgOnInitDialog(int high, int standard, int low, int expected) =>
        Assert.Equal((GameMode)expected, new GameModeUsage(high, standard, low).MostUsed);

    [Fact]
    public void Record_CountsEachModeAndSaturates()
    {
        GameModeUsage usage = new GameModeUsage(0, 0, 0).Record(GameMode.High).Record(GameMode.Low).Record(GameMode.Low);

        Assert.Equal(new GameModeUsage(1, 0, 2), usage);
        Assert.Equal(int.MaxValue, new GameModeUsage(0, int.MaxValue, 0).Record(GameMode.Standard).StandardTimes);
        Assert.Throws<ArgumentOutOfRangeException>(() => usage.Record((GameMode)3));
    }

    [Fact]
    public void LowConfirmation_IsAskedUntilLowHasBeenUsed()
    {
        Assert.True(new GameModeUsage(4, 4, 0).LowNeedsConfirmation);
        Assert.False(new GameModeUsage(0, 0, 1).LowNeedsConfirmation);
    }
}
