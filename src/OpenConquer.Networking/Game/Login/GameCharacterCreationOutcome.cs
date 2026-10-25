namespace OpenConquer.Networking.Game.Login;

internal enum GameCharacterCreationKind
{
    /// <summary>Create-role channel <c>ANSWER_OK</c>: the character exists and the client repeats its 1052 proof.</summary>
    Created,
    /// <summary>Any other create-role channel text; the native client shows it and stays on the create-role screen.</summary>
    Refused,
}

/// <param name="Kind">Created or refused.</param>
/// <param name="RefusalBytes">
/// Owned NUL-terminated-prefix bytes of an untrusted refusal. Decode only at an explicitly selected UI code page.
/// </param>
internal readonly record struct GameCharacterCreationOutcome(
    GameCharacterCreationKind Kind, ReadOnlyMemory<byte> RefusalBytes);
