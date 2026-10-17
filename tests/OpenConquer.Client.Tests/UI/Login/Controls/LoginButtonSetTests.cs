using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.Tests.UI.Login.Controls;

public sealed class LoginButtonSetTests
{
    private static readonly LoginRect s_left = new(0, 0, 10, 10);
    private static readonly LoginRect s_right = new(20, 0, 10, 10);

    [Fact]
    public void Release_ClicksOnlyTheButtonThePressStartedOn()
    {
        LoginButtonSet<LoginDialogButton> buttons = Create();

        Assert.Equal(LoginDialogButton.Ok, buttons.PointerPressed(true, 5, 5));
        Assert.Equal(LoginDialogButton.Ok, buttons.PointerReleased(true, 5, 5));
        Assert.Null(buttons.PointerReleased(true, 5, 5));

        buttons.PointerPressed(true, 5, 5);
        Assert.Null(buttons.PointerReleased(true, 25, 5));

        buttons.PointerPressed(true, 5, 5);
        Assert.Null(buttons.PointerReleased(mapped: false, 5, 5));
    }

    [Fact]
    public void States_FollowHoverAndPress()
    {
        LoginButtonSet<LoginDialogButton> buttons = Create();

        Assert.Equal(LoginDialogButton.Ok, buttons.PointerMoved(true, 5, 5));
        Assert.Null(buttons.PointerMoved(true, 6, 6));
        Assert.Equal(LoginButtonState.Over, buttons.StateOf(LoginDialogButton.Ok));
        Assert.Equal(LoginButtonState.Up, buttons.StateOf(LoginDialogButton.Cancel));

        buttons.PointerPressed(true, 5, 5);
        Assert.Equal(LoginButtonState.Down, buttons.StateOf(LoginDialogButton.Ok));

        // Dragged off while held: the pressed button shows Up and the other does not light up.
        buttons.PointerMoved(true, 25, 5);
        Assert.Equal(LoginButtonState.Up, buttons.StateOf(LoginDialogButton.Ok));
        Assert.Equal(LoginButtonState.Up, buttons.StateOf(LoginDialogButton.Cancel));

        buttons.PointerMoved(true, 5, 5);
        Assert.Equal(LoginButtonState.Down, buttons.StateOf(LoginDialogButton.Ok));
    }

    [Fact]
    public void Reset_DropsHoverAndCapture()
    {
        LoginButtonSet<LoginDialogButton> buttons = Create();
        buttons.PointerPressed(true, 5, 5);

        buttons.Reset();

        Assert.Equal(LoginButtonState.Up, buttons.StateOf(LoginDialogButton.Ok));
        Assert.Null(buttons.PointerReleased(true, 5, 5));
    }

    [Fact]
    public void BoundsOf_RejectsButtonsOutsideTheSet()
    {
        LoginButtonSet<LoginDialogButton> buttons = new([(LoginDialogButton.Ok, s_left)]);

        Assert.Equal(s_left, buttons.BoundsOf(LoginDialogButton.Ok));
        Assert.Throws<ArgumentOutOfRangeException>(() => buttons.BoundsOf(LoginDialogButton.Cancel));
    }

    private static LoginButtonSet<LoginDialogButton> Create() => new([(LoginDialogButton.Ok, s_left), (LoginDialogButton.Cancel, s_right)]);
}
