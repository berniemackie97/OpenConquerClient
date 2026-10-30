namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Result of one AccountServer exchange. On success it keeps the account socket open for the 1100/1052
/// follow-up; on failure it carries only the numeric refusal code. It says nothing about GameServer admission.
/// </summary>
internal sealed class LoginAccountExchangeResult : IAsyncDisposable
{
    private readonly LoginAccountSession? _session;
    private readonly Lock _lifetimeGate = new();
    private bool _postAuthenticationCompleted;
    private int _reportedResourceVersion;
    private bool _gameProofWritten;
    private bool _gameHandoffClaimed;
    private readonly CancellationTokenSource _gameHandoffLifetime = new();
    private bool _disposed;

    private LoginAccountExchangeResult(LoginAccountResponse1055 response, LoginAccountSession? session)
    {
        Response = response;
        _session = session;
    }

    internal bool IsSuccess => Response.IsSuccess;
    internal uint FailureCode => IsSuccess ? 0 : Response.AuthenticationKeyOrFailureCode;

    // Sensitive success fields must never be included in logs or serialized diagnostics.
    internal LoginAccountResponse1055 Response
    {
        get;
    }

    /// <summary>
    /// Sends the 1100/1052 AccountServer follow-up and closes the socket. This isn't the GameServer 1052 login
    /// proof and doesn't connect to the world.
    /// </summary>
    internal async Task CompletePostAuthenticationAsync(ReadOnlyMemory<byte> macAddress,
        int resourceVersion, TimeSpan phaseTimeout, CancellationToken cancellationToken = default)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("A failed AccountServer exchange has no continuation.");
        }

        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        await _session.SendPostAuthenticationReportsAsync(
            Response.SessionToken, macAddress, resourceVersion, phaseTimeout, cancellationToken).ConfigureAwait(false);

        lock (_lifetimeGate)
        {
            // Disposal can race the outgoing reports; never grant a proof after disposal.
            ObjectDisposedException.ThrowIf(_disposed, this);
            _reportedResourceVersion = resourceVersion;
            _postAuthenticationCompleted = true;
        }
    }

    /// <summary>
    /// Writes one standard post-DH GameServer login proof after successful AccountServer
    /// reports. This does not open a GameServer connection or authorize world entry.
    /// The resource version is taken from the completed AccountServer 1052 report.
    /// The caller owns and must clear the resulting sensitive packet.
    /// </summary>
    internal void WriteGameLoginProof(Span<byte> destination, ushort localeTag,
        ReadOnlySpan<byte> hardwareAddress)
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session is null || !Response.IsSuccess || !_postAuthenticationCompleted)
            {
                throw new InvalidOperationException("The AccountServer post-authentication sequence has not completed.");
            }

            if (_gameProofWritten)
            {
                throw new InvalidOperationException("The GameServer login proof has already been composed.");
            }

            GameLoginProof1052.Write(destination, Response.SessionToken,
                Response.AuthenticationKeyOrFailureCode, localeTag, hardwareAddress, _reportedResourceVersion);
            _gameProofWritten = true;
        }
    }

    /// <summary>
    /// Grants at most one GameServer handshake attempt after both native account
    /// reports completed. A 1055 redirect is untrusted: only literal IPv4
    /// loopback is permitted until independently authenticated transport exists.
    /// </summary>
    internal (int Port, CancellationToken Lifetime) ClaimLoopbackGameHandoff()
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session is null || !Response.IsSuccess || !_postAuthenticationCompleted)
            {
                throw new InvalidOperationException("AccountServer post-authentication has not completed.");
            }

            if (_gameHandoffClaimed)
            {
                throw new InvalidOperationException("GameServer handshake has already been claimed.");
            }

            if (Response.GameHost != "127.0.0.1" ||
                Response.GamePort is < 1 or > 65535)
            {
                throw new InvalidOperationException("Remote GameServer redirect requires authenticated peer transport.");
            }

            _gameHandoffClaimed = true;
            return ((int)Response.GamePort, _gameHandoffLifetime.Token);
        }
    }

    internal static LoginAccountExchangeResult Success(LoginAccountResponse1055 response, LoginAccountSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!response.IsSuccess)
        {
            throw new ArgumentException("A success result needs a successful 1055 response.", nameof(response));
        }

        return new LoginAccountExchangeResult(response, session);
    }

    internal static LoginAccountExchangeResult Failure(LoginAccountResponse1055 response)
    {
        if (response.IsSuccess)
        {
            throw new ArgumentException("A failure result needs a failed 1055 response.", nameof(response));
        }

        return new LoginAccountExchangeResult(response, session: null);
    }

    public ValueTask DisposeAsync()
    {
        bool cancel;
        lock (_lifetimeGate)
        {
            cancel = !_disposed;
            _disposed = true;
        }

        if (cancel)
        {
            // Cancel active connect/handshake without running callbacks under
            // the lifetime lock.
            _gameHandoffLifetime.Cancel();
            _gameHandoffLifetime.Dispose();
        }

        return _session?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public override string ToString() => $"LoginAccountExchangeResult {{ IsSuccess = {IsSuccess} }}";
}
