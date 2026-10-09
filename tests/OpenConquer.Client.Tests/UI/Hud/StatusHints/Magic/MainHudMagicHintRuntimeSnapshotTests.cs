using OpenConquer.Client.UI.Hud.StatusHints.Magic;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints.Magic;

public sealed class MainHudMagicHintRuntimeSnapshotTests
{
    [Fact]
    public void TryFindLearnedMagic_UsesFirstFullDwordTypeMatch()
    {
        MainHudMagicHintRuntimeSnapshot snapshot = CreateSnapshot(
        [
            new MainHudLearnedMagic(65537, 1, 100),
            new MainHudLearnedMagic(1, 2, 200),
            new MainHudLearnedMagic(65537, 3, 300),
        ]);

        Assert.True(snapshot.TryFindLearnedMagic(65537, out MainHudLearnedMagic first));
        Assert.Equal(1u, first.Level);
        Assert.Equal(100u, first.CurrentExperience);

        Assert.True(snapshot.TryFindLearnedMagic(1, out MainHudLearnedMagic second));
        Assert.Equal(2u, second.Level);

        Assert.False(snapshot.TryFindLearnedMagic(65538, out _));
    }

    [Theory]
    [InlineData(0L, false)]
    [InlineData(100000L, true)]
    [InlineData(900000L, true)]
    [InlineData(-100000L, false)]
    public void HasLearnedSubprofession_UsesPositivePackedClassDigit(long packed, bool expected)
    {
        MainHudMagicHintRuntimeSnapshot snapshot = CreateSnapshot([], packedPhases: packed);

        Assert.Equal(expected, snapshot.HasLearnedSubprofession(6));
        Assert.False(snapshot.HasLearnedSubprofession(0));
        Assert.False(snapshot.HasLearnedSubprofession(19));
    }

    [Fact]
    public void StateStore_UsesAnExplicitPublishedSnapshotWithoutFabricatedDefaults()
    {
        MainHudMagicHintStateStore store = new();

        Assert.Null(store.Capture());

        MainHudMagicHintRuntimeSnapshot snapshot = CreateSnapshot(
            [new MainHudLearnedMagic(1000, 0, 50)]);

        store.Publish(snapshot);
        Assert.Same(snapshot, store.Capture());

        store.Clear();
        Assert.Null(store.Capture());
    }

    [Fact]
    public void RuntimeSnapshot_CopiesLearnedMagicVector()
    {
        MainHudLearnedMagic[] source = [new(1000, 0, 50)];
        MainHudMagicHintRuntimeSnapshot snapshot = CreateSnapshot(source);

        source[0] = new MainHudLearnedMagic(2000, 0, 100);

        Assert.True(snapshot.TryFindLearnedMagic(1000, out MainHudLearnedMagic original));
        Assert.Equal(50u, original.CurrentExperience);
        Assert.False(snapshot.TryFindLearnedMagic(2000, out _));
    }

    private static MainHudMagicHintRuntimeSnapshot CreateSnapshot(IEnumerable<MainHudLearnedMagic> learned, long packedPhases = 0)
    {
        return new MainHudMagicHintRuntimeSnapshot(learned, 130, 0, 0, packedPhases, null, default);
    }
}
