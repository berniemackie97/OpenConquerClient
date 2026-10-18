using System.Text;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Startup.Catalog;

/// <summary>
/// Binds a single AccountServer exchange to the currently verified signed realm catalog.
/// Offline/loopback-only until native credential submission has an authenticated remote peer.
/// </summary>
internal static class LoginSignedAccountExchange
{
    internal static async Task<LoginAccountExchangeResult> AuthenticateAsync(
        SignedLoginCatalog? catalog, LoginServerSelection selection,
        ReadOnlyMemory<byte> accountName, ReadOnlyMemory<char> password,
        LoginAccountTimeouts timeouts, TimeProvider clock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(clock);
        timeouts.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        LoginRegion? region = selection.SelectedRegion;
        LoginServer? server = selection.SelectedServer;
        long revision = selection.Revision;
        if (!IsAuthorized(catalog, selection, region, server, revision, clock))
        {
            throw new InvalidOperationException("No current authenticated AccountServer selection is available.");
        }

        // The native cipher is NOT authenticated encryption. Explicitly disallow real remote
        // password submission while keeping this bounded exchange testable with a local peer.
        if (!catalog!.TryGetAccountEndpoint(region!, server!, out LoginAccountEndpoint? endpoint) ||
            endpoint is null || endpoint.Host != "127.0.0.1")
        {
            throw new InvalidOperationException("AccountServer credential exchange is restricted to IPv4 loopback.");
        }

        LoginAccountSession session = await LoginSignedAccountSession.OpenAsync(
            catalog, selection, clock.GetUtcNow(), timeouts, cancellationToken).ConfigureAwait(false);

        try
        {
            byte[] protocolName = Encoding.ASCII.GetBytes(server!.ProtocolServerName);
            LoginAccountResponse1055 response = await session.AuthenticateAsync(
                accountName, password, protocolName,
                () => IsAuthorized(catalog, selection, region, server, revision, clock),
                timeouts.Response, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccess)
            {
                await session.DisposeAsync().ConfigureAwait(false);
                return LoginAccountExchangeResult.Failure(response);
            }

            return LoginAccountExchangeResult.Success(response, session);
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static bool IsAuthorized(SignedLoginCatalog? catalog, LoginServerSelection selection,
        LoginRegion? region, LoginServer? server, long revision, TimeProvider clock)
    {
        return catalog is not null && catalog.IsCurrent(clock.GetUtcNow()) &&
            ReferenceEquals(selection.Catalog, catalog.View) &&
            selection.Revision == revision &&
            region is not null && server is not null &&
            ReferenceEquals(selection.SelectedRegion, region) &&
            ReferenceEquals(selection.SelectedServer, server) &&
            catalog.TryGetAccountEndpoint(region, server, out _);
    }
}
