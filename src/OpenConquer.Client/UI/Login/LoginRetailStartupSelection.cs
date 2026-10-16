using OpenConquer.Client.Startup.Catalog;

namespace OpenConquer.Client.UI.Login;

/// <summary>Applies the 5517 (0,0) startup choice, but only to a current authenticated catalog.</summary>
internal static class LoginRetailStartupSelection
{
    public static bool TryApply(SignedLoginCatalog? authenticated, DateTimeOffset now, LoginServerSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (authenticated is null || !authenticated.IsCurrent(now) ||
            !ReferenceEquals(selection.Catalog, authenticated.View) || selection.SelectedRegion is not null ||
            !selection.SelectRegionSlot(0))
        {
            return false;
        }

        // The original startup automatically chooses server 0 after resolving region 0.
        // Empty signed groups remain without a server; there is no fabricated endpoint.
        selection.SelectServerSlot(0);
        return true;
    }
}
