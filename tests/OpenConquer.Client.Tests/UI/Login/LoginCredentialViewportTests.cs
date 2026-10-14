using OpenConquer.Client.UI.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginCredentialViewportTests
{
    [Theory]
    [InlineData(1024, 768, 1366, 768)]
    [InlineData(800, 600, 1280, 720)]
    [InlineData(800, 600, 800, 600)]
    public void DragXClampsThroughLetterboxWithoutExtendingOrdinaryHitTesting(int width, int height, int hostWidth, int hostHeight)
    {
        PresentationViewport viewport = PresentationViewport.Compute(new LogicalRenderSize(width, height), hostWidth, hostHeight);
        Assert.Equal(0, viewport.MapPointerXClamped(int.MinValue));
        Assert.Equal(width - 1, viewport.MapPointerXClamped(int.MaxValue));
        Assert.Equal(0, viewport.MapPointerXClamped(viewport.OffsetX));
        Assert.Equal(width - 1, viewport.MapPointerXClamped(viewport.OffsetX + viewport.Width - 1));
        Assert.False(viewport.TryMapPointerToLogical(-1, -1, out _, out _));
    }

    [Fact]
    public void MinimizedViewportCannotProduceOutOfRangeDragCoordinates()
    {
        PresentationViewport viewport = PresentationViewport.Compute(new LogicalRenderSize(1024, 768), 0, 0);
        Assert.True(viewport.IsEmpty);
        Assert.Equal(0, viewport.MapPointerXClamped(int.MinValue));
        Assert.Equal(0, viewport.MapPointerXClamped(int.MaxValue));
        Assert.False(viewport.TryMapPointerToLogical(0, 0, out _, out _));
    }

    [Fact]
    public void CapturedDragSelectsBoundedAccountCharactersInEitherDirection()
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(new LoginServerCatalog([
            new LoginRegion("region", "Region", [new LoginServer("realm", "Realm", "NativeRealm")])
        ]));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        LoginCredentialLayout layout = LoginCredentialLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)));
        using LoginCredentialInput input = new(selection, layout);
        Assert.True(input.PointerPressed(true, layout.AccountField.X + 2, layout.AccountField.Y + 2));
        foreach (char character in new string('A', LoginCredentialInput.MaximumAccountLength))
            input.TextEntered(character);
        input.PointerReleased();
        Assert.True(input.PointerPressed(true, layout.AccountField.X + 2, layout.AccountField.Y + 2, caretIndex: 16));
        input.PointerDragged(-1000);
        Assert.Equal(0, input.SelectionStart);
        Assert.Equal(16, input.SelectionEnd);
        input.PointerDragged(1000);
        Assert.Equal(16, input.SelectionStart);
        Assert.Equal(LoginCredentialInput.MaximumAccountLength, input.SelectionEnd);
        input.PointerReleased();
        Assert.False(input.IsSelecting);
        Assert.Equal(LoginCredentialInput.MaximumAccountLength, input.AccountLength);
        Assert.False(input.HasSelection && input.SelectionEnd > input.AccountLength);
    }

    [Fact]
    public void FocusLossCancelsCapturedSelectionAndZeroesPassword()
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(new LoginServerCatalog([
            new LoginRegion("region", "Region", [new LoginServer("realm", "Realm", "NativeRealm")])
        ]));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        LoginCredentialLayout layout = LoginCredentialLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(800, 600)));
        using LoginCredentialInput input = new(selection, layout);
        Assert.True(input.PointerPressed(true, layout.PasswordField.X + 2, layout.PasswordField.Y + 2));
        input.TextEntered('S');
        input.PointerDragged(0);
        input.FocusLost();
        Assert.False(input.IsSelecting);
        Assert.Equal(0, input.PasswordLength);
        Assert.Equal(string.Empty, input.PasswordMask);
        Assert.Equal(LoginCredentialFocus.None, input.Focus);
    }
}
