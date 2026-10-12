namespace OpenConquer.Client.UI.Login;

/// <summary>Fail-closed region/server selection with fixed-size pages matching the login grid.</summary>
internal sealed class LoginServerSelection
{
    public const int RegionsPerPage = 16;
    public const int ServersPerPage = 12;

    public LoginServerCatalog Catalog { get; private set; } = LoginServerCatalog.Empty;
    public int RegionPage
    {
        get; private set;
    }
    public int ServerPage
    {
        get; private set;
    }
    public int SelectedRegionIndex { get; private set; } = -1;
    public int SelectedServerIndex { get; private set; } = -1;
    public int RegionPageCount => PageCount(Catalog.Regions.Count, RegionsPerPage);
    public int ServerPageCount => PageCount(SelectedRegion?.Servers.Count ?? 0, ServersPerPage);

    public LoginRegion? SelectedRegion => SelectedRegionIndex >= 0 ? Catalog.Regions[SelectedRegionIndex] : null;
    public LoginServer? SelectedServer => SelectedRegion is { } region && SelectedServerIndex >= 0 ? region.Servers[SelectedServerIndex] : null;

    public void ReplaceCatalog(LoginServerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        Catalog = catalog;
        RegionPage = 0;
        ServerPage = 0;
        SelectedRegionIndex = -1;
        SelectedServerIndex = -1;
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

        int index = ServerPage * ServersPerPage + slot;
        return index < region.Servers.Count ? region.Servers[index] : null;
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
            ServerPage = 0;
        }

        return true;
    }

    public bool SelectServerSlot(int slot)
    {
        if (ServerAtSlot(slot) is null)
        {
            return false;
        }

        SelectedServerIndex = ServerPage * ServersPerPage + slot;
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

        RegionPage = target;
        ServerPage = 0;
        SelectedRegionIndex = -1;
        SelectedServerIndex = -1;
        return true;
    }

    public bool ChangeServerPage(int direction)
    {
        ValidatePageDirection(direction);
        int target = ServerPage + direction;

        if (target < 0 || target >= ServerPageCount)
        {
            return false;
        }

        ServerPage = target;
        SelectedServerIndex = -1;
        return true;
    }

    public int SelectedRegionSlot => SelectedRegionIndex >= 0 && SelectedRegionIndex / RegionsPerPage == RegionPage ? SelectedRegionIndex % RegionsPerPage : -1;
    public int SelectedServerSlot => SelectedServerIndex >= 0 && SelectedServerIndex / ServersPerPage == ServerPage ? SelectedServerIndex % ServersPerPage : -1;

    private static int PageCount(int count, int perPage) => (count + perPage - 1) / perPage;

    private static void ValidatePageDirection(int direction)
    {
        if (direction is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Page direction must be -1 or +1.");
        }
    }
}
