using System.Buffers.Binary;
using System.Text;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Bounded account last-login history and location alert from native packet 2078.
/// The timestamp and alert byte are wire values, not UI policy or proof of admission.
/// </summary>
internal sealed record GameLoginHistory2078(
    uint LastLoginTimestamp, byte LocationWarningFlag, string LastLoginLocation)
{
    private const int FirstLocationByteOffset = 9;
    private const int MinimumPacketLength = FirstLocationByteOffset + 1;
    private const ushort PacketType = 2078;
    private static readonly Encoding s_ansi = CreateAnsiEncoding();

    internal static GameLoginHistory2078 Read(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < MinimumPacketLength or > GameSecuredFrameProtocol.MaximumPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            throw new InvalidDataException("GameServer account history is not a valid 2078 packet.");
        }

        ReadOnlySpan<byte> locationAndPadding = packet[FirstLocationByteOffset..];
        int terminator = locationAndPadding.IndexOf((byte)0);
        if (terminator < 0)
        {
            throw new InvalidDataException("GameServer account history location is not NUL-terminated.");
        }

        ReadOnlySpan<byte> location = locationAndPadding[..terminator];
        foreach (byte character in location)
        {
            // Windows-1252 leaves these positions undefined. Runtime providers
            // may substitute rather than triggering their decoder fallback.
            if (character is 0x81 or 0x8D or 0x8F or 0x90 or 0x9D)
            {
                throw new InvalidDataException("GameServer account history contains an undefined ANSI byte.");
            }
        }

        try
        {
            // The native 5517 reader consumes the first NUL-terminated string.
            // Bounded trailing bytes are opaque compatibility padding, not fields.
            return new GameLoginHistory2078(
                BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
                packet[8],
                s_ansi.GetString(location));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("GameServer account history has invalid ANSI text.", exception);
        }
    }

    private static Encoding CreateAnsiEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }
}
