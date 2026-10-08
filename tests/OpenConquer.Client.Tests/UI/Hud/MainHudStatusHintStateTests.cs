using OpenConquer.Client.UI.Hud;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudStatusHintStateTests
{
    [Fact]
    public void Select_PreservesNativeZeroHotspotWithoutRenderingText()
    {
        MainHudStatusHintState state = new();

        state.Select(0);

        Assert.True(state.IsVisible);
        Assert.Equal(MainHudStatusHintKind.None, state.Kind);
        Assert.False(state.CanRender);
    }

    [Fact]
    public void Select_RecognizesAllEightNativeSubKinds()
    {
        for (int index = 1; index <= 8; index++)
        {
            MainHudStatusHintState state = new();
            state.Select(index);

            Assert.Equal(index, state.HoveredHotspot);
            Assert.Equal((MainHudStatusHintKind)index, state.Kind);
            Assert.True(state.CanRender);
        }
    }

    [Fact]
    public void SkillDrag_SuppressesPanelWithoutClearingSelection()
    {
        MainHudStatusHintState state = new();
        state.Select(8);
        state.SetSkillDragActive(true);

        Assert.True(state.IsVisible);
        Assert.False(state.CanRender);

        state.SetSkillDragActive(false);

        Assert.True(state.CanRender);
        Assert.Equal(MainHudStatusHintKind.Life, state.Kind);
    }

    [Fact]
    public void Clear_ResetsNativeHoverAndVisibilityState()
    {
        MainHudStatusHintState state = new();
        state.Select(6);
        state.Clear();

        Assert.Equal(MainHudStatusHintState.NoHoveredHotspot, state.HoveredHotspot);
        Assert.Equal(MainHudStatusHintKind.None, state.Kind);
        Assert.False(state.IsVisible);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(255)]
    public void Select_RejectsUnknownHotspotIndexes(int index)
    {
        MainHudStatusHintState state = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.Select(index));
    }
}
