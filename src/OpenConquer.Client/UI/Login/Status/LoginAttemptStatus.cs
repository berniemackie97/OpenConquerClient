namespace OpenConquer.Client.UI.Login.Status;

/// <summary>UI feedback only; it never establishes GameServer world admission.</summary>
internal enum LoginAttemptStatus
{
    Idle,
    SelectRealm,
    EnterCredentials,
    ConnectingAccount,
    AccountRefused,
    ConnectingGame,
    ReadingCharacter,
    GameRefused,
    NewRoleUnavailable,
    CharacterReceived,
    Timeout,
    Unavailable,
    Cancelled
}
