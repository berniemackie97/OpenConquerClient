using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login.Selection;

namespace OpenConquer.Client.UI.Login;

/// <summary>
/// Feeds the authenticated realm catalog to the login screen. Without a configured authority there is no
/// catalog, and the screen shows no regions.
/// </summary>
internal sealed class LoginCatalogBinding : IDisposable
{
    private readonly LoginCatalogRefresh? _refresh;
    private SignedLoginCatalog? _presented;
    private bool _startupSelectionApplied;
    private bool _unavailableReported;

    private LoginCatalogBinding(LoginCatalogRefresh? refresh) => _refresh = refresh;

    /// <summary>Binds to the catalog authority embedded at build time, if there is one.</summary>
    public static LoginCatalogBinding ForEmbeddedAuthority()
    {
        if (LoginCatalogAuthority.LoadEmbedded() is not { } authority)
        {
            Console.Error.WriteLine("OpenConquer: realm catalog authority is not configured; no servers are available.");
            return new LoginCatalogBinding(refresh: null);
        }

        return new LoginCatalogBinding(new LoginCatalogRefresh(token => FetchAsync(authority, token)));
    }

    public LoginDirectoryAvailability Availability(LoginServerSelection selection) =>
        LoginDirectoryStatus.Resolve(_refresh is not null, _refresh?.Current is not null, _refresh?.LastAttemptFailed ?? false, selection);

    /// <summary>Hands a newly accepted catalog, or the loss of one, to the screen.</summary>
    public void Update(LoginScreen screen, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(screen);

        if (_refresh is null)
        {
            return;
        }

        if (_refresh.Advance(now))
        {
            SignedLoginCatalog? incoming = _refresh.Current;
            // GameSetUp.ini seeds (GroupRecord, ServerRecord) = (0, 0); only the first current catalog takes it.
            bool firstCurrent = !_startupSelectionApplied && incoming is not null && incoming.IsCurrent(now);
            _startupSelectionApplied |= firstCurrent;
            screen.ApplyCatalog(_presented, incoming, now, firstCurrent);
            _presented = incoming;
        }

        if (_refresh.LastAttemptFailed && !_unavailableReported)
        {
            Console.Error.WriteLine("OpenConquer: authenticated realm discovery is unavailable or rejected.");
        }

        _unavailableReported = _refresh.LastAttemptFailed;
    }

    public void Cancel() => _refresh?.Cancel();

    public void Dispose() => _refresh?.Dispose();

    private static async Task<SignedLoginCatalog?> FetchAsync(LoginCatalogAuthority authority, CancellationToken token)
    {
        using LoginCatalogClient client = new();
        return await client.FetchAsync(authority, DateTimeOffset.UtcNow, token).ConfigureAwait(false);
    }
}
