using System.Buffers.Binary;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// 5517 MsgAction (10010) fixed 38-byte base: parser 0x6FEB93, process 0x6FED1F. Field meaning is action-specific;
/// +37 is the trailing string-list count.
/// </summary>
internal readonly record struct GameWorldAction10010(
    uint EntityId,
    uint ParameterPair,
    uint ActionParameter,
    uint Timestamp,
    ushort Action,
    ushort Direction,
    ushort X,
    ushort Y,
    uint Data1,
    uint Data2,
    byte Flag)
{
    internal const ushort PacketType = 10010;
    internal const int PacketLength = 38;

    internal const ushort EnterMap = 0x4A;
    internal const ushort GetItems = 0x4B;
    internal const ushort GetFriends = 0x4C;
    internal const ushort GetWeaponSkills = 0x4D;
    internal const ushort GetMagic = 0x4E;
    internal const ushort GetSyndicate = 0x61;
    internal const ushort PositionCorrection = 108;
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

    /// <summary>
    /// Reads the fixed base from the native message buffer. Parse 0x6FEB93 has no size rule and does not read the +37
    /// string list; fields past a short message read zero.
    /// </summary>
    internal static GameWorldAction10010 Read(GameMessageBuffer message) => new(
        message.ReadUInt32(4),
        message.ReadUInt32(8),
        message.ReadUInt32(12),
        message.ReadUInt32(16),
        message.ReadUInt16(20),
        message.ReadUInt16(22),
        message.ReadUInt16(24),
        message.ReadUInt16(26),
        message.ReadUInt32(28),
        message.ReadUInt32(32),
        message.ReadByte(36));

    /// <summary>
    /// World-entry bootstrap reader: exact length and a zero string count. Bootstrap policy, not a native rule; its
    /// tolerance is CP2.4c.
    /// </summary>
    internal static bool TryRead(ReadOnlySpan<byte> packet, out GameWorldAction10010 action)
    {
        action = default;
        Span<byte> storage = stackalloc byte[GameMessageBuffer.Capacity];
        if (packet.Length != PacketLength || packet[37] != 0 ||
            !GameMessageBuffer.TryLoad(packet, storage, out GameMessageBuffer message) ||
            message.Type != PacketType)
        {
            return false;
        }

        action = Read(message);
        return true;
    }
}
