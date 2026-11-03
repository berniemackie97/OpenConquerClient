using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.World;

internal readonly record struct GameWalk10005(
    uint PackedDirection, uint EntityId, uint Selector, uint MovementTick, uint MapState)
{
    internal const ushort PacketType = 10005;
    internal const int PacketLength = 24;
    internal const uint MountedSelector = 9;

    internal byte DirectionIndex => (byte)((byte)PackedDirection % (Selector == MountedSelector ? 24 : 8));

    internal static bool TryRead(ReadOnlySpan<byte> packet, out GameWalk10005 walk)
    {
        walk = default;
        if (packet.Length != PacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != PacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            return false;
        }

        uint entityId = BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]);
        if (entityId == 0)
        {
            return false;
        }

        walk = new GameWalk10005(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            entityId,
            BinaryPrimitives.ReadUInt32LittleEndian(packet[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[16..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[20..]));
        return true;
    }

    internal (int X, int Y) GetOrdinaryDelta()
    {
        if (Selector == MountedSelector)
        {
            throw new InvalidOperationException("Mounted movement uses a separate direction table.");
        }

        return DirectionIndex switch
        {
            0 => (0, 1),
            1 => (-1, 1),
            2 => (-1, 0),
            3 => (-1, -1),
            4 => (0, -1),
            5 => (1, -1),
            6 => (1, 0),
            7 => (1, 1),
            _ => throw new InvalidOperationException("Invalid ground movement direction.")
        };
    }

    internal static void WriteOrdinary(Span<byte> packet, uint entityId,
        byte directionIndex, uint movementTick, uint mapState)
    {
        if (packet.Length != PacketLength)
        {
            throw new ArgumentException("The walk packet must have 24 bytes.", nameof(packet));
        }

        ArgumentOutOfRangeException.ThrowIfZero(entityId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(directionIndex, (byte)7);

        packet.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(packet, PacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet[2..], PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(packet[4..], directionIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(packet[8..], entityId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet[16..], movementTick);
        BinaryPrimitives.WriteUInt32LittleEndian(packet[20..], mapState);
    }
}
