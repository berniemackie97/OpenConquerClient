using OpenConquer.Client.UI.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginCredentialInputTests
{
    [Fact]
    public void EmptyCatalogAllowsEditingWithoutAuthorizingAnyRealm()
    {
        LoginServerSelection selection = new();
        LoginCredentialLayout layout = LoginCredentialLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)));
        using LoginCredentialInput input = new(selection, layout);

        Assert.Null(selection.SelectedServer);
        Assert.Equal(LoginCredentialFocus.None, input.Focus);
        Assert.True(input.PointerPressed(true, layout.AccountField.X + 2, layout.AccountField.Y + 2));
        input.TextEntered('A');
        Assert.True(input.Enabled);
        Assert.Equal("A", input.AccountDisplay);
        Assert.True(input.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.Shift, false)));
        Assert.Equal(LoginCredentialFocus.Password, input.Focus);
        input.TextEntered('S');
        Assert.Equal("*", input.PasswordMask);
        Assert.Null(selection.SelectedServer);
    }

    [Fact]
    public void SignedViewSelectionEnablesBoundedAsciiInputAndMaskOnly()
    {
        (LoginServerSelection selection, LoginCredentialLayout layout) = Setup(1024, 768);
        using LoginCredentialInput input = new(selection, layout);
        Assert.True(input.PointerPressed(true, layout.AccountField.X + 3, layout.AccountField.Y + 3));

        foreach (char c in "Player_90.@->")
        {
            input.TextEntered(c);
        }

        input.TextEntered(' ');
        input.TextEntered('\u6F22');
        input.TextEntered('\0');
        Assert.Equal("Player_90.@->", input.AccountDisplay);
        Assert.Equal(LoginCredentialFocus.Account, input.Focus);
        Assert.True(input.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.None, false)));
        Assert.Equal(LoginCredentialFocus.Password, input.Focus);
        foreach (char c in "Pass_90.@->")
        {
            input.TextEntered(c);
        }

        Assert.Equal(11, input.PasswordLength);
        Assert.Equal(new string('*', 11), input.PasswordMask);
        Assert.DoesNotContain("Pass", input.PasswordMask);
        Assert.True(input.KeyPressed(new DesktopKeyEvent(DesktopKey.Enter, DesktopKeyModifiers.None, false)));
        Assert.Equal(LoginCredentialFocus.Password, input.Focus);
    }

    [Fact]
    public void BothBuffersAreBoundedAndBackspaceAndForwardDeleteEraseOnlyTargetCharacters()
    {
        (LoginServerSelection selection, LoginCredentialLayout layout) = Setup(1024, 768);
        using LoginCredentialInput input = new(selection, layout);
        input.PointerPressed(true, layout.AccountField.X + 2, layout.AccountField.Y + 2);
        for (int i = 0; i < 100; i++)
            input.TextEntered('A');
        Assert.Equal(LoginCredentialInput.MaximumAccountLength, input.AccountLength);
        input.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.None, false));
        for (int i = 0; i < 100; i++)
            input.TextEntered('9');
        Assert.Equal(LoginCredentialInput.MaximumPasswordLength, input.PasswordLength);
        input.KeyPressed(new DesktopKeyEvent(DesktopKey.Backspace, DesktopKeyModifiers.None, false));
        Assert.Equal(LoginCredentialInput.MaximumPasswordLength - 1, input.PasswordLength);
        input.KeyPressed(new DesktopKeyEvent(DesktopKey.Home, DesktopKeyModifiers.None, false));
        input.KeyPressed(new DesktopKeyEvent(DesktopKey.Delete, DesktopKeyModifiers.None, false));
        Assert.Equal(LoginCredentialInput.MaximumPasswordLength - 2, input.PasswordLength);
        input.KeyPressed(new DesktopKeyEvent(DesktopKey.A, DesktopKeyModifiers.Control, false));
        input.KeyPressed(new DesktopKeyEvent(DesktopKey.Delete, DesktopKeyModifiers.None, false));
        Assert.Equal(0, input.PasswordLength);
        Assert.Equal(string.Empty, input.PasswordMask);
        Assert.Equal(LoginCredentialInput.MaximumAccountLength, input.AccountLength);
    }

    [Fact]
    public void CatalogReplacementClearsSecretButRealmSelectionPreservesEditing()
    {
        (LoginServerSelection selection, LoginCredentialLayout layout) = Setup(1024, 768);
        using LoginCredentialInput input = new(selection, layout);
        Populate(input, layout);
        selection.ReplaceCatalog(LoginServerCatalog.Empty);
        input.Synchronize();
        Assert.True(input.Enabled);
        Assert.Equal(0, input.PasswordLength);
        Assert.Equal("A", input.AccountDisplay);

        selection.ReplaceCatalog(Catalog());
        input.Synchronize();
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        Assert.True(input.PointerPressed(true, layout.PasswordField.X + 2, layout.PasswordField.Y + 2));
        input.TextEntered('P');
        Assert.True(selection.SelectServerSlot(1));
        input.Synchronize();
        Assert.Equal(1, input.PasswordLength);
        Assert.Equal("A", input.AccountDisplay);
    }

    [Fact]
    public void WindowFocusLossAndOutsidePointerClearSecret()
    {
        (LoginServerSelection selection, LoginCredentialLayout layout) = Setup(800, 600);
        using LoginCredentialInput input = new(selection, layout);
        Populate(input, layout);
        input.FocusLost();
        Assert.Equal(0, input.PasswordLength);
        Assert.Equal(LoginCredentialFocus.None, input.Focus);
        Assert.Equal("A", input.AccountDisplay);

        Populate(input, layout);
        Assert.False(input.PointerPressed(false, -1, -1));
        Assert.Equal(0, input.PasswordLength);
    }

    [Fact]
    public void ControlChordCannotInsertTextAndEscapeNeverSubmits()
    {
        (LoginServerSelection selection, LoginCredentialLayout layout) = Setup(1024, 768);
        using LoginCredentialInput input = new(selection, layout);
        Populate(input, layout);
        Assert.True(input.KeyPressed(new DesktopKeyEvent(DesktopKey.V, DesktopKeyModifiers.Control, false)));
        Assert.Equal(1, input.PasswordLength);
        Assert.True(input.KeyPressed(new DesktopKeyEvent(DesktopKey.Escape, DesktopKeyModifiers.None, false)));
        Assert.Equal(LoginCredentialFocus.None, input.Focus);
        Assert.Equal(0, input.PasswordLength);
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void FieldsFitCompletelyInsideApprovedAccountPanel(int width, int height)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginCredentialLayout fields = LoginCredentialLayout.Create(layout);
        foreach (LoginSpritePlacement field in new[] { fields.AccountField, fields.PasswordField })
        {
            Assert.True(field.Width > 0 && field.Height > 0);
            Assert.True(field.X >= layout.AccountPanel.X && field.Y >= layout.AccountPanel.Y);
            Assert.True(field.X + field.Width <= layout.AccountPanel.X + layout.AccountPanel.Width);
            Assert.True(field.Y + field.Height <= layout.AccountPanel.Y + layout.AccountPanel.Height);
        }
        Assert.True(fields.AccountField.Y + fields.AccountField.Height < fields.PasswordField.Y);
    }

    [Fact]
    public void DisposedInputRejectsFurtherEvents()
    {
        (LoginServerSelection selection, LoginCredentialLayout layout) = Setup(1024, 768);
        LoginCredentialInput input = new(selection, layout);
        Populate(input, layout);
        input.Dispose();
        input.Dispose();
        Assert.Equal(0, input.PasswordLength);
        Assert.Throws<ObjectDisposedException>(() => input.TextEntered('X'));
        Assert.Throws<ObjectDisposedException>(() => input.Synchronize());
    }

    private static void Populate(LoginCredentialInput input, LoginCredentialLayout layout)
    {
        Assert.True(input.PointerPressed(true, layout.AccountField.X + 2, layout.AccountField.Y + 2));
        input.TextEntered('A');
        input.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.None, false));
        input.TextEntered('P');
    }

    private static (LoginServerSelection Selection, LoginCredentialLayout Layout) Setup(int width, int height)
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(Catalog());
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        return (selection, LoginCredentialLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(width, height))));
    }

    private static LoginServerCatalog Catalog() => new([new LoginRegion("region", "Region", [
        new LoginServer("realm1", "Realm One", "NativeRealm1"),
        new LoginServer("realm2", "Realm Two", "NativeRealm2"),
    ])]);
}
