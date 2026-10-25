namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Reads the first secured native 5517 login-channel talk (1004) and resolves its route
/// (<c>CMyShellDlg_HandleLoginStatusSystemTalk</c> <c>0x5DCC64</c>).
/// </summary>
internal static class GameLoginControl1004
{
    internal const ushort PacketType = GameControlTalk1004.PacketType;
    internal const ushort LoginChannel = GameControlTalk1004.LoginChannel;

    internal static GameLoginControlOutcome Read(ReadOnlySpan<byte> packet)
    {
        if (!GameControlTalk1004.TryReadMessage(packet, LoginChannel, out ReadOnlySpan<byte> message))
        {
            return new GameLoginControlOutcome(GameLoginControlKind.Refused, ReadOnlyMemory<byte>.Empty);
        }

        if (GameControlTalk1004.EqualsControlWord(message, "ANSWER_OK"u8))
        {
            return new GameLoginControlOutcome(GameLoginControlKind.ExistingCharacter, ReadOnlyMemory<byte>.Empty);
        }

        if (GameControlTalk1004.EqualsControlWord(message, "NEW_ROLE"u8))
        {
            return new GameLoginControlOutcome(GameLoginControlKind.CharacterCreation, ReadOnlyMemory<byte>.Empty);
        }

        // Preserve wire bytes for an explicit presentation-code-page decision.
        // ANSI conversion is consumer-specific in 5517; it is not a framing
        // validator. Never hand out borrowed pooled frame memory.
        return new GameLoginControlOutcome(GameLoginControlKind.Refused, message.ToArray());
    }
}
