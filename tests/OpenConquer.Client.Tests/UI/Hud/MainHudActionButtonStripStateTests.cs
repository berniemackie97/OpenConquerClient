using OpenConquer.Client.UI.Hud.ActionButtons;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudActionButtonStripStateTests
{
    [Fact]
    public void Constructor_CreatesTenIndependentEnabledButtons()
    {
        MainHudActionButtonStripState state = new();
        HashSet<MainHudActionButtonState> buttons = [];

        foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
        {
            MainHudActionButtonState button = state.GetButton(definition.Id);

            Assert.True(button.IsEnabled);
            Assert.Equal(MainHudActionButtonState.NormalFrame, button.CurrentFrame);
            Assert.True(buttons.Add(button));
        }

        Assert.Equal(10, buttons.Count);
        Assert.Equal(MainHudPkButtonSkin.Button47, state.PkSkin);
        Assert.False(state.IsPkBlinkActive);
        Assert.False(state.IsOrganiseBlinkActive);
    }

    [Fact]
    public void GetButton_RejectsUnknownNativeControlId()
    {
        MainHudActionButtonStripState state = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetButton((MainHudActionButtonId)0x7FFF));
    }

    [Fact]
    public void ApplyPkMode_UsesVerifiedNativeSkinMapping()
    {
        MainHudActionButtonStripState state = new();

        state.ApplyPkMode(1);
        Assert.Equal(MainHudPkButtonSkin.Button49, state.PkSkin);

        state.ApplyPkMode(2);
        Assert.Equal(MainHudPkButtonSkin.Button48, state.PkSkin);

        state.ApplyPkMode(3);
        Assert.Equal(MainHudPkButtonSkin.Button412, state.PkSkin);

        state.ApplyPkMode(0);
        Assert.Equal(MainHudPkButtonSkin.Button47, state.PkSkin);
    }

    [Fact]
    public void ApplyPkMode_OnlyModeZeroArmsBlinkAndOtherModesPreserveExistingGate()
    {
        MainHudActionButtonStripState state = new();

        state.ApplyPkMode(1);
        Assert.False(state.IsPkBlinkActive);

        state.ApplyPkMode(0);
        Assert.True(state.IsPkBlinkActive);

        state.ApplyPkMode(2);
        Assert.True(state.IsPkBlinkActive);
        Assert.Equal(MainHudPkButtonSkin.Button48, state.PkSkin);
    }

    [Fact]
    public void ApplyPkMode_InvalidModePreservesSkinAndBlinkGate()
    {
        MainHudActionButtonStripState state = new();
        state.ApplyPkMode(0);
        state.ApplyPkMode(3);

        state.ApplyPkMode(99);

        Assert.Equal(MainHudPkButtonSkin.Button412, state.PkSkin);
        Assert.True(state.IsPkBlinkActive);
    }

    [Fact]
    public void AdvancePkBeforeDraw_WhenInactiveDoesNotReadClock()
    {
        MainHudActionButtonStripState state = new();

        state.AdvancePkBeforeDraw(() => throw new InvalidOperationException("Clock must not be read."));

        Assert.Equal(MainHudActionButtonState.NormalFrame, state.GetButton(MainHudActionButtonId.Button47).CurrentFrame);
    }

    [Theory]
    [InlineData(499, 0, true)]
    [InlineData(500, 1, true)]
    [InlineData(999, 1, true)]
    [InlineData(1000, 0, true)]
    [InlineData(29999, 1, true)]
    [InlineData(30000, 0, true)]
    [InlineData(30001, 0, false)]
    public void AdvancePkBeforeDraw_MatchesNativeStableClockTiming(uint elapsed, int expectedFrame, bool expectedActive)
    {
        MainHudActionButtonStripState state = new();
        state.ApplyPkMode(0);

        TickSequence clock = new(100, 100, 100);
        state.AdvancePkBeforeDraw(clock.Read);

        clock = new TickSequence(unchecked(100u + elapsed), unchecked(100u + elapsed));
        state.AdvancePkBeforeDraw(clock.Read);

        Assert.Equal(expectedFrame, state.GetButton(MainHudActionButtonId.Button47).CurrentFrame);
        Assert.Equal(expectedActive, state.IsPkBlinkActive);
    }

    [Fact]
    public void AdvancePkBeforeDraw_UsesIndependentPhaseAndExpiryReads()
    {
        MainHudActionButtonStripState state = new();
        state.ApplyPkMode(0);

        state.AdvancePkBeforeDraw(new TickSequence(100, 100, 100).Read);
        state.AdvancePkBeforeDraw(new TickSequence(30099, 30101).Read);

        Assert.False(state.IsPkBlinkActive);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.GetButton(MainHudActionButtonId.Button47).CurrentFrame);
    }

    [Fact]
    public void AdvancePkBeforeDraw_ResamplesZeroStartSentinel()
    {
        MainHudActionButtonStripState state = new();
        state.ApplyPkMode(0);

        state.AdvancePkBeforeDraw(new TickSequence(0, 123, 623, 623).Read);

        Assert.True(state.IsPkBlinkActive);
        Assert.Equal(MainHudActionButtonState.PressedFrame, state.GetButton(MainHudActionButtonId.Button47).CurrentFrame);
    }

    [Fact]
    public void AdvancePkBeforeDraw_UsesUnsignedDwordElapsedAcrossWrap()
    {
        MainHudActionButtonStripState state = new();
        state.ApplyPkMode(0);

        state.AdvancePkBeforeDraw(new TickSequence(0xFFFFFFF0, 0xFFFFFFF0, 0xFFFFFFF0).Read);
        state.AdvancePkBeforeDraw(new TickSequence(0x000001E4, 0x000001E4).Read);

        Assert.True(state.IsPkBlinkActive);
        Assert.Equal(MainHudActionButtonState.PressedFrame, state.GetButton(MainHudActionButtonId.Button47).CurrentFrame);
    }

    [Fact]
    public void ApplyPkModeZero_DuringActiveBlinkDoesNotRestartEpoch()
    {
        MainHudActionButtonStripState state = new();
        state.ApplyPkMode(0);

        state.AdvancePkBeforeDraw(new TickSequence(100, 100, 100).Read);
        state.ApplyPkMode(0);
        state.AdvancePkBeforeDraw(new TickSequence(600, 600).Read);

        Assert.Equal(MainHudActionButtonState.PressedFrame, state.GetButton(MainHudActionButtonId.Button47).CurrentFrame);
    }

    [Fact]
    public void AdvancePkBeforeDraw_RearmAfterExpiryUsesNativeZeroStartPath()
    {
        MainHudActionButtonStripState state = new();
        state.ApplyPkMode(0);

        state.AdvancePkBeforeDraw(new TickSequence(100, 100, 100).Read);
        state.AdvancePkBeforeDraw(new TickSequence(30101, 30101).Read);
        Assert.False(state.IsPkBlinkActive);

        state.ApplyPkMode(0);
        state.AdvancePkBeforeDraw(new TickSequence(500, 1000, 1000).Read);

        Assert.True(state.IsPkBlinkActive);
        Assert.Equal(MainHudActionButtonState.PressedFrame, state.GetButton(MainHudActionButtonId.Button47).CurrentFrame);
    }

    [Fact]
    public void AdvanceOrganiseBeforeDraw_AlternatesFramesOneAndTwoWithoutTimeout()
    {
        MainHudActionButtonStripState state = new();
        state.ArmOrganiseBlink();

        state.AdvanceOrganiseBeforeDraw(new TickSequence(100, 100).Read);
        Assert.Equal(1, state.GetButton(MainHudActionButtonId.Main3OrganiseBtn).CurrentFrame);

        state.AdvanceOrganiseBeforeDraw(new TickSequence(600).Read);
        Assert.Equal(2, state.GetButton(MainHudActionButtonId.Main3OrganiseBtn).CurrentFrame);

        state.AdvanceOrganiseBeforeDraw(new TickSequence(1100).Read);
        Assert.Equal(1, state.GetButton(MainHudActionButtonId.Main3OrganiseBtn).CurrentFrame);

        state.AdvanceOrganiseBeforeDraw(new TickSequence(1000100).Read);
        Assert.True(state.IsOrganiseBlinkActive);
    }

    [Fact]
    public void ResetAndRearmOrganiseBlink_PreservesNativeEpoch()
    {
        MainHudActionButtonStripState state = new();
        state.ArmOrganiseBlink();
        state.AdvanceOrganiseBeforeDraw(new TickSequence(100, 100).Read);

        state.ResetOrganiseBlink();

        Assert.False(state.IsOrganiseBlinkActive);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.GetButton(MainHudActionButtonId.Main3OrganiseBtn).CurrentFrame);

        state.ArmOrganiseBlink();
        state.AdvanceOrganiseBeforeDraw(new TickSequence(600).Read);

        Assert.True(state.IsOrganiseBlinkActive);
        Assert.Equal(MainHudActionButtonState.DisabledFrame, state.GetButton(MainHudActionButtonId.Main3OrganiseBtn).CurrentFrame);
    }

    [Fact]
    public void AdvanceOrganiseBeforeDraw_WhenInactiveDoesNotReadClock()
    {
        MainHudActionButtonStripState state = new();

        state.AdvanceOrganiseBeforeDraw(() => throw new InvalidOperationException("Clock must not be read."));

        Assert.Equal(MainHudActionButtonState.NormalFrame, state.GetButton(MainHudActionButtonId.Main3OrganiseBtn).CurrentFrame);
    }

    private sealed class TickSequence(params uint[] values)
    {
        private int _index;

        public uint Read()
        {
            if (_index >= values.Length)
            {
                throw new InvalidOperationException("Unexpected clock read.");
            }

            return values[_index++];
        }
    }
}
