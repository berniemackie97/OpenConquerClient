using OpenConquer.Client.UI.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginCredentialEditingTests
{
    [Fact]
    public void CanInsertDeleteAndNavigateAtBothEndsAndInTheMiddle()
    {
        using LoginCredentialInput input = Create(out LoginCredentialLayout layout);
        Focus(input, layout.AccountField);
        Enter(input, "ABCD");
        Press(input, DesktopKey.Left);
        Press(input, DesktopKey.Left);
        Assert.Equal(2, input.CaretIndex);
        input.TextEntered('X');
        Assert.Equal("ABXCD", input.AccountDisplay);
        Press(input, DesktopKey.Backspace);
        Assert.Equal("ABCD", input.AccountDisplay);
        Press(input, DesktopKey.Delete);
        Assert.Equal("ABD", input.AccountDisplay);
        Press(input, DesktopKey.Home);
        Press(input, DesktopKey.Backspace);
        Assert.Equal("ABD", input.AccountDisplay);
        Press(input, DesktopKey.Delete);
        Assert.Equal("BD", input.AccountDisplay);
        Press(input, DesktopKey.End);
        Press(input, DesktopKey.Delete);
        Assert.Equal("BD", input.AccountDisplay);
        Assert.Equal(2, input.CaretIndex);
    }

    [Fact]
    public void ShiftSelectionReplacesTextAndCollapsesCorrectly()
    {
        using LoginCredentialInput input = Create(out LoginCredentialLayout layout);
        Focus(input, layout.AccountField);
        Enter(input, "ABCDE");
        Press(input, DesktopKey.Home);
        Press(input, DesktopKey.Right, DesktopKeyModifiers.Shift);
        Press(input, DesktopKey.Right, DesktopKeyModifiers.Shift);
        Assert.Equal(0, input.SelectionStart);
        Assert.Equal(2, input.SelectionEnd);
        input.TextEntered('Z');
        Assert.Equal("ZCDE", input.AccountDisplay);
        Assert.False(input.HasSelection);
        Press(input, DesktopKey.End, DesktopKeyModifiers.Shift);
        Assert.Equal(1, input.SelectionStart);
        Assert.Equal(4, input.SelectionEnd);
        Press(input, DesktopKey.Left);
        Assert.False(input.HasSelection);
        Assert.Equal(1, input.CaretIndex);
    }

    [Fact]
    public void PointerPlacementAndDragSelectAndReplaceInEitherDirection()
    {
        using LoginCredentialInput input = Create(out LoginCredentialLayout layout);
        Focus(input, layout.AccountField);
        Enter(input, "ABCDE");
        Assert.True(input.PointerPressed(true, layout.AccountField.X + 2, layout.AccountField.Y + 2, caretIndex: 1));
        Assert.True(input.IsSelecting);
        input.PointerDragged(4);
        Assert.Equal(1, input.SelectionStart);
        Assert.Equal(4, input.SelectionEnd);
        input.PointerDragged(2);
        Assert.Equal(1, input.SelectionStart);
        Assert.Equal(2, input.SelectionEnd);
        input.PointerReleased();
        Assert.False(input.IsSelecting);
        Assert.True(input.Paste("XY"));
        Assert.Equal("AXYCDE", input.AccountDisplay);
        Assert.True(input.PointerPressed(true, layout.AccountField.X + 2, layout.AccountField.Y + 2, caretIndex: 5));
        input.PointerDragged(-1);
        Assert.Equal(0, input.SelectionStart);
        Assert.Equal(5, input.SelectionEnd);
        input.PointerReleased();
        Press(input, DesktopKey.Backspace);
        Assert.Equal("E", input.AccountDisplay);
    }

    [Fact]
    public void SelectAllPasteAndCutAreBoundedAndAtomic()
    {
        using LoginCredentialInput input = Create(out LoginCredentialLayout layout);
        Focus(input, layout.AccountField);
        Enter(input, "ABC");
        Press(input, DesktopKey.A, DesktopKeyModifiers.Control);
        Assert.Equal("ABC", input.SelectedAccountText());
        Assert.False(input.Paste("New\nLine"));
        Assert.False(input.Paste("新"));
        Assert.Equal("ABC", input.AccountDisplay);
        Assert.True(input.Paste("Z"));
        Assert.Equal("Z", input.AccountDisplay);
        Press(input, DesktopKey.A, DesktopKeyModifiers.Super);
        Assert.True(input.CutSelectedAccount());
        Assert.Equal(string.Empty, input.AccountDisplay);
        Assert.False(input.CutSelectedAccount());
        Assert.False(input.Paste(new string('A', LoginCredentialInput.MaximumAccountLength + 1)));
        Assert.True(input.Paste(new string('A', LoginCredentialInput.MaximumAccountLength)));
        Assert.False(input.Paste("B"));
        Assert.Equal(LoginCredentialInput.MaximumAccountLength, input.AccountLength);
    }

    [Fact]
    public void PasswordClipboardAndPresentationNeverExposePlaintext()
    {
        using LoginCredentialInput input = Create(out LoginCredentialLayout layout);
        Focus(input, layout.PasswordField);
        Enter(input, "Password_1");
        Press(input, DesktopKey.A, DesktopKeyModifiers.Super);
        Assert.Null(input.SelectedAccountText());
        Assert.False(input.CutSelectedAccount());
        Assert.Equal(new string('*', 10), input.PasswordMask);
        Assert.Equal(10, input.PasswordLength);
        Assert.True(input.Paste("New_2"));
        Assert.Equal("*****", input.PasswordMask);
        Assert.False(input.PasswordMask.Contains("New", StringComparison.Ordinal));
        input.FocusLost();
        Assert.Equal(string.Empty, input.PasswordMask);
        Assert.Equal(0, input.PasswordLength);
    }

    [Fact]
    public void SoundRevisionChangesOnlyForAcceptedMutationsNotNavigationOrFailures()
    {
        using LoginCredentialInput input = Create(out LoginCredentialLayout layout);
        Focus(input, layout.AccountField);
        int version = input.EditRevision;
        Press(input, DesktopKey.Backspace);
        Press(input, DesktopKey.Left);
        Press(input, DesktopKey.Delete);
        input.TextEntered(' ');
        Assert.Equal(version, input.EditRevision);
        input.TextEntered('A');
        Assert.Equal(version + 1, input.EditRevision);
        Press(input, DesktopKey.Left);
        Assert.Equal(version + 1, input.EditRevision);
        Press(input, DesktopKey.Delete);
        Assert.Equal(version + 2, input.EditRevision);
        Press(input, DesktopKey.Delete);
        Assert.Equal(version + 2, input.EditRevision);
    }

    [Fact]
    public void TabFocusRevokeAndOutsidePointerInvalidateDragAndSecrets()
    {
        using LoginCredentialInput input = Create(out LoginCredentialLayout layout, out LoginServerSelection selection);
        Focus(input, layout.AccountField);
        Enter(input, "PLAYER");
        Press(input, DesktopKey.Tab);
        Enter(input, "PASSWORD");
        input.PointerPressed(true, layout.PasswordField.X + 3, layout.PasswordField.Y + 3, 2);
        input.PointerDragged(7);
        Assert.True(input.HasSelection);
        Assert.False(input.PointerPressed(false, -1, -1));
        Assert.False(input.IsSelecting);
        Assert.Equal(0, input.PasswordLength);
        Assert.Equal("PLAYER", input.AccountDisplay);
        selection.ReplaceCatalog(LoginServerCatalog.Empty);
        input.Synchronize();
        Assert.False(input.Enabled);
        Assert.Equal(0, input.AccountLength);
        Assert.Equal(LoginCredentialFocus.None, input.Focus);
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void PointerHitRegionsStayInsideRetailPanel(int width, int height)
    {
        using LoginCredentialInput input = Create(out LoginCredentialLayout layout, width, height);
        Assert.True(input.PointerPressed(true, layout.AccountField.X + 1, layout.AccountField.Y + 1, caretIndex: 0));
        input.PointerReleased();
        Assert.False(input.PointerPressed(true, layout.AccountField.X - 1, layout.AccountField.Y + 1));
        Assert.True(input.PointerPressed(true, layout.PasswordField.X + 1, layout.PasswordField.Y + 1, caretIndex: 0));
        input.PointerReleased();
    }

    private static LoginCredentialInput Create(out LoginCredentialLayout layout, int width = 1024, int height = 768)
    {
        return Create(out layout, out _, width, height);
    }

    private static LoginCredentialInput Create(out LoginCredentialLayout layout, out LoginServerSelection selection, int width = 1024, int height = 768)
    {
        selection = new LoginServerSelection();
        selection.ReplaceCatalog(new LoginServerCatalog([new LoginRegion("region", "Region", [new LoginServer("realm", "Realm", "NativeRealm")])]));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        layout = LoginCredentialLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(width, height)));
        return new LoginCredentialInput(selection, layout);
    }

    private static void Focus(LoginCredentialInput input, LoginSpritePlacement field) =>
        Assert.True(input.PointerPressed(true, field.X + 2, field.Y + 2));

    private static void Enter(LoginCredentialInput input, string value)
    {
        foreach (char c in value)
            input.TextEntered(c);
    }

    private static void Press(LoginCredentialInput input, DesktopKey key, DesktopKeyModifiers modifiers = DesktopKeyModifiers.None) =>
        Assert.True(input.KeyPressed(new DesktopKeyEvent(key, modifiers, false)));
}
