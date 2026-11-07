using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.World;

/// <summary>
/// Client latency report (1037), as <c>sub_743fed</c> 0x743FED writes it: size 20, +4 0, +8 the local IPv4 address
/// (the raw in_addr dword), +12 the last measured latency, +16 the client millisecond counter.
/// </summary>
internal static class GameLatencyReport1037
{
    internal const ushort PacketType = 1037;
    internal const int PacketLength = 20;

    internal static void Write(Span<byte> destination, uint localAddress, uint latency, uint milliseconds)
    {
        if (destination.Length != PacketLength)
        {
            throw new ArgumentException("A 1037 report must occupy exactly 20 bytes.", nameof(destination));
        }

        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, PacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], localAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], latency);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], milliseconds);
    }
}
