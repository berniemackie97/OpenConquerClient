using OpenConquer.Client.UI.Hud.ActionButtons;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudActionButtonNativeReleaseTests
{
    private static readonly MainHudActionButtonBounds s_bounds =
        MainHudActionButtonLayout.Create(new LogicalRenderSize(800, 600)).GetBounds(MainHudActionButtonId.Button47);

    [Theory]
    [InlineData(MainHudActionButtonState.NormalFrame)]
    [InlineData(MainHudActionButtonState.DisabledFrame)]
    [InlineData(MainHudActionButtonState.HoverFrame)]
    public void LeftButtonUpInside_WhenAnimationOverwritesPressedFrame_PreservesFrameAndActivates(int overwrittenFrame)
    {
        MainHudActionButtonState state = new();

        Assert.True(state.HandleLeftButtonDown(s_bounds.X, s_bounds.Y, s_bounds));
        Assert.Equal(MainHudActionButtonState.PressedFrame, state.CurrentFrame);

        state.SetCurrentFrame(overwrittenFrame);

        Assert.True(state.HandleLeftButtonUp(s_bounds.X, s_bounds.Y, s_bounds, out bool activated));
        Assert.True(activated);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(overwrittenFrame, state.CurrentFrame);
    }

    [Fact]
    public void LeftButtonUpInside_WhenFrameRemainsPressed_ResetsToNormalAndActivates()
    {
        MainHudActionButtonState state = new();

        Assert.True(state.HandleLeftButtonDown(s_bounds.X, s_bounds.Y, s_bounds));

        Assert.True(state.HandleLeftButtonUp(s_bounds.X, s_bounds.Y, s_bounds, out bool activated));
        Assert.True(activated);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.CurrentFrame);
    }

    [Fact]
    public void LeftButtonUpOutside_WhenAnimationOverwritesPressedFrame_PreservesFrameWithoutActivation()
    {
        MainHudActionButtonState state = new();

        Assert.True(state.HandleLeftButtonDown(s_bounds.X, s_bounds.Y, s_bounds));
        state.SetCurrentFrame(MainHudActionButtonState.DisabledFrame);

        Assert.True(state.HandleLeftButtonUp(s_bounds.X - 1, s_bounds.Y, s_bounds, out bool activated));
        Assert.False(activated);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudActionButtonState.DisabledFrame, state.CurrentFrame);
    }
}
