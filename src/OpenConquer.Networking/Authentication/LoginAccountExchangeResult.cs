namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Result of one bounded AccountServer exchange. Success owns the still-connected account socket
/// for the separate native 1100/1052 continuation; failure carries only the numeric refusal code.
/// This does not represent GameServer admission.
/// </summary>
internal sealed class LoginAccountExchangeResult : IAsyncDisposable
{
    private readonly LoginAccountSession? _session;
    private readonly Lock _lifetimeGate = new();
    private bool _postAuthenticationCompleted;
    private int _reportedResourceVersion;
    private bool _gameProofWritten;
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
    /// Completes the native 1100/1052 AccountServer continuation and closes its socket.
    /// This is NOT the GameServer 1052 login proof and does not connect to the world.
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
        lock (_lifetimeGate)
        {
            _disposed = true;
        }

        return _session?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public override string ToString() => $"LoginAccountExchangeResult {{ IsSuccess = {IsSuccess} }}";
}
