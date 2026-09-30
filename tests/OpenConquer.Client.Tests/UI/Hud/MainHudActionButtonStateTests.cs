using OpenConquer.Client.UI.Hud;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudActionButtonStateTests
{
    private static readonly MainHudActionButtonBounds s_bounds = new(100, 200, 46, 22);

    [Fact]
    public void Constructor_StartsEnabledOnNormalFrameWithoutHover()
    {
        MainHudActionButtonState state = new();

        Assert.True(state.IsEnabled);
        Assert.False(state.IsCursorInside);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.CurrentFrame);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.RenderFrame);
    }

    [Fact]
    public void SetEnabled_DisablesAndRestoresNativeFrames()
    {
        MainHudActionButtonState state = new();

        state.SetEnabled(false);

        Assert.False(state.IsEnabled);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudActionButtonState.DisabledFrame, state.CurrentFrame);
        Assert.Equal(MainHudActionButtonState.DisabledFrame, state.RenderFrame);

        state.SetEnabled(true);

        Assert.True(state.IsEnabled);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.CurrentFrame);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.RenderFrame);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SetCurrentFrame_AcceptsNativeFrameRange(int frameIndex)
    {
        MainHudActionButtonState state = new();

        state.SetCurrentFrame(frameIndex);

        Assert.Equal(frameIndex, state.CurrentFrame);
        Assert.Equal(frameIndex, state.RenderFrame);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void SetCurrentFrame_RejectsFramesOutsideNativeRange(int frameIndex)
    {
        MainHudActionButtonState state = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetCurrentFrame(frameIndex));
    }

    [Fact]
    public void PointerMove_DoesNotUseHoverFrameByDefault()
    {
        MainHudActionButtonState state = new();

        bool handled = state.HandlePointerMoved(s_bounds.X, s_bounds.Y, s_bounds);

        Assert.True(handled);
        Assert.True(state.IsCursorInside);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.RenderFrame);
    }

    [Fact]
    public void HoverFrame_IsUsedOnlyWhenExplicitlyEnabled()
    {
        MainHudActionButtonState state = new();
        state.SetHoverFrameEnabled(true);

        state.HandlePointerMoved(s_bounds.X, s_bounds.Y, s_bounds);
        Assert.Equal(MainHudActionButtonState.HoverFrame, state.RenderFrame);

        state.HandlePointerMoved(s_bounds.X - 1, s_bounds.Y, s_bounds);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.RenderFrame);
    }

    [Fact]
    public void LeftButtonDownInside_CapturesPointerAndSelectsPressedFrame()
    {
        MainHudActionButtonState state = new();

        bool handled = state.HandleLeftButtonDown(s_bounds.X, s_bounds.Y, s_bounds);

        Assert.True(handled);
        Assert.True(state.IsPointerCaptured);
        Assert.True(state.IsCursorInside);
        Assert.Equal(MainHudActionButtonState.PressedFrame, state.CurrentFrame);
        Assert.Equal(MainHudActionButtonState.PressedFrame, state.RenderFrame);
    }

    [Fact]
    public void LeftButtonDownOutside_IsIgnored()
    {
        MainHudActionButtonState state = new();

        bool handled = state.HandleLeftButtonDown(s_bounds.X - 1, s_bounds.Y, s_bounds);

        Assert.False(handled);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.CurrentFrame);
    }

    [Fact]
    public void DisabledButton_DoesNotCapturePointer()
    {
        MainHudActionButtonState state = new();
        state.SetEnabled(false);

        bool handled = state.HandleLeftButtonDown(s_bounds.X, s_bounds.Y, s_bounds);

        Assert.False(handled);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudActionButtonState.DisabledFrame, state.CurrentFrame);
    }

    [Fact]
    public void LeftButtonUpInsideAfterCapture_ActivatesAndReturnsToNormal()
    {
        MainHudActionButtonState state = new();
        state.HandleLeftButtonDown(s_bounds.X, s_bounds.Y, s_bounds);

        bool handled = state.HandleLeftButtonUp(s_bounds.X, s_bounds.Y, s_bounds, out bool activated);

        Assert.True(handled);
        Assert.True(activated);
        Assert.False(state.IsPointerCaptured);
        Assert.True(state.IsCursorInside);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.CurrentFrame);
    }

    [Fact]
    public void LeftButtonUpOutsideAfterCapture_DoesNotActivate()
    {
        MainHudActionButtonState state = new();
        state.HandleLeftButtonDown(s_bounds.X, s_bounds.Y, s_bounds);

        bool handled = state.HandleLeftButtonUp(s_bounds.X - 1, s_bounds.Y, s_bounds, out bool activated);

        Assert.True(handled);
        Assert.False(activated);
        Assert.False(state.IsPointerCaptured);
        Assert.False(state.IsCursorInside);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.CurrentFrame);
    }

    [Fact]
    public void PointerMoveOutsideWhileCaptured_RemainsHandled()
    {
        MainHudActionButtonState state = new();
        state.HandleLeftButtonDown(s_bounds.X, s_bounds.Y, s_bounds);

        bool handled = state.HandlePointerMoved(s_bounds.X - 1, s_bounds.Y, s_bounds);

        Assert.True(handled);
        Assert.True(state.IsPointerCaptured);
        Assert.False(state.IsCursorInside);
        Assert.Equal(MainHudActionButtonState.PressedFrame, state.CurrentFrame);
    }

    [Fact]
    public void LeftButtonUpWithoutCapture_IsIgnored()
    {
        MainHudActionButtonState state = new();

        bool handled = state.HandleLeftButtonUp(s_bounds.X, s_bounds.Y, s_bounds, out bool activated);

        Assert.False(handled);
        Assert.False(activated);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.CurrentFrame);
    }

    [Fact]
    public void HitTest_UsesRightAndBottomExclusiveBounds()
    {
        MainHudActionButtonState state = new();

        Assert.True(state.HandlePointerMoved(s_bounds.X, s_bounds.Y, s_bounds));
        Assert.True(state.HandlePointerMoved(s_bounds.X + s_bounds.HitWidth - 1, s_bounds.Y + s_bounds.HitHeight - 1, s_bounds));
        Assert.False(state.HandlePointerMoved(s_bounds.X + s_bounds.HitWidth, s_bounds.Y, s_bounds));
        Assert.False(state.HandlePointerMoved(s_bounds.X, s_bounds.Y + s_bounds.HitHeight, s_bounds));
    }
}
