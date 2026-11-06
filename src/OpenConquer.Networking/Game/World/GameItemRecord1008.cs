using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.World;

/// <summary>
/// 5517 item record (1008): parser 0x762AA0, process 0x762AC3. The native class name is unproven. Only the fields this
/// client consumes are decoded; the full layout is in 20_notes MsgItem1008-domain-contract-5517.md.
/// </summary>
internal readonly record struct GameItemRecord1008(
    uint ItemId,
    uint ItemTypeId,
    ushort Durability,
    byte Action,
    byte Position,
    byte AdditionLevel,
    byte EnchantmentLifeBonus)
{
    internal const ushort PacketType = 1008;
    internal const int PacketLength = 68;

    internal const byte AddAction = 1;
    internal const byte UpdateAction = 3;

    /// <summary>Exact 68-byte length is this client's ingress policy, not a native parser rule.</summary>
    internal static bool TryRead(ReadOnlySpan<byte> packet, out GameItemRecord1008 record)
    {
        record = default;
        if (packet.Length != PacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != PacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            return false;
        }

        record = new GameItemRecord1008(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]),
            packet[16],
            packet[18],
            packet[33],
            packet[36]);
        return true;
    }
}
