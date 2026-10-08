using OpenConquer.Client.UI.Hud.CheckControls;

namespace OpenConquer.Client.Tests.UI.Hud;

public sealed class MainHudCheckControlStateTests
{
    [Fact]
    public void Constructor_StartsAtNativeStateZero()
    {
        MainHudCheckControlState state = new();

        Assert.Equal(0, state.CurrentState);
        Assert.Equal(0, state.RenderFrame);
    }

    [Fact]
    public void LeftButtonDown_TogglesStateZeroToOne()
    {
        MainHudCheckControlState state = new();

        state.HandleLeftButtonDown();

        Assert.Equal(1, state.CurrentState);
        Assert.Equal(1, state.RenderFrame);
    }

    [Fact]
    public void LeftButtonDown_TogglesStateOneBackToZero()
    {
        MainHudCheckControlState state = new();

        state.HandleLeftButtonDown();
        state.HandleLeftButtonDown();

        Assert.Equal(0, state.CurrentState);
        Assert.Equal(0, state.RenderFrame);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(-256)]
    [InlineData(-255)]
    public void SetState_AcceptsValuesWhoseLowByteIsValidNativeState(int requestedState)
    {
        MainHudCheckControlState state = new();
        int expectedState = unchecked((byte)requestedState);

        state.SetState(requestedState);

        Assert.Equal(expectedState, state.CurrentState);
        Assert.Equal(expectedState, state.RenderFrame);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(255)]
    [InlineData(258)]
    [InlineData(-1)]
    [InlineData(-2)]
    public void SetState_RejectsValuesWhoseLowByteIsOutsideNativeStateRange(int requestedState)
    {
        MainHudCheckControlState state = new();
        state.SetState(1);

        state.SetState(requestedState);

        Assert.Equal(1, state.CurrentState);
        Assert.Equal(1, state.RenderFrame);
    }

    [Fact]
    public void SetState_DoesNotAffectSubsequentToggleSequence()
    {
        MainHudCheckControlState state = new();
        state.SetState(1);

        state.HandleLeftButtonDown();

        Assert.Equal(0, state.CurrentState);
        Assert.Equal(0, state.RenderFrame);
    }
}
