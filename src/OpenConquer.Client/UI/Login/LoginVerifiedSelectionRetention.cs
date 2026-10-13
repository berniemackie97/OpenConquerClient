using OpenConquer.Client.Startup.Catalog;

namespace OpenConquer.Client.UI.Login;

/// <summary>Rebinds an explicit realm choice only across fully authenticated, equivalent snapshots.</summary>
internal static class LoginVerifiedSelectionRetention
{
    public static void Apply(SignedLoginCatalog? previous, SignedLoginCatalog? current, DateTimeOffset now, LoginSelectionInput input, LoginServerSelection selection)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(selection);

        bool previouslyAuthenticated = previous is not null && previous.IsCurrent(now) && ReferenceEquals(previous.View, selection.Catalog);
        current = current is not null && current.IsCurrent(now) ? current : null;
        LoginRegion? selectedRegion = previouslyAuthenticated ? selection.SelectedRegion : null;
        LoginServer? selectedServer = previouslyAuthenticated ? selection.SelectedServer : null;

        // Always invalidate pointer capture and stale object references, including on expiry.
        input.ReplaceCatalog(current?.View ?? LoginServerCatalog.Empty);

        if (!previouslyAuthenticated || current is null || selectedRegion is null)
        {
            return;
        }

        int regionIndex = IndexOfRegion(current.View, selectedRegion);
        if (regionIndex < 0)
        {
            return;
        }

        for (int page = 0; page < regionIndex / LoginServerSelection.RegionsPerPage; page++)
        {
            if (!selection.ChangeRegionPage(1))
            {
                throw new InvalidOperationException("A verified region no longer fits its page.");
            }
        }

        if (!selection.SelectRegionSlot(regionIndex % LoginServerSelection.RegionsPerPage) || selectedServer is null)
        {
            return;
        }

        LoginRegion newRegion = selection.SelectedRegion!;
        int serverIndex = IndexOfServer(newRegion, selectedServer);
        if (serverIndex < 0 ||
            !previous!.TryGetAccountEndpoint(selectedRegion, selectedServer, out LoginAccountEndpoint? oldEndpoint) ||
            !current.TryGetAccountEndpoint(newRegion, newRegion.Servers[serverIndex], out LoginAccountEndpoint? newEndpoint) ||
            oldEndpoint is null || newEndpoint is null ||
            !string.Equals(oldEndpoint.Host, newEndpoint.Host, StringComparison.Ordinal) || oldEndpoint.Port != newEndpoint.Port)
        {
            return;
        }

        for (int page = 0; page < serverIndex / LoginServerSelection.ServersPerPage; page++)
        {
            if (!selection.ChangeServerPage(1))
            {
                throw new InvalidOperationException("A verified server no longer fits its page.");
            }
        }

        if (!selection.SelectServerSlot(serverIndex % LoginServerSelection.ServersPerPage))
        {
            throw new InvalidOperationException("A verified server disappeared during selection rebind.");
        }
    }

    private static int IndexOfRegion(LoginServerCatalog catalog, LoginRegion oldRegion)
    {
        for (int index = 0; index < catalog.Regions.Count; index++)
        {
            LoginRegion region = catalog.Regions[index];
            if (string.Equals(region.Id, oldRegion.Id, StringComparison.Ordinal))
            {
                return string.Equals(region.DisplayName, oldRegion.DisplayName, StringComparison.Ordinal) ? index : -1;
            }
        }

        return -1;
    }

    private static int IndexOfServer(LoginRegion region, LoginServer oldServer)
    {
        for (int index = 0; index < region.Servers.Count; index++)
        {
            LoginServer server = region.Servers[index];
            if (string.Equals(server.Id, oldServer.Id, StringComparison.Ordinal))
            {
                return string.Equals(server.DisplayName, oldServer.DisplayName, StringComparison.Ordinal) &&
                    string.Equals(server.ProtocolServerName, oldServer.ProtocolServerName, StringComparison.Ordinal) &&
                    string.Equals(server.IconId, oldServer.IconId, StringComparison.Ordinal) ? index : -1;
            }
        }

        return -1;
    }
}
