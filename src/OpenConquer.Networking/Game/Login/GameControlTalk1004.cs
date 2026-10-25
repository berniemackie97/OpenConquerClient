using System.Buffers.Binary;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// The bounded native 5517 control-talk (1004) message reader shared by the login channel (2101) and the
/// create-role channel (2100). <c>CMsgTalk__Process</c> splits the two at <c>0x7314D2</c> but queues the same
/// message text for both. The wire carries byte strings, not a mandatory Windows-1252 Unicode encoding.
/// </summary>
internal static class GameControlTalk1004
{
    internal const ushort PacketType = 1004;
    internal const ushort CreationChannel = 0x834;
    internal const ushort LoginChannel = 0x835;

    private const int ChannelOffset = 8;
    private const int StringCountOffset = 24;
    private const int FirstStringOffset = 25;
    private const int MessageIndex = 3;

    /// <summary>
    /// Validates the frame and channel and returns the NUL-terminated prefix of the message entry. The native
    /// reader queries entries 0..3 independently and does not require count == 4, so an absent message entry
    /// returns false with empty text.
    /// </summary>
    internal static bool TryReadMessage(ReadOnlySpan<byte> packet, ushort channel, out ReadOnlySpan<byte> message)
    {
        if (packet.Length is < FirstStringOffset or > GameSecuredFrameProtocol.MaximumPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            throw new InvalidDataException("The GameServer control frame is not a valid 1004 packet.");
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(packet[ChannelOffset..]) != channel)
        {
            throw new InvalidDataException("The GameServer control talk is not on the expected channel.");
        }

        message = ReadOnlySpan<byte>.Empty;
        if (packet[StringCountOffset] <= MessageIndex)
        {
            return false;
        }

        int offset = FirstStringOffset;
        for (int index = 0; index < MessageIndex; index++)
        {
            // Oversized ignored entries are skipped by their declared length.
            // Never reproduce the native reader's unchecked memory accesses:
            // a length running beyond the bounded received frame is invalid.
            _ = ReadEntry(packet, ref offset);
        }

        message = ReadEntry(packet, ref offset);
        int terminator = message.IndexOf((byte)0);
        if (terminator >= 0)
        {
            message = message[..terminator];
        }

        return true;
    }

    /// <summary>The native dispatchers compare control words with <c>_stricmp</c>, not ordinally.</summary>
    internal static bool EqualsControlWord(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
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

    private static ReadOnlySpan<byte> ReadEntry(ReadOnlySpan<byte> packet, scoped ref int offset)
    {
        if (offset >= packet.Length)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        int length = packet[offset++];
        if (length > packet.Length - offset)
        {
            throw new InvalidDataException("GameServer control talk string exceeds the bounded received packet.");
        }

        ReadOnlySpan<byte> entry = packet.Slice(offset, length);
        offset += length;
        return entry;
    }
}
