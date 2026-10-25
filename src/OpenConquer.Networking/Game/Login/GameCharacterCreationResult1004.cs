namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Reads one create-role channel (2100) talk, the result of a 1001 request
/// (<c>CMyShellDlg_HandleCreateRoleStatusSystemTalk</c> <c>0x5DCDDF</c>).
/// </summary>
internal static class GameCharacterCreationResult1004
{
    internal static GameCharacterCreationOutcome Read(ReadOnlySpan<byte> packet)
    {
        if (!GameControlTalk1004.TryReadMessage(packet, GameControlTalk1004.CreationChannel, out ReadOnlySpan<byte> message))
        {
            return new GameCharacterCreationOutcome(GameCharacterCreationKind.Refused, ReadOnlyMemory<byte>.Empty);
        }

        return GameControlTalk1004.EqualsControlWord(message, "ANSWER_OK"u8)
            ? new GameCharacterCreationOutcome(GameCharacterCreationKind.Created, ReadOnlyMemory<byte>.Empty)
            : new GameCharacterCreationOutcome(GameCharacterCreationKind.Refused, message.ToArray());
    }
}
