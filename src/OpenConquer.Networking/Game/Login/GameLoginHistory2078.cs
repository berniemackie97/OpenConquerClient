using System.Buffers.Binary;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// Native 2078 account-history wire values. The location is retained as owned
/// bytes; the native setter stores bytes and the font consumer selects a code
/// page later, rather than mandating a Windows-1252 packet decoder.
/// </summary>
internal sealed record GameLoginHistory2078(
    uint LastLoginTimestamp, byte LocationWarningFlag, ReadOnlyMemory<byte> LastLoginLocationBytes)
{
    private const int FirstLocationByteOffset = 9;
    private const ushort PacketType = 2078;

    internal static GameLoginHistory2078 Read(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < FirstLocationByteOffset or > GameSecuredFrameProtocol.MaximumPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            throw new InvalidDataException("GameServer account history is not a valid 2078 packet.");
        }

        ReadOnlySpan<byte> location = packet[FirstLocationByteOffset..];
        int terminator = location.IndexOf((byte)0);
        if (terminator >= 0)
        {
            location = location[..terminator];
        }
        else if (packet.Length == GameSecuredFrameProtocol.MaximumPacketLength)
        {
            // Native strlen can overread the 1024-byte packet storage here.
            // We deliberately fail closed instead of reproducing the overread.
            throw new InvalidDataException("GameServer account history has no bounded location terminator.");
        }

        // For shorter packets the native constructor's zero-filled storage
        // supplies the implicit C-string terminator. Never read past our span.
        // Bytes after the first NUL are ignored, as in the native setter.
        return new GameLoginHistory2078(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            packet[8],
            location.ToArray());
    }
}
