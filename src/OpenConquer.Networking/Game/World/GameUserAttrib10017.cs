using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.World;

/// <summary>
/// 5517 MsgUserAttrib (10017): +4 target id, +8 entry count, then 20-byte entries of a u32 attribute id and a 16-byte
/// payload read as two u64 halves. Parser 0x757351, process 0x757374; status 25 copies all 16 payload bytes
/// (0x7574E3), so the high half is not padding.
/// </summary>
internal readonly ref struct GameUserAttrib10017
{
    internal const ushort PacketType = 10017;
    internal const int HeaderLength = 12;
    internal const int EntryLength = 20;

    private readonly ReadOnlySpan<byte> _entries;

    private GameUserAttrib10017(uint targetId, ReadOnlySpan<byte> entries)
    {
        TargetId = targetId;
        _entries = entries;
    }

    internal uint TargetId
    {
        get;
    }

    internal int Count => _entries.Length / EntryLength;

    /// <summary>
    /// Requires the length to be exactly the header plus the declared entries; that exactness is this client's policy,
    /// not a native parser rule.
    /// </summary>
    internal static bool TryRead(ReadOnlySpan<byte> packet, out GameUserAttrib10017 attributes)
    {
        attributes = default;
        if (packet.Length < HeaderLength ||
            (packet.Length - HeaderLength) % EntryLength != 0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType ||
            BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]) != (uint)((packet.Length - HeaderLength) / EntryLength))
        {
            return false;
        }

        attributes = new GameUserAttrib10017(BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]), packet[HeaderLength..]);
        return true;
    }

    // Public only because foreach binds to a public GetEnumerator; the declaring type stays internal.
    public Enumerator GetEnumerator() => new(_entries);

    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<byte> _entries;
        private int _offset;

        internal Enumerator(ReadOnlySpan<byte> entries)
        {
            _entries = entries;
            _offset = -EntryLength;
        }

        public readonly GameUserAttribEntry Current
        {
            get
            {
                ReadOnlySpan<byte> entry = _entries.Slice(_offset, EntryLength);
                return new GameUserAttribEntry(
                    BinaryPrimitives.ReadUInt32LittleEndian(entry),
                    BinaryPrimitives.ReadUInt64LittleEndian(entry[4..]),
                    BinaryPrimitives.ReadUInt64LittleEndian(entry[12..]));
            }
        }

        public bool MoveNext()
        {
            _offset += EntryLength;
            return _offset < _entries.Length;
        }
    }
}

/// <summary>One 10017 entry: attribute id and its 16-byte payload as low and high u64 halves.</summary>
internal readonly record struct GameUserAttribEntry(uint AttributeId, ulong Low, ulong High);
