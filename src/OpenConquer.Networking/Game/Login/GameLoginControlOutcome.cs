namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// A verified first login-channel control response. Neither successful route
/// means that the character has completed bootstrap or entered the world.
/// </summary>
internal enum GameLoginControlKind
{
    ExistingCharacter,
    CharacterCreation,
    Refused,
}

/// <param name="Kind">The character-login route, or a terminal refusal.</param>
/// <param name="RefusalText">Only populated for a refusal; untrusted presentation text.</param>
internal readonly record struct GameLoginControlOutcome(GameLoginControlKind Kind, string? RefusalText);
