using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Networking.Game.World;

/// <summary>
/// Packet 1110 map metadata: identity +4, document +8, flags +12/+16 (0x732B6F/0x732B84/0x732B99).
/// Parser 0x732AED has only common header/type validation; the inherited buffer is zeroed at 0x732AA9.
/// </summary>
internal readonly record struct GameMapInfo1110(uint MapId, uint MapDataId, ulong MapFlags)
{
    internal const ushort PacketType = 1110;

    internal static bool TryRead(ReadOnlySpan<byte> packet, out GameMapInfo1110 map)
    {
        map = default;
        Span<byte> storage = stackalloc byte[GameMessageBuffer.Capacity];
        if (!GameMessageBuffer.TryLoad(packet, storage, out GameMessageBuffer message) || message.Type != PacketType)
        {
            return false;
        }

        map = new GameMapInfo1110(message.ReadUInt32(4), message.ReadUInt32(8), message.ReadUInt64(12));
        return true;
    }
}
