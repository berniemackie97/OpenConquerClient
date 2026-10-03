using OpenConquer.Client.UI.Hud;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudCheckControlsInputTests
{
    private static readonly MainHudCheckControlLayout s_layout =
        MainHudCheckControlLayout.Create(new LogicalRenderSize(800, 600));

    [Theory]
    [InlineData(0x3F4)]
    [InlineData(0x3F7)]
    [InlineData(0x3FF)]
    [InlineData(0x3F8)]
    public void LeftButtonDownInsideControl_TogglesOnlyThatControl(int controlId)
    {
        MainHudCheckControlsState state = new();
        MainHudCheckControlsInput input = CreateInput(state);
        MainHudCheckControlId id = (MainHudCheckControlId)controlId;
        MainHudCheckControlBounds bounds = s_layout.GetBounds(id);

        Assert.True(input.HandleLeftButtonDown(bounds.X, bounds.Y));

        foreach (MainHudCheckControlDefinition definition in MainHudCheckControlDefinitions.NativeDrawOrder)
        {
            Assert.Equal(definition.Id == id ? 1 : 0, state.GetControl(definition.Id).CurrentState);
        }
    }

    [Fact]
    public void RepeatedLeftButtonDown_TogglesWithoutRelease()
    {
        MainHudCheckControlsState state = new();
        MainHudCheckControlsInput input = CreateInput(state);
        MainHudCheckControlBounds bounds = s_layout.GetBounds(MainHudCheckControlId.Check40);

        Assert.True(input.HandleLeftButtonDown(bounds.X, bounds.Y));
        Assert.Equal(1, state.GetControl(MainHudCheckControlId.Check40).CurrentState);

        Assert.True(input.HandleLeftButtonDown(bounds.X, bounds.Y));
        Assert.Equal(0, state.GetControl(MainHudCheckControlId.Check40).CurrentState);
    }

    [Fact]
    public void LeftButtonDownOutsideAllControls_IsIgnored()
    {
        MainHudCheckControlsState state = new();
        MainHudCheckControlsInput input = CreateInput(state);

        Assert.False(input.HandleLeftButtonDown(200, 400));

        foreach (MainHudCheckControlDefinition definition in MainHudCheckControlDefinitions.NativeDrawOrder)
        {
            Assert.Equal(0, state.GetControl(definition.Id).CurrentState);
        }
    }

    [Fact]
    public void UnavailableControl_IsNotInteractive()
    {
        MainHudCheckControlsState state = new();
        MainHudCheckControlsInput input = CreateInput(state, MainHudCheckControlId.Check43);
        MainHudCheckControlBounds bounds = s_layout.GetBounds(MainHudCheckControlId.Check43);

        Assert.False(input.HandleLeftButtonDown(bounds.X, bounds.Y));
        Assert.Equal(0, state.GetControl(MainHudCheckControlId.Check43).CurrentState);
    }

    [Fact]
    public void HitTesting_UsesRightAndBottomExclusiveNativeBounds()
    {
        MainHudCheckControlsState state = new();
        MainHudCheckControlsInput input = CreateInput(state);
        MainHudCheckControlBounds bounds = s_layout.GetBounds(MainHudCheckControlId.Button411);

        Assert.True(input.HandleLeftButtonDown(bounds.X + MainHudCheckControlDefinitions.Width - 1, bounds.Y + MainHudCheckControlDefinitions.Height - 1));
        Assert.Equal(1, state.GetControl(MainHudCheckControlId.Button411).CurrentState);

        state.GetControl(MainHudCheckControlId.Button411).SetState(0);

        Assert.False(input.HandleLeftButtonDown(bounds.X + MainHudCheckControlDefinitions.Width, bounds.Y));
        Assert.False(input.HandleLeftButtonDown(bounds.X, bounds.Y + MainHudCheckControlDefinitions.Height));
        Assert.Equal(0, state.GetControl(MainHudCheckControlId.Button411).CurrentState);
    }

    private static MainHudCheckControlsInput CreateInput(MainHudCheckControlsState state, MainHudCheckControlId? unavailable = null)
    {
        return new MainHudCheckControlsInput(state, s_layout, id => id != unavailable);
    }
}
