using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Networking.Authentication;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Startup.LoginFlow;

/// <summary>
/// Owns the UI-initiated attempt, credentials, cancellations and accepted game
/// transport. Only the desktop thread starts/collects attempts; I/O is async.
/// </summary>
internal sealed class LoginAttemptCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);
    private readonly string _contentRoot;
    private readonly TimeProvider _clock;
    private Task<AttemptResult>? _pending;
    private CancellationTokenSource? _cancellation;
    private GameServerHandshakeSession? _acceptedGame;
    private SignedLoginCatalog? _catalog;
    private LoginServerSelection? _selection;
    private long _revision;
    private long _lastAttemptTimestamp;
    private bool _hasAttempted;
    private int _status;
    private bool _disposed;

    internal LoginAttemptCoordinator(string contentRoot, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        _contentRoot = contentRoot;
        _clock = clock ?? TimeProvider.System;
    }

    internal LoginAttemptStatus Status => (LoginAttemptStatus)Volatile.Read(ref _status);

    internal bool TryStart(LoginScreen screen, SignedLoginCatalog? catalog)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(screen);
        if (_pending is not null)
        {
            return false;
        }

        LoginServerSelection selected = screen.Selection;
        if (catalog is null || !catalog.IsCurrent(_clock.GetUtcNow()) ||
            !ReferenceEquals(selected.Catalog, catalog.View) ||
            selected.SelectedRegion is not { } region ||
            selected.SelectedServer is not { } server ||
            !catalog.TryGetAccountEndpoint(region, server, out LoginAccountEndpoint? endpoint) ||
            endpoint is null || endpoint.Host != "127.0.0.1")
        {
            Publish(LoginAttemptStatus.SelectRealm);
            return false;
        }

        // Native CMD_ENTERGAME (0x4ABFBD) applies its 10-second attempt gate
        // before validating the password, using monotonic elapsed time.
        long now = _clock.GetTimestamp();
        if (_hasAttempted && _clock.GetElapsedTime(_lastAttemptTimestamp, now) <
            TimeSpan.FromSeconds(10))
        {
            Publish(LoginAttemptStatus.RetryLater);
            return false;
        }

        _hasAttempted = true;
        _lastAttemptTimestamp = now;
        if (!screen.Credentials.TryTakeLoginCredentials(out byte[] name, out char[] password))
        {
            Publish(LoginAttemptStatus.EnterCredentials);
            return false;
        }

        GameServerHandshakeSession? previous = _acceptedGame;
        _acceptedGame = null;
        _catalog = catalog;
        _selection = selected;
        _revision = selected.Revision;
        _cancellation = new CancellationTokenSource();
        Publish(LoginAttemptStatus.ConnectingAccount);
        _pending = RunAsync(catalog, selected, name, password, previous, _cancellation.Token);
        return true;
    }

    internal void Update(SignedLoginCatalog? current)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pending is not { } pending)
        {
            return;
        }

        if (!ReferenceEquals(current, _catalog) || current is null ||
            !current.IsCurrent(_clock.GetUtcNow()) || _selection?.Revision != _revision)
        {
            _cancellation?.Cancel();
        }

        if (!pending.IsCompleted)
        {
            return;
        }

        // RunAsync maps all asynchronous failures to safe, nonsecret status.
        AttemptResult result = pending.GetAwaiter().GetResult();
        _pending = null;
        _cancellation?.Dispose();
        _cancellation = null;
        _catalog = null;
        _selection = null;
        _acceptedGame = result.Game;
        Publish(result.Status);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation?.Cancel();
        if (_pending is { } pending)
        {
            AttemptResult result = await pending.ConfigureAwait(false);
            if (result.Game is { } game)
            {
                await game.DisposeAsync().ConfigureAwait(false);
            }
        }

        _cancellation?.Dispose();
        _cancellation = null;
        _pending = null;
        if (_acceptedGame is { } accepted)
        {
            _acceptedGame = null;
            await accepted.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<AttemptResult> RunAsync(SignedLoginCatalog catalog,
        LoginServerSelection selection, byte[] name, char[] password,
        GameServerHandshakeSession? previous, CancellationToken cancellationToken)
    {
        try
        {
            if (previous is not null)
            {
                await previous.DisposeAsync().ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await using LoginAccountExchangeResult account =
                await LoginSignedAccountExchange.AuthenticateAsync(catalog, selection,
                    name, password, LoginAccountTimeouts.Default, _clock, cancellationToken)
                    .ConfigureAwait(false);

            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(password.AsSpan()));
            CryptographicOperations.ZeroMemory(name);
            if (!account.IsSuccess)
            {
                return new(LoginAttemptStatus.AccountRefused, null);
            }

            // Native NetBIOS failure: account packet 1100 has an empty
            // 12-byte ASCII MAC text field; game proof 1052 has six raw zeros.
            // Their serializers independently zero-fill an absent address.
            await account.CompletePostAuthenticationAsync(ReadOnlyMemory<byte>.Empty,
                ReadResourceVersion(_contentRoot), s_timeout, cancellationToken).ConfigureAwait(false);

            Publish(LoginAttemptStatus.ConnectingGame);
            GameServerHandshakeSession? game = null;
            bool transferred = false;
            try
            {
                game = await GameServerHandshakeSession.OpenAsync(account,
                    GameServerHandshakeTimeouts.Default, cancellationToken).ConfigureAwait(false);

                // The verified English dataset emits the little-endian 'En' tag.
                await game.SendLoginProofAsync(0x6E45, ReadOnlyMemory<byte>.Empty,
                    s_timeout, cancellationToken).ConfigureAwait(false);

                Publish(LoginAttemptStatus.ReadingCharacter);
                GameLoginControlOutcome control =
                    await game.ReadLoginControlAsync(s_timeout, cancellationToken).ConfigureAwait(false);
                if (control.Kind == GameLoginControlKind.Refused)
                {
                    return new(LoginAttemptStatus.GameRefused, null);
                }

                if (control.Kind == GameLoginControlKind.CharacterCreation)
                {
                    return new(LoginAttemptStatus.NewRoleUnavailable, null);
                }

                _ = await game.ReadExistingCharacterPreludeAsync(s_timeout, cancellationToken)
                    .ConfigureAwait(false);
                _ = await game.ReadCharacterInfoAsync(s_timeout, cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                transferred = true;
                return new(LoginAttemptStatus.CharacterReceived, game);
            }
            finally
            {
                if (!transferred && game is not null)
                {
                    await game.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return new(LoginAttemptStatus.Cancelled, null);
        }
        catch (TimeoutException)
        {
            return new(LoginAttemptStatus.Timeout, null);
        }
        catch (Exception)
        {
            // Exception messages from untrusted peers must never reach the UI.
            return new(LoginAttemptStatus.Unavailable, null);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(password.AsSpan()));
            CryptographicOperations.ZeroMemory(name);
        }
    }

    private static int ReadResourceVersion(string contentRoot)
    {
        try
        {
            using FileStream file = new(Path.Combine(contentRoot, "res.dat"),
                FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> bytes = stackalloc byte[64];
            int read = file.Read(bytes);
            if (read is < 1 or >= 64)
            {
                return 0;
            }

            string text = System.Text.Encoding.ASCII.GetString(bytes[..read]);
            string token = text.Split([' ', '\r', '\n', '\t'], 2)[0];
            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int version) ? version : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private void Publish(LoginAttemptStatus value) => Volatile.Write(ref _status, (int)value);

    private sealed record AttemptResult(LoginAttemptStatus Status, GameServerHandshakeSession? Game);
}
