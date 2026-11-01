using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.Framing;

/// <summary>
/// The GameServer packet-length field excludes
/// the trailing eight-byte direction-specific protocol signature.
/// </summary>
internal static class GameSecuredFrameProtocol
{
    internal const int HeaderLength = 4;
    internal const int MaximumPacketLength = 0x400;
    internal const int SignatureLength = 8;
    internal const int MaximumWireLength = MaximumPacketLength + SignatureLength;

    internal static ReadOnlySpan<byte> ClientSignature => "TQClient"u8;
    internal static ReadOnlySpan<byte> ServerSignature => "TQServer"u8;

    internal static int GetWireLength(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < HeaderLength or > MaximumPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length)
        {
            throw new ArgumentException("Invalid GameServer packet framing.", nameof(packet));
        }

        return packet.Length + SignatureLength;
    }
}
