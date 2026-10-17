using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Startup.Catalog;

/// <summary>
/// Only opens account transport for a selected realm in a still-current signed catalog.
/// The returned session owns its TCP connection; callers must dispose it.
/// </summary>
internal static class LoginSignedAccountSession
{
    internal static Task<LoginAccountSession> OpenAsync(
        SignedLoginCatalog? catalog, LoginServerSelection selection, DateTimeOffset now,
        LoginAccountTimeouts timeouts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (catalog is null || !catalog.IsCurrent(now) ||
            !ReferenceEquals(selection.Catalog, catalog.View) ||
            selection.SelectedRegion is not { } region ||
            selection.SelectedServer is not { } server ||
            !catalog.TryGetAccountEndpoint(region, server, out LoginAccountEndpoint? endpoint) ||
            endpoint is null)
        {
            throw new InvalidOperationException("No current authenticated AccountServer selection is available.");
        }

        return LoginAccountSession.OpenAsync(endpoint.Host, endpoint.Port, timeouts, cancellationToken);
    }
}
