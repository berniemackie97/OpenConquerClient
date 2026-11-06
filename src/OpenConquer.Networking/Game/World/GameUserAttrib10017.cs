using OpenConquer.Networking.Game.Framing;

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

    /// <summary>
    /// Entries that lie wholly inside the 0x400 buffer. Native loops the full signed count (0x758B92) and reads past its
    /// buffer above this; that is not mirrored.
    /// </summary>
    internal const int MaximumEntries = (GameMessageBuffer.Capacity - HeaderLength) / EntryLength;

    private readonly GameMessageBuffer _message;

    private GameUserAttrib10017(GameMessageBuffer message) => _message = message;

    internal uint TargetId => _message.ReadUInt32(4);

    /// <summary>The signed count at +8, as native compares it.</summary>
    internal int DeclaredCount => unchecked((int)_message.ReadUInt32(8));

    /// <summary>
    /// The entries processed: none for a count of zero or less, else the count stopped at <see cref="MaximumEntries"/>.
    /// Entries past the message but inside the buffer read zero, as native's do.
    /// </summary>
    internal int Count => Math.Clamp(DeclaredCount, 0, MaximumEntries);

    /// <summary>Parse has no size or count rule; every field is read from the native message buffer.</summary>
    internal static GameUserAttrib10017 Read(GameMessageBuffer message) => new(message);

    // Public only because foreach binds to a public GetEnumerator; the declaring type stays internal.
    public Enumerator GetEnumerator() => new(_message, Count);

    public ref struct Enumerator
    {
        private readonly GameMessageBuffer _message;
        private readonly int _count;
        private int _index;

        internal Enumerator(GameMessageBuffer message, int count)
        {
            _message = message;
            _count = count;
            _index = -1;
        }

        public readonly GameUserAttribEntry Current
        {
            get
            {
                int offset = HeaderLength + _index * EntryLength;
                return new GameUserAttribEntry(
                    _message.ReadUInt32(offset),
                    _message.ReadUInt64(offset + 4),
                    _message.ReadUInt64(offset + 12));
            }
        }

        public bool MoveNext() => ++_index < _count;
    }
}

/// <summary>One 10017 entry: attribute id and its 16-byte payload as low and high u64 halves.</summary>
internal readonly record struct GameUserAttribEntry(uint AttributeId, ulong Low, ulong High);
