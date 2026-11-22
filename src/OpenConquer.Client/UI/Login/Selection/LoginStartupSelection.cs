using OpenConquer.Client.Startup.Catalog;

namespace OpenConquer.Client.UI.Login.Selection;

internal static class LoginStartupSelection
{
    public static bool TryApply(SignedLoginCatalog? authenticated, DateTimeOffset now, LoginServerSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (authenticated is null || !authenticated.IsCurrent(now) || !ReferenceEquals(selection.Catalog, authenticated.View)
            || selection.SelectedRegion is not null || !selection.SelectRegionSlot(0))
        {
            return false;
        }

        selection.SelectServerSlot(0);
        return true;
    }
}
