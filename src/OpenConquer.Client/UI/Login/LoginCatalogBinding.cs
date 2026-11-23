using OpenConquer.Client.Diagnostics;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login.Selection;

namespace OpenConquer.Client.UI.Login;

internal sealed class LoginCatalogBinding : IDisposable
{
    private readonly LoginCatalogRefresh? _refresh;
    private readonly ClientDiagnostics _diagnostics;
    private SignedLoginCatalog? _presented;
    private bool _startupSelectionApplied;
    private bool _unavailableReported;

    internal LoginCatalogBinding(LoginCatalogRefresh? refresh, ClientDiagnostics? diagnostics = null)
    {
        _refresh = refresh;
        _diagnostics = diagnostics ?? ClientDiagnostics.Silent;
    }

    public static LoginCatalogBinding ForEmbeddedAuthority(TimeProvider clock, ClientDiagnostics diagnostics)
    {
        if (LoginCatalogAuthority.LoadEmbedded() is { } authority)
        {
            return new LoginCatalogBinding(new LoginCatalogRefresh(token => FetchAsync(authority, clock, token)), diagnostics);
        }

        diagnostics.CatalogUnconfigured();
        return new LoginCatalogBinding(refresh: null, diagnostics);

    }

    internal SignedLoginCatalog? CurrentCatalog(DateTimeOffset now)
    {
        SignedLoginCatalog? current = _refresh?.Current;
        return current is not null && ReferenceEquals(current, _presented) && current.IsCurrent(now)
            ? current
            : null;
    }

    public LoginDirectoryAvailability Availability(LoginServerSelection selection)
    {
        return LoginDirectoryStatus.Resolve(_refresh is not null, _refresh?.Current is not null, _refresh?.LastAttemptFailed ?? false, selection);
    }

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
            bool firstCurrent = !_startupSelectionApplied && incoming is not null && incoming.IsCurrent(now);
            _startupSelectionApplied |= firstCurrent;
            screen.ApplyCatalog(_presented, incoming, now, firstCurrent);
            _presented = incoming;
        }

        if (_refresh.LastAttemptFailed && !_unavailableReported)
        {
            _diagnostics.CatalogUnavailable();
        }

        _unavailableReported = _refresh.LastAttemptFailed;
    }

    public void Cancel()
    {
        _refresh?.Cancel();
    }

    public void Dispose()
    {
        _refresh?.Dispose();
    }

    private static async Task<SignedLoginCatalog?> FetchAsync(LoginCatalogAuthority authority, TimeProvider clock, CancellationToken token)
    {
        using LoginCatalogClient client = new(clock);
        return await client.FetchAsync(authority, clock.GetUtcNow(), token).ConfigureAwait(false);
    }
}
