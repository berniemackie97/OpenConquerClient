namespace OpenConquer.Networking.Authentication;

internal enum LoginConnectTarget
{
    Account,
    Game,
}

/// <summary>A socket-connect failure, distinct from an established connection's protocol/read failure.</summary>
internal sealed class LoginConnectFailureException(LoginConnectTarget target, bool timedOut, Exception cause)
    : IOException("Login socket connection failed.", cause)
{
    internal LoginConnectTarget Target { get; } = target;
    internal bool TimedOut { get; } = timedOut;
}
