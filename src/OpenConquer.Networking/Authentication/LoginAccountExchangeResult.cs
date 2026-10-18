namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Result of one AccountServer exchange. On success it keeps the account socket open for the 1100/1052
/// follow-up; on failure it carries only the numeric refusal code. It says nothing about GameServer admission.
/// </summary>
internal sealed class LoginAccountExchangeResult : IAsyncDisposable
{
    private readonly LoginAccountSession? _session;

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
    internal Task CompletePostAuthenticationAsync(ReadOnlyMemory<byte> macAddress,
        int resourceVersion, TimeSpan phaseTimeout, CancellationToken cancellationToken = default)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("A failed AccountServer exchange has no continuation.");
        }

        return _session.SendPostAuthenticationReportsAsync(
            Response.SessionToken, macAddress, resourceVersion, phaseTimeout, cancellationToken);
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

    public ValueTask DisposeAsync() => _session?.DisposeAsync() ?? ValueTask.CompletedTask;

    public override string ToString() => $"LoginAccountExchangeResult {{ IsSuccess = {IsSuccess} }}";
}
