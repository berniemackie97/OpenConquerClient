using OpenConquer.Client.UI.Hud.ActionButtons;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudActionButtonStripInputTests
{
    private static readonly MainHudActionButtonLayout s_layout =
        MainHudActionButtonLayout.Create(new LogicalRenderSize(800, 600));

    [Fact]
    public void PointerMoveInsideAvailableButtonUpdatesOnlyThatButton()
    {
        MainHudActionButtonStripState state = new();
        MainHudActionButtonStripInput input = CreateInput(state);
        MainHudActionButtonBounds bounds = s_layout.GetBounds(MainHudActionButtonId.Button42);

        Assert.True(input.HandlePointerMoved(bounds.X, bounds.Y));
        Assert.True(state.GetButton(MainHudActionButtonId.Button42).IsCursorInside);

        foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
        {
            if (definition.Id != MainHudActionButtonId.Button42)
            {
                Assert.False(state.GetButton(definition.Id).IsCursorInside);
            }
        }
    }

    [Fact]
    public void PointerMoveOutsideAllButtonsReturnsFalse()
    {
        MainHudActionButtonStripState state = new();
        MainHudActionButtonStripInput input = CreateInput(state);

        Assert.False(input.HandlePointerMoved(-1, -1));
    }

    [Fact]
    public void UnavailableButtonIsNotInteractive()
    {
        MainHudActionButtonStripState state = new();
        MainHudActionButtonStripInput input = CreateInput(state, MainHudActionButtonId.Button42);
        MainHudActionButtonBounds bounds = s_layout.GetBounds(MainHudActionButtonId.Button42);

        Assert.False(input.HandlePointerMoved(bounds.X, bounds.Y));
        Assert.False(input.HandleLeftButtonDown(bounds.X, bounds.Y));
        Assert.False(state.GetButton(MainHudActionButtonId.Button42).IsCursorInside);
        Assert.False(state.GetButton(MainHudActionButtonId.Button42).IsPointerCaptured);
    }

    [Fact]
    public void LeftButtonDownCapturesSingleButton()
    {
        MainHudActionButtonStripState state = new();
        MainHudActionButtonStripInput input = CreateInput(state);
        MainHudActionButtonBounds bounds = s_layout.GetBounds(MainHudActionButtonId.Button46);

        Assert.True(input.HandleLeftButtonDown(bounds.X, bounds.Y));
        Assert.True(state.GetButton(MainHudActionButtonId.Button46).IsPointerCaptured);

        int captured = MainHudActionButtonDefinitions.NativeDrawOrder.ToArray()
            .Count(definition => state.GetButton(definition.Id).IsPointerCaptured);

        Assert.Equal(1, captured);
    }

    [Fact]
    public void PointerMoveWhileCapturedRoutesOnlyToCaptureOwner()
    {
        MainHudActionButtonStripState state = new();
        MainHudActionButtonStripInput input = CreateInput(state);
        MainHudActionButtonBounds capturedBounds = s_layout.GetBounds(MainHudActionButtonId.Button40);
        MainHudActionButtonBounds otherBounds = s_layout.GetBounds(MainHudActionButtonId.Button41);

        input.HandleLeftButtonDown(capturedBounds.X, capturedBounds.Y);

        Assert.True(input.HandlePointerMoved(otherBounds.X, otherBounds.Y));
        Assert.True(state.GetButton(MainHudActionButtonId.Button40).IsPointerCaptured);
        Assert.False(state.GetButton(MainHudActionButtonId.Button40).IsCursorInside);
        Assert.False(state.GetButton(MainHudActionButtonId.Button41).IsCursorInside);
    }

    [Fact]
    public void LeftButtonUpInsideCaptureOwnerActivatesNativeControl()
    {
        MainHudActionButtonStripState state = new();
        MainHudActionButtonStripInput input = CreateInput(state);
        MainHudActionButtonBounds bounds = s_layout.GetBounds(MainHudActionButtonId.Main3MissionBtn);

        input.HandleLeftButtonDown(bounds.X, bounds.Y);

        bool handled = input.HandleLeftButtonUp(bounds.X, bounds.Y, out MainHudActionButtonId? activated);

        Assert.True(handled);
        Assert.Equal(MainHudActionButtonId.Main3MissionBtn, activated);
        Assert.False(state.GetButton(MainHudActionButtonId.Main3MissionBtn).IsPointerCaptured);
        Assert.Equal(MainHudActionButtonState.NormalFrame, state.GetButton(MainHudActionButtonId.Main3MissionBtn).CurrentFrame);
    }

    [Fact]
    public void LeftButtonUpOutsideCaptureOwnerDoesNotActivate()
    {
        MainHudActionButtonStripState state = new();
        MainHudActionButtonStripInput input = CreateInput(state);
        MainHudActionButtonBounds bounds = s_layout.GetBounds(MainHudActionButtonId.Button410);

        input.HandleLeftButtonDown(bounds.X, bounds.Y);

        bool handled = input.HandleLeftButtonUp(bounds.X - 1, bounds.Y, out MainHudActionButtonId? activated);

        Assert.True(handled);
        Assert.Null(activated);
        Assert.False(state.GetButton(MainHudActionButtonId.Button410).IsPointerCaptured);
    }

    [Fact]
    public void UnmappedReleaseClearsCaptureWithoutActivation()
    {
        MainHudActionButtonStripState state = new();
        MainHudActionButtonStripInput input = CreateInput(state);
        MainHudActionButtonBounds bounds = s_layout.GetBounds(MainHudActionButtonId.Main3OrganiseBtn);

        input.HandleLeftButtonDown(bounds.X, bounds.Y);

        Assert.True(input.HandleLeftButtonUp(-1, -1, out MainHudActionButtonId? activated));
        Assert.Null(activated);
        Assert.False(state.GetButton(MainHudActionButtonId.Main3OrganiseBtn).IsPointerCaptured);
    }

    [Fact]
    public void ButtonBecomingUnavailableDuringCaptureSuppressesActivationAndReleasesCapture()
    {
        MainHudActionButtonStripState state = new();
        HashSet<MainHudActionButtonId> unavailable = [];
        MainHudActionButtonStripInput input = new(state, s_layout, id => !unavailable.Contains(id));
        MainHudActionButtonBounds bounds = s_layout.GetBounds(MainHudActionButtonId.Button47);

        input.HandleLeftButtonDown(bounds.X, bounds.Y);
        unavailable.Add(MainHudActionButtonId.Button47);

        Assert.True(input.HandleLeftButtonUp(bounds.X, bounds.Y, out MainHudActionButtonId? activated));
        Assert.Null(activated);
        Assert.False(state.GetButton(MainHudActionButtonId.Button47).IsPointerCaptured);
    }

    [Fact]
    public void DisabledButtonIsNotCaptured()
    {
        MainHudActionButtonStripState state = new();
        MainHudActionButtonStripInput input = CreateInput(state);
        MainHudActionButtonState button = state.GetButton(MainHudActionButtonId.Button41);
        MainHudActionButtonBounds bounds = s_layout.GetBounds(MainHudActionButtonId.Button41);

        button.SetEnabled(false);

        Assert.False(input.HandleLeftButtonDown(bounds.X, bounds.Y));
        Assert.False(button.IsPointerCaptured);
        Assert.Equal(MainHudActionButtonState.DisabledFrame, button.CurrentFrame);
    }

    [Fact]
    public void LeftButtonUpWithoutCaptureReturnsFalse()
    {
        MainHudActionButtonStripState state = new();
        MainHudActionButtonStripInput input = CreateInput(state);

        Assert.False(input.HandleLeftButtonUp(0, 0, out MainHudActionButtonId? activated));
        Assert.Null(activated);
    }

    private static MainHudActionButtonStripInput CreateInput(
        MainHudActionButtonStripState state,
        MainHudActionButtonId? unavailable = null)
    {
        return new MainHudActionButtonStripInput(state, s_layout, id => id != unavailable);
    }
}
