using OpenConquer.Client.UI.Hud;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudSkillExperienceStateTests
{
    [Fact]
    public void InitialState_MatchesNativeProgress42Constructor()
    {
        MainHudSkillExperienceState state = new();

        Assert.Null(state.Snapshot);
        Assert.Equal(0, state.SkillSubVariant);
    }

    [Fact]
    public void SetSnapshot_StoresTheLatestSnapshot()
    {
        MainHudSkillExperienceState state = new();
        MainHudSkillExperienceSnapshot first = new(25, 1000, 5000);
        MainHudSkillExperienceSnapshot second = new(75, 4000, 5000);

        state.SetSnapshot(first);
        state.SetSnapshot(second);

        Assert.Equal(second, state.Snapshot);
    }

    [Fact]
    public void ClearSnapshot_PreservesProgressState()
    {
        MainHudSkillExperienceState state = new();
        state.SetSnapshot(new MainHudSkillExperienceSnapshot(50, 2500, 5000));
        state.ArmSkillHighlight();
        state.AdvanceAfterHudDraw(1000);

        state.ClearSnapshot();

        Assert.Null(state.Snapshot);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1500);

        Assert.Equal(0, state.SkillSubVariant);
    }

    [Fact]
    public void ArmSkillHighlight_PreservesVariantUntilPostHudTail()
    {
        MainHudSkillExperienceState state = new();
        state.ActivateSkillSubVariant();

        state.ArmSkillHighlight();

        Assert.Equal(1, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1000);

        Assert.Equal(2, state.SkillSubVariant);
    }

    [Fact]
    public void NormalExpiration_MatchesNativeTimeline()
    {
        MainHudSkillExperienceState state = new();

        state.ArmSkillHighlight();
        state.AdvanceAfterHudDraw(1000);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1499);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1500);
        Assert.Equal(0, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1501);
        Assert.Equal(0, state.SkillSubVariant);
    }

    [Fact]
    public void VariantOne_IsLostWhenHighlightExpires()
    {
        MainHudSkillExperienceState state = new();
        state.ActivateSkillSubVariant();

        state.ArmSkillHighlight();
        state.AdvanceAfterHudDraw(1000);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1499);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1500);
        Assert.Equal(0, state.SkillSubVariant);
    }

    [Fact]
    public void VariantOneDuringRunningHighlight_IsNotReassertedToHighlightBeforeExpiration()
    {
        MainHudSkillExperienceState state = new();
        state.ArmSkillHighlight();
        state.AdvanceAfterHudDraw(1000);

        state.ActivateSkillSubVariant();
        state.AdvanceAfterHudDraw(1100);
        Assert.Equal(1, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1499);
        Assert.Equal(1, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1500);
        Assert.Equal(0, state.SkillSubVariant);
    }

    [Fact]
    public void ResetSkillSubVariant_DoesNotCancelRunningHighlightTimer()
    {
        MainHudSkillExperienceState state = new();
        state.ActivateSkillSubVariant();
        state.ArmSkillHighlight();
        state.AdvanceAfterHudDraw(1000);

        state.ResetSkillSubVariant();
        state.AdvanceAfterHudDraw(1100);
        Assert.Equal(0, state.SkillSubVariant);

        state.ActivateSkillSubVariant();
        state.AdvanceAfterHudDraw(1499);
        Assert.Equal(1, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1500);
        Assert.Equal(0, state.SkillSubVariant);
    }

    [Fact]
    public void Rearm_RestartsHighlightAfterTheCurrentShow()
    {
        MainHudSkillExperienceState state = new();
        state.ActivateSkillSubVariant();
        state.ArmSkillHighlight();
        state.AdvanceAfterHudDraw(1000);

        state.ActivateSkillSubVariant();
        state.ArmSkillHighlight();

        Assert.Equal(1, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1200);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1500);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1699);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1700);
        Assert.Equal(0, state.SkillSubVariant);
    }

    [Fact]
    public void ZeroTimestamp_RemainsTheNativeStartSentinel()
    {
        MainHudSkillExperienceState state = new();
        state.ArmSkillHighlight();

        state.AdvanceAfterHudDraw(0);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(500);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(501);
        Assert.Equal(0, state.SkillSubVariant);
    }

    [Fact]
    public void WrappedDeadline_CanExpireEarlyBeforeClockWrap()
    {
        MainHudSkillExperienceState state = new();
        state.ArmSkillHighlight();

        state.AdvanceAfterHudDraw(0xFFFF_FF00u);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(0xFFFF_FF01u);

        Assert.Equal(0, state.SkillSubVariant);
    }

    [Fact]
    public void WrappedDeadline_AfterClockWrapExpiresAtWrappedBoundary()
    {
        MainHudSkillExperienceState state = new();
        state.ArmSkillHighlight();
        state.AdvanceAfterHudDraw(0xFFFF_FF00u);

        state.AdvanceAfterHudDraw(0);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(243);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(244);
        Assert.Equal(0, state.SkillSubVariant);
    }

    [Fact]
    public void MissedDeadlineBeforeClockWrap_CanRemainActiveAfterWrap()
    {
        MainHudSkillExperienceState state = new();
        state.ArmSkillHighlight();

        state.AdvanceAfterHudDraw(0xFFFF_FC00u);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(0);
        Assert.Equal(2, state.SkillSubVariant);

        state.AdvanceAfterHudDraw(1000);
        Assert.Equal(2, state.SkillSubVariant);
    }
}
