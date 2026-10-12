using OpenConquer.Client.UI.Login;
using OpenConquer.Platform.Windowing.Desktop;
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
    public void Pages_ExposeOnlyPresentRowsAndRejectOutOfRangeSelections()
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(CreateCatalog(regionCount: 17, serversPerRegion: 13));

        Assert.Equal(2, selection.RegionPageCount);
        Assert.True(selection.ChangeRegionPage(1));
        Assert.NotNull(selection.RegionAtSlot(0));
        Assert.Null(selection.RegionAtSlot(1));
        Assert.False(selection.SelectRegionSlot(1));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.Equal("region-16", selection.SelectedRegion?.Id);

        Assert.Equal(2, selection.ServerPageCount);
        Assert.True(selection.ChangeServerPage(1));
        Assert.True(selection.SelectServerSlot(0));
        Assert.Equal("realm-16-12", selection.SelectedServer?.Id);
        Assert.False(selection.SelectServerSlot(1));
        Assert.False(selection.ChangeServerPage(1));
        Assert.True(selection.ChangeServerPage(-1));
        Assert.Null(selection.SelectedServer);
        Assert.Equal(-1, selection.SelectedServerSlot);
        Assert.Throws<ArgumentOutOfRangeException>(() => selection.ChangeServerPage(2));
    }

    [Fact]
    public void PointerSelection_RequiresPressAndReleaseOnSameOccupiedCell()
    {
        LoginServerSelection selection = new();
        LoginSelectionInput input = CreateInput(selection);
        input.ReplaceCatalog(CreateCatalog(regionCount: 1, serversPerRegion: 2));

        input.PointerPressed(mapped: true, 60, 200);
        input.PointerReleased(mapped: true, 300, 300);
        Assert.Null(selection.SelectedRegion);

        input.PointerPressed(mapped: true, 60, 200);
        input.PointerReleased(mapped: true, 60, 200);
        Assert.Equal("region-0", selection.SelectedRegion?.Id);

        input.PointerPressed(mapped: true, 440, 225);
        input.PointerReleased(mapped: false, 440, 225);
        Assert.Null(selection.SelectedServer);

        input.PointerPressed(mapped: true, 440, 225);
        input.PointerReleased(mapped: true, 440, 225);
        Assert.Equal("realm-0-0", selection.SelectedServer?.Id);
    }

    [Fact]
    public void FocusLossAndCatalogReplacement_CancelPendingPointerCapture()
    {
        LoginServerSelection selection = new();
        LoginSelectionInput input = CreateInput(selection);
        input.ReplaceCatalog(CreateCatalog(regionCount: 1, serversPerRegion: 1));
        input.PointerPressed(mapped: true, 60, 200);
        input.FocusLost();
        input.PointerReleased(mapped: true, 60, 200);
        Assert.Null(selection.SelectedRegion);

        input.PointerPressed(mapped: true, 60, 200);
        input.ReplaceCatalog(CreateCatalog(regionCount: 1, serversPerRegion: 1));
        input.PointerReleased(mapped: true, 60, 200);
        Assert.Null(selection.SelectedRegion);
    }

    [Fact]
    public void KeyboardNavigation_SelectsOnlyExplicitlyFocusedValidCatalogEntries()
    {
        LoginServerSelection selection = new();
        LoginSelectionInput input = CreateInput(selection);
        input.ReplaceCatalog(CreateCatalog(regionCount: 2, serversPerRegion: 2));

        Key(input, DesktopKey.Enter);
        Assert.Null(selection.SelectedRegion);
        Key(input, DesktopKey.Tab);
        Assert.Equal(LoginSelectionFocus.Region, input.Focus);
        Key(input, DesktopKey.Right);
        Assert.Equal(1, input.FocusedRegionSlot);
        Key(input, DesktopKey.Enter);
        Assert.Equal("region-1", selection.SelectedRegion?.Id);

        Key(input, DesktopKey.Tab);
        Assert.Equal(LoginSelectionFocus.Server, input.Focus);
        Key(input, DesktopKey.Right);
        Key(input, DesktopKey.Enter);
        Assert.Equal("realm-1-1", selection.SelectedServer?.Id);

        Key(input, DesktopKey.Escape);
        Assert.Equal(LoginSelectionFocus.None, input.Focus);
        Assert.Equal("realm-1-1", selection.SelectedServer?.Id);
    }

    [Fact]
    public void KeyboardWithEmptyCatalog_CannotCreateSelectionOrFocus()
    {
        LoginServerSelection selection = new();
        LoginSelectionInput input = CreateInput(selection);

        Key(input, DesktopKey.Tab);
        Key(input, DesktopKey.Enter);
        Key(input, DesktopKey.PageDown);
        Assert.Null(selection.SelectedRegion);
        Assert.Null(selection.SelectedServer);
        Assert.Equal(LoginSelectionFocus.None, input.Focus);
    }

    private static LoginSelectionInput CreateInput(LoginServerSelection selection) =>
        new(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)), selection);

    private static void Key(LoginSelectionInput input, DesktopKey key) =>
        input.KeyPressed(new DesktopKeyEvent(key, DesktopKeyModifiers.None, IsRepeat: false));

    private static LoginServerCatalog CreateCatalog(int regionCount, int serversPerRegion) => new(
        Enumerable.Range(0, regionCount).Select(region => new LoginRegion($"region-{region}", $"Region {region}",
            Enumerable.Range(0, serversPerRegion).Select(server => new LoginServer($"realm-{region}-{server}", $"Realm {server}", $"Protocol{region}_{server}")))));
}
