using OpenConquer.Client.UI.Hud.StatusHints;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints.Magic;

public sealed class MainHudMagicHintSelectionTests
{
    [Fact]
    public void SelectMagic_PreservesFullDwordTypeAndNativeStoredAnchor()
    {
        MainHudStatusHintState state = new();

        state.SetMagicAnchor(90, 557);
        state.SelectMagic(65537);

        Assert.Equal(9, state.Category);
        Assert.Equal(65537u, state.MagicType);
        Assert.Equal(90, state.MagicAnchorX);
        Assert.Equal(599, state.MagicStoredAnchorY);
        Assert.True(state.CanRenderMagic);
        Assert.False(state.CanRender);
    }

    [Fact]
    public void Clear_HidesWithoutErasingCategoryTypeOrAnchor()
    {
        MainHudStatusHintState state = new();

        state.SetMagicAnchor(450, 725);
        state.SelectMagic(uint.MaxValue);
        state.Clear();

        Assert.False(state.IsVisible);
        Assert.False(state.CanRenderMagic);
        Assert.Equal(9, state.Category);
        Assert.Equal(uint.MaxValue, state.MagicType);
        Assert.Equal(450, state.MagicAnchorX);
        Assert.Equal(767, state.MagicStoredAnchorY);
    }

    [Fact]
    public void SelectMagic_AllowsZeroTypeForConditionalNativeBranches()
    {
        MainHudStatusHintState state = new();

        state.SetMagicAnchor(90, 557);
        state.SelectMagic(0);

        Assert.True(state.CanRenderMagic);
        Assert.Equal(0u, state.MagicType);
    }

    [Fact]
    public void SkillDragSuppressesBothCategoriesWithoutDeletingSelection()
    {
        MainHudStatusHintState state = new();

        state.SetMagicAnchor(90, 557);
        state.SelectMagic(1000);
        state.SetSkillDragActive(true);

        Assert.False(state.CanRenderMagic);
        Assert.True(state.IsVisible);

        state.SetSkillDragActive(false);

        Assert.True(state.CanRenderMagic);
        Assert.Equal(1000u, state.MagicType);
    }

    [Fact]
    public void CategoryEightRemainsIndependentOfMagicType()
    {
        MainHudStatusHintState state = new();

        state.SetMagicAnchor(90, 557);
        state.SelectMagic(1000);
        state.Select((int)MainHudStatusHintKind.WalkRun);

        Assert.Equal(MainHudStatusHintDefinition.Category, state.Category);
        Assert.Equal(MainHudStatusHintKind.WalkRun, state.Kind);
        Assert.True(state.CanRender);
        Assert.False(state.CanRenderMagic);
        Assert.Equal(1000u, state.MagicType);
    }

    [Fact]
    public void CategoryEightNoneHotspotDoesNotRender()
    {
        MainHudStatusHintState state = new();

        state.Select(0);

        Assert.Equal(MainHudStatusHintKind.None, state.Kind);
        Assert.True(state.IsVisible);
        Assert.False(state.CanRender);
        Assert.False(state.CanRenderMagic);
    }

    [Fact]
    public void NativeAnchorClampsSignedLowWordXToZero()
    {
        MainHudStatusHintState state = new();

        state.SetMagicAnchor(-10, 557);
        state.SelectMagic(1000);

        Assert.Equal(0, state.MagicAnchorX);
    }
}
