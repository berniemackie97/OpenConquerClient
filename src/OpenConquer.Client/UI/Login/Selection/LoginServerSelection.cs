namespace OpenConquer.Client.UI.Login.Selection;

/// <summary>
/// Region and server selection over the retail grids: sixteen region slots per page, and the twelve server slots
/// of the selected region (sprite 180 has no server paging). Nothing is selected until the player picks, and a
/// new catalog clears the selection.
/// </summary>
internal sealed class LoginServerSelection
{
    public const int RegionsPerPage = 16;
    public const int ServersPerPage = 12;

    public LoginServerCatalog Catalog { get; private set; } = LoginServerCatalog.Empty;
    public int RegionPage
    {
        get; private set;
    }
    public int SelectedRegionIndex { get; private set; } = -1;
    public int SelectedServerIndex { get; private set; } = -1;
    internal long Revision
    {
        get; private set;
    }
    public int RegionPageCount => PageCount(Catalog.Regions.Count, RegionsPerPage);

    public LoginRegion? SelectedRegion => SelectedRegionIndex >= 0 ? Catalog.Regions[SelectedRegionIndex] : null;
    public LoginServer? SelectedServer => SelectedRegion is { } region && SelectedServerIndex >= 0 ? region.Servers[SelectedServerIndex] : null;

    public void ReplaceCatalog(LoginServerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        Catalog = catalog;
        RegionPage = 0;
        SelectedRegionIndex = -1;
        SelectedServerIndex = -1;
        Revision++;
    }

    public LoginRegion? RegionAtSlot(int slot)
    {
        if ((uint)slot >= RegionsPerPage)
        {
            return null;
        }

        int index = RegionPage * RegionsPerPage + slot;
        return index < Catalog.Regions.Count ? Catalog.Regions[index] : null;
    }

    public LoginServer? ServerAtSlot(int slot)
    {
        if ((uint)slot >= ServersPerPage || SelectedRegion is not { } region)
        {
            return null;
        }

        return slot < region.Servers.Count ? region.Servers[slot] : null;
    }

    public bool SelectRegionSlot(int slot)
    {
        if (RegionAtSlot(slot) is null)
        {
            return false;
        }

        int index = RegionPage * RegionsPerPage + slot;

        if (SelectedRegionIndex != index)
        {
            SelectedRegionIndex = index;
            SelectedServerIndex = -1;
            Revision++;
        }

        return true;
    }

    public bool SelectServerSlot(int slot)
    {
        if (ServerAtSlot(slot) is null)
        {
            return false;
        }

        if (SelectedServerIndex != slot)
        {
            SelectedServerIndex = slot;
            Revision++;
        }

        return true;
    }

    public bool ChangeRegionPage(int direction)
    {
        ValidatePageDirection(direction);
        int target = RegionPage + direction;

        if (target < 0 || target >= RegionPageCount)
        {
            return false;
        }

        // `sr.flush(page)` reloads the holder movies only; the selected region and its servers stay.
        RegionPage = target;
        return true;
    }

    public int SelectedRegionSlot => SelectedRegionIndex >= 0 && SelectedRegionIndex / RegionsPerPage == RegionPage ? SelectedRegionIndex % RegionsPerPage : -1;
    public int SelectedServerSlot => SelectedServerIndex;

    private static int PageCount(int count, int perPage) => (count + perPage - 1) / perPage;

    private static void ValidatePageDirection(int direction)
    {
        if (direction is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Page direction must be -1 or +1.");
        }
    }
}
