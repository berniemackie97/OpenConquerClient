using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.World;

/// <summary>
/// 5517 MsgItem (1009): parser 0x6EAE56, process 0x6EAEB6. +4 and +8 are subtype-specific parameters, +12 the subtype,
/// +20 a list count, +84 a u32 list. Subtype 46 carries 13 active-equipment UIDs at +0x20..+0x50.
/// </summary>
internal readonly ref struct GameItem1009
{
    internal const ushort PacketType = 1009;
    internal const int MinimumLength = 14;
    internal const int FixedLength = 88;
    internal const int ListOffset = 84;
    internal const int SnapshotItemCount = 13;

    internal const ushort RemoveItem = 3;
    internal const ushort EquipItem = 5;
    internal const ushort UnequipItem = 6;
    internal const ushort UpdateDurability = 17;
    internal const ushort EquipmentSnapshot = 46;

    private const int SnapshotOffset = 0x20;
    private const int CountOffset = 20;

    private readonly ReadOnlySpan<byte> _packet;

    private GameItem1009(ReadOnlySpan<byte> packet) => _packet = packet;

    internal int Length => _packet.Length;

    internal uint ParameterA => BinaryPrimitives.ReadUInt32LittleEndian(_packet[4..]);

    internal uint ParameterB => BinaryPrimitives.ReadUInt32LittleEndian(_packet[8..]);

    internal ushort Subtype => BinaryPrimitives.ReadUInt16LittleEndian(_packet[12..]);

    /// <summary>True for the fixed 88-byte body plus whole u32 list entries.</summary>
    internal bool HasListLayout => _packet.Length >= FixedLength && (_packet.Length - ListOffset) % 4 == 0;

    /// <summary>The number of u32 entries the length leaves room for after +84.</summary>
    internal int ListCapacity => HasListLayout ? (_packet.Length - ListOffset) / 4 : 0;

    /// <summary>The declared list count at +20. Requires <see cref="HasListLayout"/>.</summary>
    internal uint DeclaredListCount => HasListLayout
        ? BinaryPrimitives.ReadUInt32LittleEndian(_packet[CountOffset..])
        : throw new InvalidOperationException("The 1009 frame has no list body.");

    /// <summary>Reads list entry <paramref name="index"/> at +84 + 4*index.</summary>
    internal uint GetListedId(int index) => (uint)index < (uint)ListCapacity
        ? BinaryPrimitives.ReadUInt32LittleEndian(_packet[(ListOffset + index * 4)..])
        : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>Reads snapshot UID <paramref name="index"/> (0..12) at +0x20 + 4*index. Requires the fixed body.</summary>
    internal uint GetSnapshotItemId(int index) => (uint)index < SnapshotItemCount && _packet.Length >= FixedLength
        ? BinaryPrimitives.ReadUInt32LittleEndian(_packet[(SnapshotOffset + index * 4)..])
        : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>Requires at least the subtype word; body length rules per subtype are this client's policy.</summary>
    internal static bool TryRead(ReadOnlySpan<byte> packet, out GameItem1009 item)
    {
        item = default;
        if (packet.Length < MinimumLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            return false;
        }

        item = new GameItem1009(packet);
        return true;
    }
}
