namespace OpenConquer.Client.UI.Login;

internal enum LoginRegionPageButton
{
    None,
    Previous,
    Next,
}

/// <summary>Region/server pointer outcomes that carry a retail <c>flashLogin.ini [serverSelection]</c> sound.</summary>
internal enum LoginSelectionEvent
{
    None,
    /// <summary><c>sr.onPressItem</c> for a different region (<c>dragonMov</c>).</summary>
    RegionSelected,
    /// <summary><c>ss</c> hitMC press on an enabled realm, selected or not (<c>click_server</c>).</summary>
    ServerPressed,
    /// <summary>leftBtn/rightBtn press (<c>click_leftPage</c>/<c>click_rightPage</c>).</summary>
    PagePressed,
}

/// <summary>
/// Retail sprite 259/180 pointer handling: region and server holders select on press through their
/// <c>hitMC</c>, region pages turn on release. The login movie has no keyboard path into either list.
/// </summary>
internal sealed class LoginSelectionInput
{
    private const int ButtonUp = 0;
    private const int ButtonOver = 1;
    private const int ButtonDown = 2;

    private readonly LoginVisualLayout _layout;
    private readonly LoginServerSelection _selection;
    private LoginRegionPageButton _pressedPageButton;
    private LoginServerCatalog? _pressedCatalog;
    private bool _pointerMapped;
    private int _pointerX;
    private int _pointerY;

    public LoginSelectionInput(LoginVisualLayout layout, LoginServerSelection selection)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(selection);
        _layout = layout;
        _selection = selection;
    }

    public void ReplaceCatalog(LoginServerCatalog catalog)
    {
        CancelPointerCapture();
        _selection.ReplaceCatalog(catalog);
    }

    public void PointerMoved(bool mapped, int x, int y)
    {
        _pointerMapped = mapped;
        _pointerX = x;
        _pointerY = y;
    }

    public LoginSelectionEvent PointerPressed(bool mapped, int x, int y)
    {
        CancelPointerCapture();
        PointerMoved(mapped, x, y);

        if (!mapped)
        {
            return LoginSelectionEvent.None;
        }

        LoginRegionPageButton page = HitPageButton(x, y);
        if (page != LoginRegionPageButton.None)
        {
            _pressedPageButton = page;
            _pressedCatalog = _selection.Catalog;
            return LoginSelectionEvent.PagePressed;
        }

        // unopenedR has no hitMC, so only populated region holders take the press.
        int regionSlot = LoginVisualLayout.HitSlot(_layout.RegionHitAreas, x, y);
        if (regionSlot >= 0 && _selection.RegionAtSlot(regionSlot) is not null)
        {
            bool changed = regionSlot != _selection.SelectedRegionSlot;
            _selection.SelectRegionSlot(regionSlot);
            return changed ? LoginSelectionEvent.RegionSelected : LoginSelectionEvent.None;
        }

        // `ss.flush` disables the hitMC of holders without a realm.
        int serverSlot = LoginVisualLayout.HitSlot(_layout.ServerHitAreas, x, y);
        if (serverSlot >= 0 && _selection.ServerAtSlot(serverSlot) is not null)
        {
            _selection.SelectServerSlot(serverSlot);
            return LoginSelectionEvent.ServerPressed;
        }

        return LoginSelectionEvent.None;
    }

    public void PointerReleased(bool mapped, int x, int y)
    {
        LoginRegionPageButton pressed = _pressedPageButton;
        LoginServerCatalog? catalog = _pressedCatalog;
        CancelPointerCapture();
        PointerMoved(mapped, x, y);

        if (pressed == LoginRegionPageButton.None || !mapped || !ReferenceEquals(catalog, _selection.Catalog) ||
            HitPageButton(x, y) != pressed)
        {
            return;
        }

        // leftBtn/rightBtn.onRelease: step within 1..page count; nothing happens at either end.
        _selection.ChangeRegionPage(pressed == LoginRegionPageButton.Previous ? -1 : 1);
    }

    /// <summary>DefineButton2 Up/Over/Down state index for a visible page button.</summary>
    public int PageButtonState(LoginRegionPageButton button)
    {
        bool over = _pointerMapped && HitPageButton(_pointerX, _pointerY) == button;
        if (_pressedPageButton == button)
        {
            return over ? ButtonDown : ButtonUp;
        }

        return over && _pressedPageButton == LoginRegionPageButton.None ? ButtonOver : ButtonUp;
    }

    public LoginRegionPageButton HitPageButton(int x, int y)
    {
        if (_selection.RegionPageCount <= 1)
        {
            return LoginRegionPageButton.None;
        }

        return LoginVisualLayout.Contains(_layout.RegionPagePrevious, x, y) ? LoginRegionPageButton.Previous
            : LoginVisualLayout.Contains(_layout.RegionPageNext, x, y) ? LoginRegionPageButton.Next
            : LoginRegionPageButton.None;
    }

    public void FocusLost()
    {
        CancelPointerCapture();
        _pointerMapped = false;
    }

    private void CancelPointerCapture()
    {
        _pressedPageButton = LoginRegionPageButton.None;
        _pressedCatalog = null;
    }
}
