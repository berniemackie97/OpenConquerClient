using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Login;

internal enum LoginSelectionFocus
{
    None,
    Region,
    Server,
}

/// <summary>Owns the region/server pointer gesture and keyboard-navigation boundary.</summary>
internal sealed class LoginSelectionInput
{
    private readonly LoginVisualLayout _layout;
    private readonly LoginServerSelection _selection;
    private LoginSelectionFocus _pressedArea;
    private int _pressedSlot = -1;
    private int _pressedPage = -1;
    private LoginServerCatalog? _pressedCatalog;
    private LoginRegion? _pressedRegion;

    public LoginSelectionFocus Focus
    {
        get; private set;
    }
    public int FocusedRegionSlot { get; private set; } = -1;
    public int FocusedServerSlot { get; private set; } = -1;

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
        Focus = LoginSelectionFocus.None;
        FocusedRegionSlot = -1;
        FocusedServerSlot = -1;
    }

    public void PointerPressed(bool mapped, int x, int y)
    {
        CancelPointerCapture();

        if (!mapped)
        {
            return;
        }

        if (TryHit(_layout.RegionCells, x, y, out int regionSlot) && _selection.RegionAtSlot(regionSlot) is not null)
        {
            FocusRegion(regionSlot);
            Capture(LoginSelectionFocus.Region, regionSlot, _selection.RegionPage);
            return;
        }

        if (TryHit(_layout.ServerCells, x, y, out int serverSlot) && _selection.ServerAtSlot(serverSlot) is not null)
        {
            FocusServer(serverSlot);
            Capture(LoginSelectionFocus.Server, serverSlot, _selection.ServerPage);
        }
    }

    public void PointerReleased(bool mapped, int x, int y)
    {
        LoginSelectionFocus area = _pressedArea;
        int slot = _pressedSlot;
        int page = _pressedPage;
        LoginServerCatalog? catalog = _pressedCatalog;
        LoginRegion? region = _pressedRegion;
        CancelPointerCapture();

        if (!mapped || area == LoginSelectionFocus.None || !ReferenceEquals(catalog, _selection.Catalog))
        {
            return;
        }

        if (area == LoginSelectionFocus.Region && page == _selection.RegionPage && TryHit(_layout.RegionCells, x, y, out int releasedRegionSlot) && slot == releasedRegionSlot)
        {
            _selection.SelectRegionSlot(slot);
            FocusedServerSlot = -1;
        }
        else if (area == LoginSelectionFocus.Server && ReferenceEquals(region, _selection.SelectedRegion) && page == _selection.ServerPage && TryHit(_layout.ServerCells, x, y, out int releasedServerSlot) && slot == releasedServerSlot)
        {
            _selection.SelectServerSlot(slot);
        }
    }

    public void FocusLost()
    {
        CancelPointerCapture();
        Focus = LoginSelectionFocus.None;
        FocusedRegionSlot = -1;
        FocusedServerSlot = -1;
    }

    public void KeyPressed(DesktopKeyEvent key)
    {
        if ((key.Modifiers & (DesktopKeyModifiers.Control | DesktopKeyModifiers.Alt | DesktopKeyModifiers.Super)) != 0)
        {
            return;
        }

        CancelPointerCapture();

        if (key.Key == DesktopKey.Escape)
        {
            FocusLost();
            return;
        }

        if (key.Key == DesktopKey.Tab)
        {
            if (!key.IsRepeat)
            {
                AdvanceFocus((key.Modifiers & DesktopKeyModifiers.Shift) != 0);
            }
            return;
        }

        if (Focus == LoginSelectionFocus.None)
        {
            return;
        }

        if (key.Key == DesktopKey.Enter)
        {
            if (!key.IsRepeat)
            {
                if (Focus == LoginSelectionFocus.Region && _selection.SelectRegionSlot(FocusedRegionSlot))
                {
                    FocusedServerSlot = -1;
                }
                else if (Focus == LoginSelectionFocus.Server)
                {
                    _selection.SelectServerSlot(FocusedServerSlot);
                }
            }
            return;
        }

        if (key.Key is DesktopKey.PageUp or DesktopKey.PageDown)
        {
            int direction = key.Key == DesktopKey.PageUp ? -1 : 1;
            bool changed = Focus == LoginSelectionFocus.Region ? _selection.ChangeRegionPage(direction) : _selection.ChangeServerPage(direction);

            if (changed)
            {
                if (Focus == LoginSelectionFocus.Region)
                {
                    FocusRegion(0);
                }
                else
                {
                    FocusServer(0);
                }
            }
            return;
        }

        int columns = Focus == LoginSelectionFocus.Region ? 2 : 3;
        int current = Focus == LoginSelectionFocus.Region ? FocusedRegionSlot : FocusedServerSlot;
        int delta = key.Key switch
        {
            DesktopKey.Left => -1,
            DesktopKey.Right => 1,
            DesktopKey.Up => -columns,
            DesktopKey.Down => columns,
            _ => 0,
        };

        if (delta == 0 || current < 0 ||
            (key.Key == DesktopKey.Left && current % columns == 0) ||
            (key.Key == DesktopKey.Right && current % columns == columns - 1))
        {
            return;
        }

        int target = current + delta;
        bool available = Focus == LoginSelectionFocus.Region ? _selection.RegionAtSlot(target) is not null : _selection.ServerAtSlot(target) is not null;

        if (!available)
        {
            return;
        }

        if (Focus == LoginSelectionFocus.Region)
        {
            FocusRegion(target);
        }
        else
        {
            FocusServer(target);
        }
    }

    private void AdvanceFocus(bool reverse)
    {
        if (_selection.RegionAtSlot(0) is null && _selection.Catalog.Regions.Count == 0)
        {
            FocusLost();
            return;
        }

        bool hasServers = _selection.SelectedRegion is { Servers.Count: > 0 };
        bool toServer = hasServers && (reverse ? Focus != LoginSelectionFocus.Server : Focus == LoginSelectionFocus.Region);

        if (toServer)
        {
            FocusServer(_selection.SelectedServerSlot >= 0 ? _selection.SelectedServerSlot : 0);
        }
        else
        {
            FocusRegion(_selection.SelectedRegionSlot >= 0 ? _selection.SelectedRegionSlot : 0);
        }
    }

    private void FocusRegion(int slot)
    {
        if (_selection.RegionAtSlot(slot) is null)
        {
            return;
        }

        Focus = LoginSelectionFocus.Region;
        FocusedRegionSlot = slot;
    }

    private void FocusServer(int slot)
    {
        if (_selection.ServerAtSlot(slot) is null)
        {
            return;
        }

        Focus = LoginSelectionFocus.Server;
        FocusedServerSlot = slot;
    }

    private void Capture(LoginSelectionFocus area, int slot, int page)
    {
        _pressedArea = area;
        _pressedSlot = slot;
        _pressedPage = page;
        _pressedCatalog = _selection.Catalog;
        _pressedRegion = _selection.SelectedRegion;
    }

    private void CancelPointerCapture()
    {
        _pressedArea = LoginSelectionFocus.None;
        _pressedSlot = -1;
        _pressedPage = -1;
        _pressedCatalog = null;
        _pressedRegion = null;
    }

    private static bool TryHit(ReadOnlySpan<LoginSpritePlacement> placements, int x, int y, out int slot)
    {
        for (int index = 0; index < placements.Length; index++)
        {
            LoginSpritePlacement placement = placements[index];

            if (x >= placement.X && y >= placement.Y && (long)x < (long)placement.X + placement.Width && (long)y < (long)placement.Y + placement.Height)
            {
                slot = index;
                return true;
            }
        }

        slot = -1;
        return false;
    }
}
