using OpenConquer.Networking.Game.Framing;

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

    /// <summary>Reads the fields from the native message buffer. Parse has no size rule; fields past a short record read zero.</summary>
    internal static GameItemRecord1008 Read(GameMessageBuffer message) => new(
        message.ReadUInt32(4),
        message.ReadUInt32(8),
        message.ReadUInt16(12),
        message.ReadByte(16),
        message.ReadByte(18),
        message.ReadByte(33),
        message.ReadByte(36));
}
