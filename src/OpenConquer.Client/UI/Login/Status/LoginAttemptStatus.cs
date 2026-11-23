namespace OpenConquer.Client.UI.Login.Status;

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
    InitializingWorld,
    GameRefused,
    CharacterCreation,
    CharacterReceived,
    Timeout,
    Unavailable,
    Cancelled
}
