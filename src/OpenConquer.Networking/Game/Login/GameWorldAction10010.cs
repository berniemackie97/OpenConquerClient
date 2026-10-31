using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.Login;

/// <summary>5517 MsgAction fixed wire layout used during existing-character world entry.</summary>
internal static class GameWorldAction10010
{
    internal const ushort PacketType = 10010;
    internal const int PacketLength = 38;

    internal const ushort EnterMap = 0x4A;
    internal const ushort GetItems = 0x4B;
    internal const ushort GetFriends = 0x4C;
    internal const ushort GetWeaponSkills = 0x4D;
    internal const ushort GetMagic = 0x4E;
    internal const ushort GetSyndicate = 0x61;
    internal const ushort GetStatistics = 0x84;
    internal const ushort ReportSilentInfo = 0xFB;
    internal const ushort ClientStateApplied = 0x198;

    internal static void Write(Span<byte> destination, ushort action, uint entityId, uint parameterPair = 0, uint timestamp = 0)
    {
        if (destination.Length != PacketLength)
        {
            throw new ArgumentException("A 10010 action must occupy exactly 38 bytes.", nameof(destination));
        }

        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, PacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], entityId);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], parameterPair);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], timestamp);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[20..], action);
    }

    internal static bool TryRead(ReadOnlySpan<byte> packet, out ushort action, out uint entityId,
        out uint parameterPair, out ushort x, out ushort y)
    {
        action = 0;
        entityId = parameterPair = 0;
        x = y = 0;
        if (packet.Length != PacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != PacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType ||
            packet[37] != 0)
        {
            return false;
        }

        entityId = BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]);
        parameterPair = BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]);
        action = BinaryPrimitives.ReadUInt16LittleEndian(packet[20..]);
        x = BinaryPrimitives.ReadUInt16LittleEndian(packet[24..]);
        y = BinaryPrimitives.ReadUInt16LittleEndian(packet[26..]);
        return true;
    }
}
