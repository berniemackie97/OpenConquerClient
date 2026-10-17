using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginServerSelectionTests
{
    [Fact]
    public void SelectingAnotherRegion_InvalidatesPreviouslySelectedServer()
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(CreateCatalog(regionCount: 2, serversPerRegion: 2));

        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(1));
        Assert.Equal("region-0", selection.SelectedRegion?.Id);
        Assert.Equal("realm-0-1", selection.SelectedServer?.Id);

        Assert.True(selection.SelectRegionSlot(1));
        Assert.Equal("region-1", selection.SelectedRegion?.Id);
        Assert.Null(selection.SelectedServer);
        Assert.Equal(-1, selection.SelectedServerIndex);
    }

    [Fact]
    public void ReplacingCatalog_InvalidatesAllSelectionsEvenForReusedIds()
    {
        LoginServerSelection selection = new();
        LoginServerCatalog original = CreateCatalog(regionCount: 1, serversPerRegion: 1);
        selection.ReplaceCatalog(original);
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));

        selection.ReplaceCatalog(CreateCatalog(regionCount: 1, serversPerRegion: 1));

        Assert.Equal(-1, selection.SelectedRegionIndex);
        Assert.Equal(-1, selection.SelectedServerIndex);
        Assert.Null(selection.SelectedServer);
    }

    [Fact]
    public void RegionPages_ExposeOnlyPresentRowsAndKeepTheSelectedRegion()
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(CreateCatalog(regionCount: 17, serversPerRegion: 13));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(11));

        Assert.Equal(2, selection.RegionPageCount);
        Assert.True(selection.ChangeRegionPage(1));
        Assert.Equal("region-16", selection.RegionAtSlot(0)?.Id);
        Assert.Null(selection.RegionAtSlot(1));
        Assert.False(selection.SelectRegionSlot(1));
        // sr.flush(page) only reloads the holders; region 0 and its realm stay selected off-page.
        Assert.Equal("region-0", selection.SelectedRegion?.Id);
        Assert.Equal(-1, selection.SelectedRegionSlot);
        Assert.Equal("realm-0-11", selection.SelectedServer?.Id);
        Assert.False(selection.ChangeRegionPage(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => selection.ChangeRegionPage(2));
    }

    [Fact]
    public void ServerGrid_ShowsOnlyTheTwelveHolders()
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(CreateCatalog(regionCount: 1, serversPerRegion: 13));
        Assert.True(selection.SelectRegionSlot(0));

        Assert.Equal("realm-0-11", selection.ServerAtSlot(11)?.Id);
        Assert.Null(selection.ServerAtSlot(12));
        Assert.False(selection.SelectServerSlot(12));
    }

    [Fact]
    public void PointerPress_SelectsLikeTheHitMcPressHandlers()
    {
        LoginServerSelection selection = new();
        LoginSelectionInput input = CreateInput(selection);
        input.ReplaceCatalog(CreateCatalog(regionCount: 2, serversPerRegion: 2));

        Assert.Equal(LoginSelectionEvent.RegionSelected, input.PointerPressed(mapped: true, 100, 200));
        Assert.Equal("region-0", selection.SelectedRegion?.Id);
        Assert.Equal(LoginSelectionEvent.None, input.PointerPressed(mapped: true, 100, 200));

        Assert.Equal(LoginSelectionEvent.ServerPressed, input.PointerPressed(mapped: true, 500, 230));
        Assert.Equal("realm-0-0", selection.SelectedServer?.Id);
        // click_server sounds for every enabled realm press, the selected one included.
        Assert.Equal(LoginSelectionEvent.ServerPressed, input.PointerPressed(mapped: true, 500, 230));

        Assert.Equal(LoginSelectionEvent.RegionSelected, input.PointerPressed(mapped: true, 100, 250));
        Assert.Equal("region-1", selection.SelectedRegion?.Id);
        Assert.Null(selection.SelectedServer);
    }

    [Fact]
    public void EmptyHolders_HaveNoHitArea()
    {
        LoginServerSelection selection = new();
        LoginSelectionInput input = CreateInput(selection);
        input.ReplaceCatalog(CreateCatalog(regionCount: 1, serversPerRegion: 1));
        input.PointerPressed(mapped: true, 100, 200);

        Assert.Equal(LoginSelectionEvent.None, input.PointerPressed(mapped: true, 100, 250));
        Assert.Equal(LoginSelectionEvent.None, input.PointerPressed(mapped: true, 650, 230));
        Assert.Equal(LoginSelectionEvent.None, input.PointerPressed(mapped: false, 500, 230));
        Assert.Null(selection.SelectedServer);
    }

    [Fact]
    public void PageButtons_TurnOnReleaseOverThePressedButton()
    {
        LoginServerSelection selection = new();
        LoginSelectionInput input = CreateInput(selection);
        input.ReplaceCatalog(CreateCatalog(regionCount: 17, serversPerRegion: 1));

        input.PointerMoved(mapped: true, 260, 615);
        Assert.Equal(1, input.PageButtonState(LoginRegionPageButton.Next));
        Assert.Equal(LoginSelectionEvent.PagePressed, input.PointerPressed(mapped: true, 260, 615));
        Assert.Equal(2, input.PageButtonState(LoginRegionPageButton.Next));
        input.PointerMoved(mapped: true, 600, 300);
        Assert.Equal(0, input.PageButtonState(LoginRegionPageButton.Next));
        input.PointerReleased(mapped: true, 600, 300);
        Assert.Equal(0, selection.RegionPage);

        input.PointerPressed(mapped: true, 260, 615);
        input.PointerReleased(mapped: true, 260, 615);
        Assert.Equal(1, selection.RegionPage);

        input.PointerPressed(mapped: true, 150, 615);
        input.PointerReleased(mapped: true, 150, 615);
        Assert.Equal(0, selection.RegionPage);
    }

    [Fact]
    public void PageButtons_ExistOnlyWithMoreThanOnePage()
    {
        LoginServerSelection selection = new();
        LoginSelectionInput input = CreateInput(selection);
        input.ReplaceCatalog(CreateCatalog(regionCount: 16, serversPerRegion: 1));

        Assert.Equal(LoginRegionPageButton.None, input.HitPageButton(260, 615));
        Assert.Equal(LoginSelectionEvent.None, input.PointerPressed(mapped: true, 260, 615));
    }

    [Fact]
    public void FocusLossAndCatalogReplacement_CancelPendingPageCapture()
    {
        LoginServerSelection selection = new();
        LoginSelectionInput input = CreateInput(selection);
        input.ReplaceCatalog(CreateCatalog(regionCount: 17, serversPerRegion: 1));
        input.PointerPressed(mapped: true, 260, 615);
        input.FocusLost();
        input.PointerReleased(mapped: true, 260, 615);
        Assert.Equal(0, selection.RegionPage);

        input.PointerPressed(mapped: true, 260, 615);
        input.ReplaceCatalog(CreateCatalog(regionCount: 17, serversPerRegion: 1));
        input.PointerReleased(mapped: true, 260, 615);
        Assert.Equal(0, selection.RegionPage);
    }

    private static LoginSelectionInput CreateInput(LoginServerSelection selection) =>
        new(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)), selection);

    private static LoginServerCatalog CreateCatalog(int regionCount, int serversPerRegion) => new(
        Enumerable.Range(0, regionCount).Select(region => new LoginRegion($"region-{region}", $"Region {region}",
            Enumerable.Range(0, serversPerRegion).Select(server => new LoginServer($"realm-{region}-{server}", $"Realm {server}", $"Protocol{region}_{server}")))));
}
