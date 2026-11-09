using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Client.Tests.Networking;

internal static class WorldSpawnPacketFixture
{
    internal static byte[] Create(uint entityId, ushort x = 62, ushort y = 101,
        byte direction = 3, string name = "Peer", uint appearance = 1003)
    {
        byte[] nameBytes = Encoding.ASCII.GetBytes(name);
        byte[] packet = new byte[223 + nameBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 10014);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), appearance);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), entityId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), 7);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(22), 0x1234);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(30), 0x5678);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(40), 111000);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(48), 130000);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(52), 420000);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(56), 410000);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(80), 250);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(84), 810);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(86), x);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(88), y);
        packet[90] = direction;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(91), 100);
        packet[98] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(99), 120);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(167), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(182), 111000);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(186), 130000);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(190), 420000);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(194), 410000);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(207), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(209), 20);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(211), 30);
        packet[218] = 4;
        packet[219] = checked((byte)nameBytes.Length);
        nameBytes.CopyTo(packet, 220);
        return packet;
    }
}
