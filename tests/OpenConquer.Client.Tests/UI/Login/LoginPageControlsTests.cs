using OpenConquer.Client.UI.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginPageControlsTests
{
    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void Controls_FitStageWithoutOverlappingSelectionCells(int width, int height)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginPageControls controls = LoginPageControls.Create(layout);
        LoginSpritePlacement[] bounds =
        [
            controls.RegionPrevious,
            controls.RegionNext,
            controls.ServerPrevious,
            controls.ServerNext,
        ];

        foreach (LoginSpritePlacement control in bounds)
        {
            Assert.True(control.Width > 0 && control.Height > 0);
            Assert.True(control.X >= 0 && control.Y >= 0);
            Assert.True(control.X + control.Width <= width && control.Y + control.Height <= height);

            foreach (LoginSpritePlacement cell in layout.RegionCells)
            {
                Assert.False(Overlaps(control, cell));
            }

            foreach (LoginSpritePlacement cell in layout.ServerCells)
            {
                Assert.False(Overlaps(control, cell));
            }
        }

        for (int left = 0; left < bounds.Length; left++)
        {
            for (int right = left + 1; right < bounds.Length; right++)
            {
                Assert.False(Overlaps(bounds[left], bounds[right]));
            }
        }
    }

    [Fact]
    public void Controls_UseStableReferenceStageAnchors()
    {
        LoginPageControls controls = LoginPageControls.Create(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)));

        Assert.Equal(new LoginSpritePlacement(24, 194, 24, 24), controls.RegionPrevious);
        Assert.Equal(new LoginSpritePlacement(24, 570, 24, 24), controls.RegionNext);
        Assert.Equal(new LoginSpritePlacement(935, 218, 24, 24), controls.ServerPrevious);
        Assert.Equal(new LoginSpritePlacement(935, 522, 24, 24), controls.ServerNext);

        foreach (LoginPageControl control in new[] { LoginPageControl.RegionPrevious, LoginPageControl.RegionNext, LoginPageControl.ServerPrevious, LoginPageControl.ServerNext })
        {
            LoginSpritePlacement area = controls.Bounds(control);
            Assert.True(controls.TryHit(area.X, area.Y, out LoginPageControl topLeft));
            Assert.Equal(control, topLeft);
            Assert.True(controls.TryHit(area.X + area.Width - 1, area.Y + area.Height - 1, out LoginPageControl bottomRight));
            Assert.Equal(control, bottomRight);
            Assert.False(controls.TryHit(area.X - 1, area.Y - 1, out _));
        }
    }

    [Fact]
    public void EmptyAndSinglePageCatalogsExposeNoNavigation()
    {
        LoginPageControls controls = LoginPageControls.Create(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)));
        LoginServerSelection selection = new();

        Assert.False(LoginPageControls.IsVisible(LoginPageControl.RegionNext, selection));
        Assert.False(LoginPageControls.IsEnabled(LoginPageControl.ServerNext, selection));

        selection.ReplaceCatalog(CreateCatalog(1, 1));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.False(LoginPageControls.IsVisible(LoginPageControl.RegionNext, selection));
        Assert.False(LoginPageControls.IsVisible(LoginPageControl.ServerNext, selection));
    }

    [Fact]
    public void RegionPaging_RequiresSameEnabledButtonPressAndRelease()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginPageControls controls = LoginPageControls.Create(layout);
        LoginServerSelection selection = new();
        LoginSelectionInput input = new(layout, selection);
        input.ReplaceCatalog(CreateCatalog(17, 1));

        Assert.True(LoginPageControls.IsVisible(LoginPageControl.RegionNext, selection));
        Assert.True(LoginPageControls.IsEnabled(LoginPageControl.RegionNext, selection));
        Assert.False(LoginPageControls.IsEnabled(LoginPageControl.RegionPrevious, selection));
        PressRelease(input, controls.RegionPrevious);
        Assert.Equal(0, selection.RegionPage);

        LoginSpritePlacement next = controls.RegionNext;
        input.PointerPressed(true, next.X + 1, next.Y + 1);
        Assert.Equal(LoginPageControl.RegionNext, input.PressedPageControl);
        input.PointerReleased(true, next.X + next.Width, next.Y + 1);
        Assert.Equal(0, selection.RegionPage);

        PressRelease(input, next);
        Assert.Equal(1, selection.RegionPage);
        Assert.Equal(LoginSelectionFocus.Region, input.Focus);
        Assert.Equal(0, input.FocusedRegionSlot);
        Assert.Null(selection.SelectedRegion);
        Assert.False(LoginPageControls.IsEnabled(LoginPageControl.RegionNext, selection));
        PressRelease(input, next);
        Assert.Equal(1, selection.RegionPage);
        PressRelease(input, controls.RegionPrevious);
        Assert.Equal(0, selection.RegionPage);
    }

    [Fact]
    public void ServerPaging_RevokesInvisibleServerWithoutClearingRetailCredentialInput()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginPageControls pages = LoginPageControls.Create(layout);
        LoginServerSelection selection = new();
        LoginSelectionInput input = new(layout, selection);
        using LoginCredentialInput credentials = new(selection, LoginCredentialLayout.Create(layout));
        input.ReplaceCatalog(CreateCatalog(1, 13));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        credentials.Synchronize();

        LoginCredentialLayout fields = LoginCredentialLayout.Create(layout);
        Assert.True(credentials.PointerPressed(true, fields.AccountField.X + 2, fields.AccountField.Y + 2));
        credentials.TextEntered('A');
        Assert.True(credentials.PointerPressed(true, fields.PasswordField.X + 2, fields.PasswordField.Y + 2));
        credentials.TextEntered('P');
        Assert.Equal(1, credentials.AccountLength);
        Assert.Equal(1, credentials.PasswordLength);

        Assert.True(LoginPageControls.IsVisible(LoginPageControl.ServerNext, selection));
        PressRelease(input, pages.ServerNext);
        credentials.Synchronize();
        Assert.Equal(1, selection.ServerPage);
        Assert.Equal(LoginSelectionFocus.Server, input.Focus);
        Assert.Equal(0, input.FocusedServerSlot);
        Assert.Null(selection.SelectedServer);
        // Paging invalidates the selected endpoint, but does not mutate account/password
        // editor state. Submission remains disallowed without a verified selected server.
        Assert.Equal("A", credentials.AccountDisplay);
        Assert.Equal(1, credentials.AccountLength);
        Assert.Equal(1, credentials.PasswordLength);
        Assert.Equal("*", credentials.PasswordMask);
        Assert.Equal(LoginCredentialFocus.Password, credentials.Focus);
        Assert.False(LoginPageControls.IsEnabled(LoginPageControl.ServerNext, selection));
        Assert.True(LoginPageControls.IsEnabled(LoginPageControl.ServerPrevious, selection));
    }

    [Fact]
    public void CaptureIsCancelledByFocusLossAndCatalogReplacement()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginPageControls pages = LoginPageControls.Create(layout);
        LoginServerSelection selection = new();
        LoginSelectionInput input = new(layout, selection);
        input.ReplaceCatalog(CreateCatalog(17, 1));
        LoginSpritePlacement next = pages.RegionNext;

        input.PointerPressed(true, next.X + 1, next.Y + 1);
        input.FocusLost();
        input.PointerReleased(true, next.X + 1, next.Y + 1);
        Assert.Equal(0, selection.RegionPage);
        Assert.Equal(LoginPageControl.None, input.PressedPageControl);

        input.PointerPressed(true, next.X + 1, next.Y + 1);
        input.ReplaceCatalog(CreateCatalog(17, 1));
        input.PointerReleased(true, next.X + 1, next.Y + 1);
        Assert.Equal(0, selection.RegionPage);
        Assert.Equal(LoginPageControl.None, input.PressedPageControl);

        input.PointerPressed(false, next.X + 1, next.Y + 1);
        input.PointerReleased(true, next.X + 1, next.Y + 1);
        Assert.Equal(0, selection.RegionPage);
    }

    [Fact]
    public void ChangingRegionDuringServerCaptureCancelsServerNavigation()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginPageControls pages = LoginPageControls.Create(layout);
        LoginServerSelection selection = new();
        LoginSelectionInput input = new(layout, selection);
        input.ReplaceCatalog(CreateCatalog(2, 13));
        Assert.True(selection.SelectRegionSlot(0));

        LoginSpritePlacement next = pages.ServerNext;
        input.PointerPressed(true, next.X + 1, next.Y + 1);
        Assert.True(selection.SelectRegionSlot(1));
        input.PointerReleased(true, next.X + 1, next.Y + 1);

        Assert.Equal(0, selection.ServerPage);
        Assert.Equal("region-1", selection.SelectedRegion?.Id);
    }

    [Fact]
    public void KeyboardPageNavigationContinuesToUseTheSameSelectionRules()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginServerSelection selection = new();
        LoginSelectionInput input = new(layout, selection);
        input.ReplaceCatalog(CreateCatalog(17, 13));

        input.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.None, false));
        input.KeyPressed(new DesktopKeyEvent(DesktopKey.PageDown, DesktopKeyModifiers.None, false));
        Assert.Equal(1, selection.RegionPage);
        Assert.Equal(0, input.FocusedRegionSlot);
        Assert.True(selection.SelectRegionSlot(0));
        input.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.None, false));
        input.KeyPressed(new DesktopKeyEvent(DesktopKey.PageDown, DesktopKeyModifiers.None, false));
        Assert.Equal(1, selection.ServerPage);
        Assert.Null(selection.SelectedServer);
        Assert.Equal(0, input.FocusedServerSlot);
    }

    private static bool Overlaps(LoginSpritePlacement left, LoginSpritePlacement right) =>
        left.X < right.X + right.Width && right.X < left.X + left.Width &&
        left.Y < right.Y + right.Height && right.Y < left.Y + left.Height;

    private static void PressRelease(LoginSelectionInput input, LoginSpritePlacement rectangle)
    {
        input.PointerPressed(true, rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2);
        input.PointerReleased(true, rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2);
    }

    private static LoginServerCatalog CreateCatalog(int regionCount, int serversPerRegion) => new(
        Enumerable.Range(0, regionCount).Select(region => new LoginRegion($"region-{region}", $"Region {region}",
            Enumerable.Range(0, serversPerRegion).Select(server => new LoginServer($"realm-{region}-{server}", $"Realm {server}", $"Native{region}_{server}")))));
}
