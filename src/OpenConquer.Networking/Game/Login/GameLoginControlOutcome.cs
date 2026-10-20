namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// A verified first login-channel route, or a terminal refusal. Neither
/// successful route establishes world admission or authenticates the peer.
/// </summary>
internal enum GameLoginControlKind
{
    ExistingCharacter,
    CharacterCreation,
    Refused,
}

/// <param name="Kind">Existing character, creation, or refusal.</param>
/// <param name="RefusalBytes">
/// Owned NUL-terminated-prefix bytes of an untrusted refusal. Decode only at
/// an explicitly selected UI code page; native wire text is not fixed CP1252.
/// </param>
internal readonly record struct GameLoginControlOutcome(
    GameLoginControlKind Kind, ReadOnlyMemory<byte> RefusalBytes);
