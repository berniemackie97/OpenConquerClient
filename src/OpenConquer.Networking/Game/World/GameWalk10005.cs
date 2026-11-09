using System.Buffers.Binary;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Networking.Game.World;

internal readonly record struct GameWalk10005(
    uint PackedDirection, uint EntityId, uint Selector, uint MovementTick, uint MapState)
{
    internal const ushort PacketType = 10005;
    internal const int PacketLength = 24;
    internal const uint WalkSelector = 0;
    internal const uint RunSelector = 1;
    internal const uint MountedSelector = 9;

    internal bool IsMounted => (byte)Selector == MountedSelector;
    internal byte DirectionIndex => (byte)((byte)PackedDirection % (IsMounted ? 24 : 8));

    internal static GameWalk10005 Read(GameMessageBuffer message) => new(
        message.ReadUInt32(4),
        message.ReadUInt32(8),
        message.ReadUInt32(12),
        message.ReadUInt32(16),
        message.ReadUInt32(20));

    private static readonly sbyte[] s_twoTileDeltaX =
        [0, -2, -2, -2, 0, 2, 2, 2, -1, -2, -2, -1, 1, 2, 2, 1, -1, -2, -2, -1, 1, 2, 2, 1];
    private static readonly sbyte[] s_twoTileDeltaY =
        [2, 2, 0, -2, -2, -2, 0, 2, 2, 1, -1, -2, -2, -1, 1, 2, 2, 1, -1, -2, -2, -1, 1, 2];

    internal (int X, int Y) GetDelta()
    {
        byte index = DirectionIndex;
        if (IsMounted)
        {
            return (s_twoTileDeltaX[index], s_twoTileDeltaY[index]);
        }

        return index switch
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
        byte packedDirection, uint movementTick, uint mapState, byte selector = 0)
    {
        if (packet.Length != PacketLength)
        {
            throw new ArgumentException("The walk packet must have 24 bytes.", nameof(packet));
        }

        ArgumentOutOfRangeException.ThrowIfZero(entityId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(selector, (byte)RunSelector);

        packet.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(packet, PacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet[2..], PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(packet[4..], packedDirection);
        BinaryPrimitives.WriteUInt32LittleEndian(packet[8..], entityId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet[12..], selector);
        BinaryPrimitives.WriteUInt32LittleEndian(packet[16..], movementTick);
        BinaryPrimitives.WriteUInt32LittleEndian(packet[20..], mapState);
    }
}
