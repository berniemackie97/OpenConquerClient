using System.Buffers.Binary;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Reads the first secured native 5517 login-channel talk (1004). The wire
/// carries byte strings, not a mandatory Windows-1252 Unicode encoding.
/// Native list-count/copy failures do not invalidate the packet header.
/// </summary>
internal static class GameLoginControl1004
{
    internal const ushort PacketType = 1004;
    internal const ushort LoginChannel = 0x835;

    private const int StringCountOffset = 24;
    private const int FirstStringOffset = 25;
    private const int MessageIndex = 3;

    internal static GameLoginControlOutcome Read(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < FirstStringOffset or > GameSecuredFrameProtocol.MaximumPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            throw new InvalidDataException("The first GameServer control frame is not a valid 1004 packet.");
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(packet[8..]) != LoginChannel)
        {
            throw new InvalidDataException("The first GameServer control talk is not on the login channel.");
        }

        // The native 5517 reader queries indices 0..3 independently and does
        // not require count == 4. Absent message entries produce empty text.
        if (packet[StringCountOffset] <= MessageIndex)
        {
            return new GameLoginControlOutcome(GameLoginControlKind.Refused, ReadOnlyMemory<byte>.Empty);
        }

        int offset = FirstStringOffset;
        for (int index = 0; index < MessageIndex; index++)
        {
            // Oversized ignored entries are skipped by their declared length.
            // Never reproduce the native reader's unchecked memory accesses:
            // a length running beyond the bounded received frame is invalid.
            _ = ReadEntry(packet, ref offset);
        }

        ReadOnlySpan<byte> message = ReadEntry(packet, ref offset);
        int terminator = message.IndexOf((byte)0);
        if (terminator >= 0)
        {
            message = message[..terminator];
        }

        // The native dispatcher compares its NUL-terminated control words
        // using _stricmp, not an ordinal case-sensitive comparison.
        if (EqualsAsciiControlWord(message, "ANSWER_OK"u8))
        {
            return new GameLoginControlOutcome(GameLoginControlKind.ExistingCharacter, ReadOnlyMemory<byte>.Empty);
        }

        if (EqualsAsciiControlWord(message, "NEW_ROLE"u8))
        {
            return new GameLoginControlOutcome(GameLoginControlKind.CharacterCreation, ReadOnlyMemory<byte>.Empty);
        }

        // Preserve wire bytes for an explicit presentation-code-page decision.
        // ANSI conversion is consumer-specific in 5517; it is not a framing
        // validator. Never hand out borrowed pooled frame memory.
        return new GameLoginControlOutcome(GameLoginControlKind.Refused, message.ToArray());
    }

    private static ReadOnlySpan<byte> ReadEntry(ReadOnlySpan<byte> packet, ref int offset)
    {
        if (offset >= packet.Length)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        int length = packet[offset++];
        if (length > packet.Length - offset)
        {
            throw new InvalidDataException(
                "GameServer login talk string exceeds the bounded received packet.");
        }

        ReadOnlySpan<byte> entry = packet.Slice(offset, length);
        offset += length;
        return entry;
    }

    private static bool EqualsAsciiControlWord(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
    {
        if (actual.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < actual.Length; index++)
        {
            byte character = actual[index];
            if (character is >= (byte)'a' and <= (byte)'z')
            {
                character = (byte)(character - ('a' - 'A'));
            }

            if (character != expected[index])
            {
                return false;
            }
        }

        return true;
    }
}
