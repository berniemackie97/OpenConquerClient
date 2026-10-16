using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginActionInputTests
{
    [Theory]
    [InlineData(1024, 768, 650, 704, 132, 40, 859)]
    [InlineData(800, 600, 508, 550, 103, 31, 671)]
    public void Layout_FollowsRetailBottomRowWithinStage(int width, int height, int enterX, int enterY, int enterWidth, int enterHeight, int exitX)
    {
        LoginVisualLayout visual = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginActionLayout layout = LoginActionLayout.Create(visual);
        Assert.Equal(new LoginSpritePlacement(enterX, enterY, enterWidth, enterHeight), layout.Enter);
        Assert.Equal(exitX, layout.Exit.X);
        Assert.Equal(enterY, layout.Exit.Y);
        Assert.Equal(enterWidth, layout.Exit.Width);
        Assert.Equal(enterHeight, layout.Exit.Height);
        Assert.True(layout.Enter.X + layout.Enter.Width < layout.Exit.X);
        Assert.True(layout.Enter.Y >= visual.AccountPanel.Y);
        Assert.True(layout.Exit.X + layout.Exit.Width <= visual.Width);
        Assert.True(layout.Exit.Y + layout.Exit.Height <= visual.Height);
        Assert.Equal(LoginActionButton.None, layout.Hit(visual.AccountPanel.X + 1, visual.AccountPanel.Y + 1));
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void Buttons_DoNotOverlapDirectoryGridsOrCredentials(int width, int height)
    {
        LoginVisualLayout visual = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginActionLayout actions = LoginActionLayout.Create(visual);
        LoginCredentialLayout credentials = LoginCredentialLayout.Create(visual);

        foreach (LoginSpritePlacement button in new[] { actions.Enter, actions.Exit })
        {
            Assert.False(Overlaps(button, credentials.AccountField));
            Assert.False(Overlaps(button, credentials.PasswordField));
            Assert.False(Overlaps(button, LoginDirectoryStatus.Bounds(visual)));
            foreach (LoginSpritePlacement cell in visual.RegionCells)
            {
                Assert.False(Overlaps(button, cell));
            }
            foreach (LoginSpritePlacement cell in visual.ServerCells)
            {
                Assert.False(Overlaps(button, cell));
            }
        }
    }

    [Theory]
    [InlineData(1024, 768, 0)]
    [InlineData(800, 600, 0)]
    public void RetailEnterFootprint_ClearsTheEntireAccountPanel(int width, int height, int expectedGap)
    {
        LoginVisualLayout visual = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginSpritePlacement enter = LoginActionLayout.Create(visual).Enter;
        LoginSpritePlacement panel = visual.AccountPanel;

        Assert.True(enter.X < panel.X + panel.Width);
        Assert.Equal(expectedGap, enter.Y - (panel.Y + panel.Height));
        Assert.False(Overlaps(enter, panel));
    }

    [Fact]
    public void Enter_CapturesButCannotSubmitAnAction()
    {
        LoginActionLayout layout = CreateLayout();
        LoginActionInput input = new(layout);
        Assert.True(input.PointerPressed(true, layout.Enter.X + 4, layout.Enter.Y + 4));
        Assert.Equal(LoginActionButton.None, input.Pressed);
        Assert.Equal(LoginActionButton.None, input.PointerReleased(true, layout.Enter.X + 4, layout.Enter.Y + 4));
    }

    [Fact]
    public void Exit_RequiresMatchingReleaseWithinTheViewport()
    {
        LoginActionLayout layout = CreateLayout();
        LoginActionInput input = new(layout);
        int x = layout.Exit.X + 5;
        int y = layout.Exit.Y + 5;
        Assert.True(input.PointerPressed(true, x, y));
        Assert.Equal(LoginActionButton.Exit, input.Pressed);
        Assert.Equal(LoginActionButton.None, input.PointerReleased(false, x, y));
        Assert.Equal(LoginActionButton.None, input.Pressed);

        Assert.True(input.PointerPressed(true, x, y));
        Assert.Equal(LoginActionButton.None, input.PointerReleased(true, layout.Exit.X + layout.Exit.Width, y));
        Assert.True(input.PointerPressed(true, x, y));
        Assert.Equal(LoginActionButton.Exit, input.PointerReleased(true, x, y));
        Assert.Equal(LoginActionButton.None, input.PointerReleased(true, x, y));
    }

    [Fact]
    public void FocusLossAndUnmappedPress_CancelExitCapture()
    {
        LoginActionLayout layout = CreateLayout();
        LoginActionInput input = new(layout);
        int x = layout.Exit.X + 5;
        int y = layout.Exit.Y + 5;

        Assert.True(input.PointerPressed(true, x, y));
        input.FocusLost();
        Assert.Equal(LoginActionButton.None, input.PointerReleased(true, x, y));
        Assert.False(input.PointerPressed(false, x, y));
        Assert.Equal(LoginActionButton.None, input.PointerReleased(true, x, y));
    }

    [Fact]
    public void HoverAndNewPress_ClearStalePointerCapture()
    {
        LoginActionLayout layout = CreateLayout();
        LoginActionInput input = new(layout);
        int x = layout.Exit.X + 5;
        int y = layout.Exit.Y + 5;

        Assert.True(input.PointerPressed(true, x, y));
        input.PointerMoved(false, x, y);
        Assert.Equal(LoginActionButton.None, input.Hovered);
        Assert.Equal(LoginActionButton.None, input.PointerReleased(false, x, y));

        Assert.True(input.PointerPressed(true, x, y));
        Assert.True(input.PointerPressed(true, layout.Enter.X + 5, layout.Enter.Y + 5));
        Assert.Equal(LoginActionButton.None, input.PointerReleased(true, x, y));
    }

    private static bool Overlaps(LoginSpritePlacement left, LoginSpritePlacement right) =>
        (long)left.X < (long)right.X + right.Width && (long)right.X < (long)left.X + left.Width &&
        (long)left.Y < (long)right.Y + right.Height && (long)right.Y < (long)left.Y + left.Height;

    private static LoginActionLayout CreateLayout() => LoginActionLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)));
}
