namespace OpenConquer.Client.UI.Login.Status;

internal enum LoginFailureKind
{
    EmptyAccount,
    EmptyPassword,
    Resource,
    AccountRefusal,
    GameRefusal,
}

/// <summary>Owned, bounded presentation evidence; never contains credentials or exception messages.</summary>
internal sealed class LoginFailure
{
    private LoginFailure(LoginFailureKind kind, uint code, byte[] bytes)
    {
        Kind = kind;
        Code = code;
        Bytes = bytes;
    }

    internal LoginFailureKind Kind
    {
        get;
    }

    internal uint Code
    {
        get;
    }

    internal ReadOnlyMemory<byte> Bytes
    {
        get;
    }
    internal bool OpensErrorMovie => Kind != LoginFailureKind.AccountRefusal || Code != 24;

    internal static LoginFailure EmptyAccount() => new(LoginFailureKind.EmptyAccount, 0, []);
    internal static LoginFailure EmptyPassword() => new(LoginFailureKind.EmptyPassword, 0, []);
    internal static LoginFailure Resource(uint id) => new(LoginFailureKind.Resource, id, []);
    internal static LoginFailure Account(uint code) => new(LoginFailureKind.AccountRefusal, code, []);
    internal static LoginFailure Game(ReadOnlySpan<byte> text)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, 255);
        int nul = text.IndexOf((byte)0);
        return new(LoginFailureKind.GameRefusal, 0, (nul < 0 ? text : text[..nul]).ToArray());
    }

    public override string ToString() => $"LoginFailure {{ Kind = {Kind}, Code = {Code} }}";
}

/// <summary>An atomic UI snapshot. A new failure instance is one notification, even if its text repeats.</summary>
internal sealed record LoginAttemptFeedback(LoginAttemptStatus Status, LoginFailure? Failure = null);
