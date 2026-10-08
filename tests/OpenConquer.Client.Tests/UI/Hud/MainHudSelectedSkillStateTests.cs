using OpenConquer.Client.UI.Hud.SelectedSkill;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudSelectedSkillStateTests
{
    [Fact]
    public void Constructor_MatchesNativePostInitClearState()
    {
        MainHudSelectedSkillState state = new();

        Assert.Equal("Magic0", state.SectionName);
        Assert.Equal(0u, state.ContentId);
        Assert.Equal(0u, state.BlockedCover);
        Assert.Equal(0, state.CoverFlag);
        Assert.False(state.IsImageActive);
        Assert.False(state.IsCovered);
        Assert.False(state.IsInputBlocked);
    }

    [Fact]
    public void SetSectionAndContent_MatchesNativePreLookupMutation()
    {
        MainHudSelectedSkillState state = new();
        state.SetCoverFlag(1);

        state.SetSectionAndContent("MagicSkillType1000", 1000, 7);

        Assert.True(state.IsImageActive);
        Assert.Equal("MagicSkillType1000", state.SectionName);
        Assert.Equal(1000u, state.ContentId);
        Assert.Equal(7u, state.BlockedCover);
        Assert.Equal(1, state.CoverFlag);
        Assert.True(state.IsCovered);
        Assert.True(state.IsInputBlocked);
    }

    [Fact]
    public void ReplacingSection_DoesNotDependOnPreviousImageResolution()
    {
        MainHudSelectedSkillState state = new();

        state.SetSectionAndContent("MagicSkillType1000", 1000, 0);
        state.SetSectionAndContent("MissingSection", 2000, 0);

        Assert.True(state.IsImageActive);
        Assert.Equal("MissingSection", state.SectionName);
        Assert.Equal(2000u, state.ContentId);
    }

    [Fact]
    public void ClearLoadedImage_ClearsOnlyNativeSelectedInstanceFields()
    {
        MainHudSelectedSkillState state = new();

        state.SetCoverFlag(1);
        state.SetSectionAndContent("XpSkillType2000", 2000, 9);
        state.ClearLoadedImage();

        Assert.False(state.IsImageActive);
        Assert.Equal("XpSkillType2000", state.SectionName);
        Assert.Equal(0u, state.ContentId);
        Assert.Equal(0u, state.BlockedCover);
        Assert.Equal(1, state.CoverFlag);
        Assert.True(state.IsCovered);
        Assert.False(state.IsInputBlocked);
    }

    [Fact]
    public void CoverFlag_IsIndependentFromLoadedAndBlockedState()
    {
        MainHudSelectedSkillState state = new();

        state.SetCoverFlag(2);

        Assert.True(state.IsCovered);

        state.SetCoverFlag(0);

        Assert.False(state.IsCovered);

        state.SetSectionAndContent("MagicSkillType1000", 1000, 1);

        Assert.True(state.IsCovered);

        state.SetCoverFlag(0);

        Assert.True(state.IsCovered);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetSectionAndContent_RejectsMissingManagedSectionName(string? sectionName)
    {
        MainHudSelectedSkillState state = new();

        Assert.ThrowsAny<ArgumentException>(() => state.SetSectionAndContent(sectionName!, 1, 0));
    }
}
