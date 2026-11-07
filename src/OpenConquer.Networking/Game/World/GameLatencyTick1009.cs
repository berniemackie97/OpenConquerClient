using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.World;

/// <summary>
/// Client heartbeat, 1009 subtype 27, as <c>CMsgTick__Build_SubType</c> 0x6EAC51 writes it: size 88, +4 hero id, +8 0,
/// +12 subtype 27, +16 the client millisecond counter XOR the hero id (0x6EACB4).
/// </summary>
internal static class GameLatencyTick1009
{
    internal const ushort Subtype = 27;

    internal static void Write(Span<byte> destination, uint heroId, uint milliseconds)
    {
        if (destination.Length != GameItem1009.FixedLength)
        {
            throw new ArgumentException("A 1009 heartbeat must occupy exactly 88 bytes.", nameof(destination));
        }

        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, GameItem1009.FixedLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], GameItem1009.PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], heroId);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[12..], Subtype);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], milliseconds ^ heroId);
    }
}
