using OpenConquer.Client.UI.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginPointerCursorTests
{
    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void PointerUsesOnlyCurrentOccupiedAndEnabledTargets(int width, int height)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginPointerCursor cursor = new(layout);
        LoginServerSelection selection = new();
        LoginActionLayout actions = LoginActionLayout.Create(layout);
        LoginCredentialLayout credentials = LoginCredentialLayout.Create(layout);
        LoginSpritePlacement region = layout.RegionCells[0];
        LoginSpritePlacement emptyRegion = layout.RegionCells[1];
        LoginSpritePlacement server = layout.ServerCells[0];

        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, region));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, server));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, credentials.AccountField));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, actions.Enter));
        Assert.Equal(DesktopPointerCursor.Hand, At(cursor, selection, actions.Exit));

        selection.ReplaceCatalog(Catalog(1, 1));
        Assert.Equal(DesktopPointerCursor.Hand, At(cursor, selection, region));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, emptyRegion));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, server));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, credentials.PasswordField));

        Assert.True(selection.SelectRegionSlot(0));
        Assert.Equal(DesktopPointerCursor.Hand, At(cursor, selection, server));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, credentials.PasswordField));
        Assert.True(selection.SelectServerSlot(0));
        Assert.Equal(DesktopPointerCursor.Text, At(cursor, selection, credentials.AccountField));
        Assert.Equal(DesktopPointerCursor.Text, At(cursor, selection, credentials.PasswordField));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, actions.Enter));

        selection.ReplaceCatalog(LoginServerCatalog.Empty);
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, region));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, server));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, credentials.AccountField));
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void PagingCursorAppearsOnlyForExecutableDirection(int width, int height)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginPointerCursor cursor = new(layout);
        LoginPageControls pages = LoginPageControls.Create(layout);
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(Catalog(17, 13));

        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, pages.RegionPrevious));
        Assert.Equal(DesktopPointerCursor.Hand, At(cursor, selection, pages.RegionNext));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, pages.ServerNext));

        Assert.True(selection.SelectRegionSlot(0));
        Assert.Equal(DesktopPointerCursor.Hand, At(cursor, selection, pages.ServerNext));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, pages.ServerPrevious));
        Assert.True(selection.ChangeServerPage(1));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, pages.ServerNext));
        Assert.Equal(DesktopPointerCursor.Hand, At(cursor, selection, pages.ServerPrevious));

        Assert.True(selection.ChangeRegionPage(1));
        Assert.Equal(DesktopPointerCursor.Hand, At(cursor, selection, pages.RegionPrevious));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, pages.RegionNext));
        Assert.Equal(DesktopPointerCursor.Arrow, At(cursor, selection, pages.ServerNext));
    }

    [Fact]
    public void BoundsAndLetterboxingCannotClaimInteractiveCursor()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginPointerCursor cursor = new(layout);
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(Catalog(1, 1));
        LoginSpritePlacement region = layout.RegionCells[0];

        Assert.Equal(DesktopPointerCursor.Arrow, cursor.Resolve(false, region.X, region.Y, selection));
        Assert.Equal(DesktopPointerCursor.Arrow, cursor.Resolve(true, -1, region.Y, selection));
        Assert.Equal(DesktopPointerCursor.Arrow, cursor.Resolve(true, region.X, -1, selection));
        Assert.Equal(DesktopPointerCursor.Arrow, cursor.Resolve(true, layout.Width, region.Y, selection));
        Assert.Equal(DesktopPointerCursor.Arrow, cursor.Resolve(true, region.X, layout.Height, selection));
        Assert.Equal(DesktopPointerCursor.Hand, cursor.Resolve(true, region.X, region.Y, selection));
        Assert.Equal(DesktopPointerCursor.Hand, cursor.Resolve(true, region.X + region.Width - 1, region.Y + region.Height - 1, selection));
        Assert.Equal(DesktopPointerCursor.Arrow, cursor.Resolve(true, region.X + region.Width, region.Y, selection));
    }

    [Fact]
    public void CursorDoesNotChangeOrAuthorizeTheSelection()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginPointerCursor cursor = new(layout);
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(Catalog(1, 1));
        int regionX = layout.RegionCells[0].X + 1;
        int regionY = layout.RegionCells[0].Y + 1;

        for (int index = 0; index < 100; index++)
        {
            Assert.Equal(DesktopPointerCursor.Hand, cursor.Resolve(true, regionX, regionY, selection));
        }

        Assert.Null(selection.SelectedRegion);
        Assert.Null(selection.SelectedServer);
    }

    private static DesktopPointerCursor At(LoginPointerCursor cursor, LoginServerSelection selection, LoginSpritePlacement bounds) =>
        cursor.Resolve(true, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, selection);

    private static LoginServerCatalog Catalog(int regionCount, int serversPerRegion) => new(
        Enumerable.Range(0, regionCount).Select(region => new LoginRegion($"region-{region}", $"Region {region}",
            Enumerable.Range(0, serversPerRegion).Select(server => new LoginServer($"realm-{region}-{server}", $"Realm {server}", $"Protocol{region}_{server}")))));
}
