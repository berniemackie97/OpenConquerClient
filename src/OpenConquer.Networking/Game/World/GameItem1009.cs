using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Networking.Game.World;

/// <summary>
/// 5517 MsgItem (1009): parser 0x6EAE56, process 0x6EAEB6. +4 and +8 are subtype-specific parameters, +12 the subtype,
/// +20 a list count, +84 a u32 list. Subtype 46 carries 13 active-equipment UIDs at +0x20..+0x50. Parse has no size
/// rule, so every field is read from the native message buffer and fields past a short message read zero.
/// </summary>
internal readonly ref struct GameItem1009
{
    internal const ushort PacketType = 1009;
    internal const int FixedLength = 88;
    internal const int ListOffset = 84;
    internal const int SnapshotItemCount = 13;

    /// <summary>
    /// List entries inside the 0x400 buffer. Native subtype 5 loops the full unsigned count (0x6EB436) and reads past its
    /// buffer above this; that is not mirrored.
    /// </summary>
    internal const int ListCapacity = (GameMessageBuffer.Capacity - ListOffset) / 4;

    internal const ushort RemoveItem = 3;
    internal const ushort EquipItem = 5;
    internal const ushort UnequipItem = 6;
    internal const ushort UpdateDurability = 17;
    internal const ushort LatencyTick = 27;
    internal const ushort EquipmentSnapshot = 46;

    private const int SnapshotOffset = 0x20;
    private const int CountOffset = 20;

    private readonly GameMessageBuffer _message;

    private GameItem1009(GameMessageBuffer message) => _message = message;

    /// <summary>The header size word.</summary>
    internal int Length => _message.Length;

    internal uint ParameterA => _message.ReadUInt32(4);

    internal uint ParameterB => _message.ReadUInt32(8);

    internal ushort Subtype => _message.ReadUInt16(12);

    /// <summary>+16; subtype 27 carries the server's time reference here (latency = now - +16).</summary>
    internal uint ParameterC => _message.ReadUInt32(16);

    /// <summary>The unsigned list count at +20.</summary>
    internal uint DeclaredListCount => _message.ReadUInt32(CountOffset);

    /// <summary>
    /// The entries native would read from inside its buffer: the declared count, stopped at <see cref="ListCapacity"/>.
    /// </summary>
    internal int ListCount => DeclaredListCount > ListCapacity ? ListCapacity : (int)DeclaredListCount;

    /// <summary>Reads list entry <paramref name="index"/> at +84 + 4*index.</summary>
    internal uint GetListedId(int index) => (uint)index < ListCapacity
        ? _message.ReadUInt32(ListOffset + index * 4)
        : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>Reads snapshot UID <paramref name="index"/> (0..12) at +0x20 + 4*index.</summary>
    internal uint GetSnapshotItemId(int index) => (uint)index < SnapshotItemCount
        ? _message.ReadUInt32(SnapshotOffset + index * 4)
        : throw new ArgumentOutOfRangeException(nameof(index));

    internal static GameItem1009 Read(GameMessageBuffer message) => new(message);
}
