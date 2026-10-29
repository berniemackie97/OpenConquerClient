namespace OpenConquer.Client.UI.Login.Status;

/// <summary>UI feedback only; it never establishes GameServer world admission.</summary>
internal enum LoginAttemptStatus
{
    Idle,
    SelectRealm,
    EnterCredentials,
    RetryLater,
    ConnectingAccount,
    AccountRefused,
    ConnectingGame,
    ReadingCharacter,
    GameRefused,
    CharacterCreation,
    CharacterReceived,
    Timeout,
    Unavailable,
    Cancelled
}
