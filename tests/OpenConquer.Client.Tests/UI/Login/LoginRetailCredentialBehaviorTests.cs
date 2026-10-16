using OpenConquer.Client.UI.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginRetailCredentialBehaviorTests
{
    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void EditingWithoutAnyRealmOrInitialFocusFollowsOriginalTabPath(int width, int height)
    {
        LoginServerSelection selection = new();
        LoginCredentialLayout layout = LoginCredentialLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(width, height)));
        using LoginCredentialInput input = new(selection, layout);
        Assert.Equal(LoginCredentialFocus.None, input.Focus);
        Assert.True(input.Enabled);
        Assert.False(input.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.None, false)));
        Assert.True(input.PointerPressed(true, layout.AccountField.X + 1, layout.AccountField.Y + 1));
        input.TextEntered('A');
        Assert.True(input.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.Shift, false)));
        Assert.Equal(LoginCredentialFocus.Password, input.Focus);
        input.TextEntered('P');
        Assert.True(input.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.None, false)));
        Assert.Equal(LoginCredentialFocus.Password, input.Focus);
        Assert.True(input.KeyPressed(new DesktopKeyEvent(DesktopKey.Enter, DesktopKeyModifiers.None, false)));
        Assert.Equal(LoginCredentialFocus.Password, input.Focus);
        Assert.Equal("A", input.AccountDisplay);
        Assert.Equal("*", input.PasswordMask);
        Assert.Null(selection.SelectedServer);
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void TabSelectsExistingPasswordAndShiftDoesNotReverseFocus(int width, int height)
    {
        LoginServerSelection selection = new();
        LoginCredentialLayout fields = LoginCredentialLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(width, height)));
        using LoginCredentialInput input = new(selection, fields);
        Assert.True(input.PointerPressed(true, fields.PasswordField.X + 2, fields.PasswordField.Y + 2));
        input.TextEntered('A');
        input.TextEntered('B');
        Assert.True(input.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.Shift, false)));
        Assert.Equal(LoginCredentialFocus.Password, input.Focus);
        Assert.Equal(0, input.SelectionStart);
        Assert.Equal(2, input.SelectionEnd);
        Assert.True(input.PointerPressed(true, fields.AccountField.X + 2, fields.AccountField.Y + 2));
        Assert.True(input.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.None, false)));
        Assert.Equal(LoginCredentialFocus.Password, input.Focus);
        Assert.Equal(0, input.SelectionStart);
        Assert.Equal(2, input.SelectionEnd);
        input.TextEntered('C');
        Assert.Equal(1, input.PasswordLength);
    }

    [Fact]
    public void EditingAccountClearsPreviousPasswordAndPasswordLengthIsRetailBounded()
    {
        LoginServerSelection selection = new();
        LoginCredentialLayout layout = LoginCredentialLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)));
        using LoginCredentialInput input = new(selection, layout);
        Assert.True(input.PointerPressed(true, layout.PasswordField.X + 1, layout.PasswordField.Y + 1));
        for (int i = 0; i < 50; i++)
            input.TextEntered('S');
        Assert.Equal(15, input.PasswordLength);
        Assert.True(input.PointerPressed(true, layout.AccountField.X + 1, layout.AccountField.Y + 1));
        input.TextEntered('N');
        Assert.Equal(0, input.PasswordLength);
        Assert.Equal("N", input.AccountDisplay);
    }
}
