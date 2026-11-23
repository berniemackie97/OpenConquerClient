using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Selection;

internal enum LoginSelectionEvent
{
    None,
    RegionSelected,
    ServerPressed,
    PagePressed,
}

internal sealed class LoginSelectionInput(LoginStageLayout layout, LoginServerSelection selection)
{
    private readonly LoginStageLayout _layout = layout ?? throw new ArgumentNullException(nameof(layout));
    private readonly LoginServerSelection _selection = selection ?? throw new ArgumentNullException(nameof(selection));
    private readonly LoginButtonSet<LoginButton> _pages = new([
        (LoginButton.RegionPagePrevious, layout.RegionPagePrevious),
        (LoginButton.RegionPageNext, layout.RegionPageNext),
    ]);
    private LoginServerCatalog? _pressedCatalog;

    public bool PagesVisible => _selection.RegionPageCount > 1;

    public LoginButtonState PageButtonState(LoginButton button)
    {
        return _pages.StateOf(button);
    }

    public void ReplaceCatalog(LoginServerCatalog catalog)
    {
        CancelPointerCapture();
        _selection.ReplaceCatalog(catalog);
    }

    public LoginButton? PointerMoved(bool mapped, int x, int y)
    {
        return _pages.PointerMoved(PagesVisible && mapped, x, y);
    }

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

        int region = LoginStageLayout.HitSlot(_layout.RegionHitAreas, x, y);
        if (region >= 0 && _selection.RegionAtSlot(region) is not null)
        {
            bool changed = region != _selection.SelectedRegionSlot;
            _selection.SelectRegionSlot(region);
            return changed ? LoginSelectionEvent.RegionSelected : LoginSelectionEvent.None;
        }

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

        if (clicked is { } page && ReferenceEquals(catalog, _selection.Catalog))
        {
            _selection.ChangeRegionPage(page == LoginButton.RegionPagePrevious ? -1 : 1);
        }
    }

    public void FocusLost()
    {
        CancelPointerCapture();
    }

    private void CancelPointerCapture()
    {
        _pages.Reset();
        _pressedCatalog = null;
    }
}
