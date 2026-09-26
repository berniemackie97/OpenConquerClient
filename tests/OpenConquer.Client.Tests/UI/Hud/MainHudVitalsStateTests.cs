using OpenConquer.Client.UI.Hud;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudVitalsStateTests
{
    [Fact]
    public void InitialState_MatchesNativeStartupSubvariants()
    {
        MainHudVitalsState state = new();

        Assert.Null(state.Snapshot);
        Assert.Equal(1, state.LifeSubVariant);
        Assert.Equal(0, state.ManaSubVariant);
    }

    [Fact]
    public void SetSnapshot_StoresTheLatestSnapshot()
    {
        MainHudVitalsState state = new();
        MainHudVitalsSnapshot snapshot = new(90, 80, 100, 40, 30, 50, 75, 100, true);

        state.SetSnapshot(snapshot);

        Assert.Equal(snapshot, state.Snapshot);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    public void SetSnapshot_DoesNotLeaveLifeStartupSubvariantWithoutPositiveClampedMana(int currentMana, int maxMana)
    {
        MainHudVitalsState state = new();

        state.SetSnapshot(new MainHudVitalsSnapshot(90, 80, 100, currentMana, 0, maxMana, 75, 100, false));

        Assert.Equal(1, state.LifeSubVariant);
    }

    [Fact]
    public void SetSnapshot_LeavesLifeStartupSubvariantAfterPositiveClampedMana()
    {
        MainHudVitalsState state = new();

        state.SetSnapshot(new MainHudVitalsSnapshot(90, 80, 100, 1, 0, 100, 75, 100, false));

        Assert.Equal(0, state.LifeSubVariant);
    }

    [Fact]
    public void LifeStartupSubvariant_DoesNotReturnAfterTransition()
    {
        MainHudVitalsState state = new();

        state.SetSnapshot(new MainHudVitalsSnapshot(90, 80, 100, 1, 0, 100, 75, 100, false));
        state.SetSnapshot(new MainHudVitalsSnapshot(90, 80, 100, 0, 0, 100, 75, 100, false));

        Assert.Equal(0, state.LifeSubVariant);
    }

    [Fact]
    public void ClearSnapshot_PreservesHudLifetimeSubvariants()
    {
        MainHudVitalsState state = new();

        state.SetSnapshot(new MainHudVitalsSnapshot(90, 80, 100, 1, 0, 100, 75, 100, false));
        state.SetManaAlternateSubVariant(enabled: true);
        state.ClearSnapshot();

        Assert.Null(state.Snapshot);
        Assert.Equal(0, state.LifeSubVariant);
        Assert.Equal(1, state.ManaSubVariant);
    }

    [Fact]
    public void SetManaAlternateSubVariant_UpdatesTheIndependentManaState()
    {
        MainHudVitalsState state = new();

        state.SetManaAlternateSubVariant(enabled: true);
        Assert.Equal(1, state.ManaSubVariant);

        state.SetManaAlternateSubVariant(enabled: false);
        Assert.Equal(0, state.ManaSubVariant);
    }
}
