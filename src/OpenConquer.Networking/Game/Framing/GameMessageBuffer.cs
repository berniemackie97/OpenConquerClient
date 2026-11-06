using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.Framing;

/// <summary>
/// A received message as native Process code sees it: the 0x400-byte packet buffer at <c>CNetMsg</c> object+4, zeroed for
/// every message by <c>CNetMsg__ResetPacketBuffer</c> 0x6DF290 and then filled with the header-size bytes by
/// <c>CNetMsg__ValidateHeaderAndCopy</c>. Offsets past the packet read zero.
/// </summary>
internal readonly ref struct GameMessageBuffer
{
    internal const int Capacity = 0x400;
    internal const int HeaderLength = 4;

    private readonly ReadOnlySpan<byte> _bytes;

    private GameMessageBuffer(ReadOnlySpan<byte> bytes, int length)
    {
        _bytes = bytes;
        Length = length;
    }

    /// <summary>The header size word: how many bytes the server sent.</summary>
    internal int Length
    {
        get;
    }

    internal ushort Type => ReadUInt16(2);

    internal byte ReadByte(int offset) => _bytes[offset];

    internal ushort ReadUInt16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(_bytes[offset..]);

    internal uint ReadUInt32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_bytes[offset..]);

    internal ulong ReadUInt64(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(_bytes[offset..]);

    /// <summary>
    /// Copies <paramref name="packet"/> into <paramref name="storage"/> after zeroing it. Returns false where native dispatch
    /// returns no message: size above 0x400 (0x6DF3D8), or the validator's header size, count and nonzero type rules
    /// (0x6DF2AE). Native skips such a message and continues (0x6DEC3A).
    /// </summary>
    internal static bool TryLoad(ReadOnlySpan<byte> packet, Span<byte> storage, out GameMessageBuffer message)
    {
        if (storage.Length != Capacity)
        {
            throw new ArgumentException("Message storage must be exactly 0x400 bytes.", nameof(storage));
        }

        message = default;
        if (packet.Length is < HeaderLength or > Capacity ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) == 0)
        {
            return false;
        }

        storage.Clear();
        packet.CopyTo(storage);
        message = new GameMessageBuffer(storage, packet.Length);
        return true;
    }
}
