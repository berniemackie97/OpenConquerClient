using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Selection;

/// <summary>Region/server pointer outcomes that carry a <c>flashLogin.ini [serverSelection]</c> sound.</summary>
internal enum LoginSelectionEvent
{
    None,
    /// <summary>A different region was chosen (<c>dragonMov</c>; <c>click_region</c> is empty).</summary>
    RegionSelected,
    /// <summary>An enabled server was pressed, selected or not (<c>click_server</c>).</summary>
    ServerPressed,
    /// <summary>A region page button was pressed (<c>click_leftPage</c> / <c>click_rightPage</c>).</summary>
    PagePressed,
}

/// <summary>
/// Region and server tiles select on press through their hit areas; region pages turn on release. The login
/// movie has no keyboard path into either list.
/// </summary>
internal sealed class LoginSelectionInput
{
    private readonly LoginStageLayout _layout;
    private readonly LoginServerSelection _selection;
    private readonly LoginButtonSet<LoginButton> _pages;
    private LoginServerCatalog? _pressedCatalog;

    public LoginSelectionInput(LoginStageLayout layout, LoginServerSelection selection)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _pages = new LoginButtonSet<LoginButton>([
            (LoginButton.RegionPagePrevious, layout.RegionPagePrevious),
            (LoginButton.RegionPageNext, layout.RegionPageNext),
        ]);
    }

    /// <summary>leftBtn and rightBtn are hidden unless the regions span more than one page.</summary>
    public bool PagesVisible => _selection.RegionPageCount > 1;

    public LoginButtonState PageButtonState(LoginButton button) => _pages.StateOf(button);

    public void ReplaceCatalog(LoginServerCatalog catalog)
    {
        CancelPointerCapture();
        _selection.ReplaceCatalog(catalog);
    }

    /// <summary>Tracks the pointer and returns the page button it just moved onto, if any.</summary>
    public LoginButton? PointerMoved(bool mapped, int x, int y) => _pages.PointerMoved(PagesVisible && mapped, x, y);

    public LoginSelectionEvent PointerPressed(bool mapped, int x, int y)
    {
        CancelPointerCapture();

        if (!mapped)
        {
            return LoginSelectionEvent.None;
        }

        if (PagesVisible && _pages.PointerPressed(mapped, x, y) is not null)
        {
            _pressedCatalog = _selection.Catalog;
            return LoginSelectionEvent.PagePressed;
        }

        // Empty region tiles (unopenedR) have no hit area.
        int region = LoginStageLayout.HitSlot(_layout.RegionHitAreas, x, y);
        if (region >= 0 && _selection.RegionAtSlot(region) is not null)
        {
            bool changed = region != _selection.SelectedRegionSlot;
            _selection.SelectRegionSlot(region);
            return changed ? LoginSelectionEvent.RegionSelected : LoginSelectionEvent.None;
        }

        // ss.flush disables the hit area of an empty server tile.
        int server = LoginStageLayout.HitSlot(_layout.ServerHitAreas, x, y);
        if (server >= 0 && _selection.SelectServerSlot(server))
        {
            return LoginSelectionEvent.ServerPressed;
        }

        return LoginSelectionEvent.None;
    }

    public void PointerReleased(bool mapped, int x, int y)
    {
        LoginServerCatalog? catalog = _pressedCatalog;
        _pressedCatalog = null;
        LoginButton? clicked = _pages.PointerReleased(PagesVisible && mapped, x, y);

        // A page turns only within the catalog it was pressed on; neither end wraps.
        if (clicked is { } page && ReferenceEquals(catalog, _selection.Catalog))
        {
            _selection.ChangeRegionPage(page == LoginButton.RegionPagePrevious ? -1 : 1);
        }
    }

    public void FocusLost() => CancelPointerCapture();

    private void CancelPointerCapture()
    {
        _pages.Reset();
        _pressedCatalog = null;
    }
}
