using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudOrganiseButtonStateTests
{
    private static readonly MainHudOrganiseButtonLayout s_layout =
        MainHudOrganiseButtonLayout.Create(new LogicalRenderSize(800, 600));

    [Fact]
    public void Constructor_StartsEnabledOnNormalFrameWithoutHover()
    {
        MainHudOrganiseButtonState state = new();

        Assert.True(state.IsEnabled);
        Assert.False(state.IsCursorInside);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudOrganiseButtonState.NormalFrame, state.CurrentFrame);
        Assert.Equal(MainHudOrganiseButtonState.NormalFrame, state.RenderFrame);
    }

    [Fact]
    public void SetEnabled_DisablesAndRestoresNativeFrames()
    {
        MainHudOrganiseButtonState state = new();

        state.SetEnabled(false);

        Assert.False(state.IsEnabled);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudOrganiseButtonState.DisabledFrame, state.CurrentFrame);
        Assert.Equal(MainHudOrganiseButtonState.DisabledFrame, state.RenderFrame);

        state.SetEnabled(true);

        Assert.True(state.IsEnabled);
        Assert.Equal(MainHudOrganiseButtonState.NormalFrame, state.CurrentFrame);
        Assert.Equal(MainHudOrganiseButtonState.NormalFrame, state.RenderFrame);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SetCurrentFrame_AcceptsNativeFrameRange(int frameIndex)
    {
        MainHudOrganiseButtonState state = new();

        state.SetCurrentFrame(frameIndex);

        Assert.Equal(frameIndex, state.CurrentFrame);
        Assert.Equal(frameIndex, state.RenderFrame);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void SetCurrentFrame_RejectsFramesOutsideNativeRange(int frameIndex)
    {
        MainHudOrganiseButtonState state = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetCurrentFrame(frameIndex));
    }

    [Fact]
    public void PointerMove_DoesNotUseHoverFrameByDefault()
    {
        MainHudOrganiseButtonState state = new();

        bool handled = state.HandlePointerMoved(s_layout.X, s_layout.Y, s_layout);

        Assert.True(handled);
        Assert.True(state.IsCursorInside);
        Assert.Equal(MainHudOrganiseButtonState.NormalFrame, state.RenderFrame);
    }

    [Fact]
    public void HoverFrame_IsUsedOnlyWhenExplicitlyEnabled()
    {
        MainHudOrganiseButtonState state = new();
        state.SetHoverFrameEnabled(true);

        state.HandlePointerMoved(s_layout.X, s_layout.Y, s_layout);

        Assert.Equal(MainHudOrganiseButtonState.HoverFrame, state.RenderFrame);

        state.HandlePointerMoved(s_layout.X - 1, s_layout.Y, s_layout);

        Assert.Equal(MainHudOrganiseButtonState.NormalFrame, state.RenderFrame);
    }

    [Fact]
    public void LeftButtonDownInside_CapturesPointerAndSelectsPressedFrame()
    {
        MainHudOrganiseButtonState state = new();

        bool handled = state.HandleLeftButtonDown(s_layout.X, s_layout.Y, s_layout);

        Assert.True(handled);
        Assert.True(state.IsPointerCaptured);
        Assert.True(state.IsCursorInside);
        Assert.Equal(MainHudOrganiseButtonState.PressedFrame, state.CurrentFrame);
        Assert.Equal(MainHudOrganiseButtonState.PressedFrame, state.RenderFrame);
    }

    [Fact]
    public void LeftButtonDownOutside_IsIgnored()
    {
        MainHudOrganiseButtonState state = new();

        bool handled = state.HandleLeftButtonDown(s_layout.X - 1, s_layout.Y, s_layout);

        Assert.False(handled);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudOrganiseButtonState.NormalFrame, state.CurrentFrame);
    }

    [Fact]
    public void DisabledButton_DoesNotCapturePointer()
    {
        MainHudOrganiseButtonState state = new();
        state.SetEnabled(false);

        bool handled = state.HandleLeftButtonDown(s_layout.X, s_layout.Y, s_layout);

        Assert.False(handled);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudOrganiseButtonState.DisabledFrame, state.CurrentFrame);
    }

    [Fact]
    public void LeftButtonUpInsideAfterCapture_ActivatesAndReturnsToNormal()
    {
        MainHudOrganiseButtonState state = new();
        state.HandleLeftButtonDown(s_layout.X, s_layout.Y, s_layout);

        bool handled = state.HandleLeftButtonUp(s_layout.X, s_layout.Y, s_layout, out bool activated);

        Assert.True(handled);
        Assert.True(activated);
        Assert.False(state.IsPointerCaptured);
        Assert.True(state.IsCursorInside);
        Assert.Equal(MainHudOrganiseButtonState.NormalFrame, state.CurrentFrame);
    }

    [Fact]
    public void LeftButtonUpOutsideAfterCapture_DoesNotActivate()
    {
        MainHudOrganiseButtonState state = new();
        state.HandleLeftButtonDown(s_layout.X, s_layout.Y, s_layout);

        bool handled = state.HandleLeftButtonUp(s_layout.X - 1, s_layout.Y, s_layout, out bool activated);

        Assert.True(handled);
        Assert.False(activated);
        Assert.False(state.IsPointerCaptured);
        Assert.False(state.IsCursorInside);
        Assert.Equal(MainHudOrganiseButtonState.NormalFrame, state.CurrentFrame);
    }

    [Fact]
    public void PointerMoveOutsideWhileCaptured_RemainsHandled()
    {
        MainHudOrganiseButtonState state = new();
        state.HandleLeftButtonDown(s_layout.X, s_layout.Y, s_layout);

        bool handled = state.HandlePointerMoved(s_layout.X - 1, s_layout.Y, s_layout);

        Assert.True(handled);
        Assert.True(state.IsPointerCaptured);
        Assert.False(state.IsCursorInside);
        Assert.Equal(MainHudOrganiseButtonState.PressedFrame, state.CurrentFrame);
    }

    [Fact]
    public void LeftButtonUpWithoutCapture_IsIgnored()
    {
        MainHudOrganiseButtonState state = new();

        bool handled = state.HandleLeftButtonUp(s_layout.X, s_layout.Y, s_layout, out bool activated);

        Assert.False(handled);
        Assert.False(activated);
        Assert.False(state.IsPointerCaptured);
        Assert.Equal(MainHudOrganiseButtonState.NormalFrame, state.CurrentFrame);
    }

    [Fact]
    public void HitTest_UsesRightAndBottomExclusiveBounds()
    {
        MainHudOrganiseButtonState state = new();

        Assert.True(state.HandlePointerMoved(s_layout.X, s_layout.Y, s_layout));
        Assert.True(state.HandlePointerMoved(s_layout.X + s_layout.HitWidth - 1, s_layout.Y + s_layout.HitHeight - 1, s_layout));

        Assert.False(state.HandlePointerMoved(s_layout.X + s_layout.HitWidth, s_layout.Y, s_layout));
        Assert.False(state.HandlePointerMoved(s_layout.X, s_layout.Y + s_layout.HitHeight, s_layout));
    }
}
