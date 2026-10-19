using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Reads the native first post-proof GameServer login control talk (1004, channel 0x835).
/// A control word establishes a character-login route, not world admission.
/// </summary>
internal static class GameLoginControl1004
{
    internal const ushort PacketType = 1004;
    internal const ushort LoginChannel = 0x835;

    private const byte StringCount = 4;
    private const int StringCountOffset = 24;
    private const int FirstStringOffset = 25;
    private const int MinimumPacketLength = FirstStringOffset + StringCount;
    private const int MaximumNameLength = 15;

    private static readonly Encoding s_ansi = CreateAnsiEncoding();

    internal static GameLoginControlOutcome Read(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < MinimumPacketLength or > 1024 ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            throw new InvalidDataException("The first GameServer control frame is not a valid 1004 packet.");
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(packet[8..]) != LoginChannel)
        {
            throw new InvalidDataException("The first GameServer control talk is not on the login channel.");
        }

        if (packet[StringCountOffset] != StringCount)
        {
            throw new InvalidDataException("The GameServer login control has an invalid string-list count.");
        }

        int offset = FirstStringOffset;
        ReadOnlySpan<byte> message = default;

        for (int index = 0; index < StringCount; index++)
        {
            if (offset >= packet.Length)
            {
                throw new InvalidDataException("The GameServer login control has a truncated string list.");
            }

            int length = packet[offset++];
            if ((index < 3 && length > MaximumNameLength) || length > packet.Length - offset)
            {
                throw new InvalidDataException("The GameServer login control has an invalid string length.");
            }

            if (index == 3)
            {
                message = packet.Slice(offset, length);
            }

            offset += length;
        }

        if (offset != packet.Length || message.Contains((byte)0))
        {
            throw new InvalidDataException("The GameServer login control contains extra data or an embedded NUL.");
        }

        // Windows-1252 does not assign printable characters to these slots.
        // Reject them explicitly: runtime code-page providers may substitute
        // control characters rather than invoke the decoder fallback.
        foreach (byte value in message)
        {
            if (value is 0x81 or 0x8D or 0x8F or 0x90 or 0x9D)
            {
                throw new InvalidDataException("The GameServer login control contains an undefined ANSI byte.");
            }
        }

        if (message.SequenceEqual("ANSWER_OK"u8))
        {
            return new GameLoginControlOutcome(GameLoginControlKind.ExistingCharacter, null);
        }

        if (message.SequenceEqual("NEW_ROLE"u8))
        {
            return new GameLoginControlOutcome(GameLoginControlKind.CharacterCreation, null);
        }

        try
        {
            return new GameLoginControlOutcome(GameLoginControlKind.Refused, s_ansi.GetString(message));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The GameServer login refusal contains invalid ANSI text.", exception);
        }
    }

    private static Encoding CreateAnsiEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }
}
