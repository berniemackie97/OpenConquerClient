namespace OpenConquer.Client.UI.Login.Status;

internal enum LoginFailureKind
{
    EmptyAccount,
    EmptyPassword,
    Resource,
    AccountRefusal,
    GameRefusal,
    RegistrationNotice,
    WorldContent,
}

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

    internal static LoginFailure EmptyAccount()
    {
        return new LoginFailure(LoginFailureKind.EmptyAccount, 0, []);
    }

    internal static LoginFailure EmptyPassword()
    {
        return new LoginFailure(LoginFailureKind.EmptyPassword, 0, []);
    }

    internal static LoginFailure Resource(uint id)
    {
        return new LoginFailure(LoginFailureKind.Resource, id, []);
    }

    internal static LoginFailure WorldContent()
    {
        return new LoginFailure(LoginFailureKind.WorldContent, 0, []);
    }

    internal static LoginFailure Account(uint code)
    {
        return new LoginFailure(LoginFailureKind.AccountRefusal, code, []);
    }

    internal static LoginFailure Registration(LoginRegistrationNotice notice)
    {
        return Enum.IsDefined(notice)
            ? new LoginFailure(LoginFailureKind.RegistrationNotice, (uint)notice, [])
            : throw new ArgumentOutOfRangeException(nameof(notice), notice, null);
    }

    internal static LoginFailure Game(ReadOnlySpan<byte> text)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, 255);
        int nul = text.IndexOf((byte)0);
        return new LoginFailure(LoginFailureKind.GameRefusal, 0, (nul < 0 ? text : text[..nul]).ToArray());
    }

    public override string ToString()
    {
        return $"LoginFailure {{ Kind = {Kind}, Code = {Code} }}";
    }
}

internal sealed record LoginAttemptFeedback(LoginAttemptStatus Status, LoginFailure? Failure = null);
