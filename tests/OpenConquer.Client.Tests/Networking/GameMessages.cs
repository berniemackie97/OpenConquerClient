using System.Buffers.Binary;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Client.Tests.Networking;

internal static class GameMessages
{
    /// <summary>Loads a frame into its own zeroed native buffer, as the world dispatcher does.</summary>
    internal static GameMessageBuffer Load(byte[] packet)
    {
        Assert.True(GameMessageBuffer.TryLoad(packet, new byte[GameMessageBuffer.Capacity], out GameMessageBuffer message));
        return message;
    }

    internal static byte[] Header(int length, ushort type)
    {
        byte[] packet = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), type);
        return packet;
    }

    /// <summary>The first <paramref name="length"/> bytes of <paramref name="packet"/> with the size word rewritten.</summary>
    internal static byte[] Truncate(byte[] packet, int length)
    {
        byte[] shortened = packet.AsSpan(0, length).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(shortened, (ushort)length);
        return shortened;
    }

    /// <summary><paramref name="packet"/> followed by <paramref name="extra"/> 0xEE bytes, with the size word rewritten.</summary>
    internal static byte[] Extend(byte[] packet, int extra)
    {
        byte[] extended = [.. packet, .. Enumerable.Repeat((byte)0xEE, extra)];
        BinaryPrimitives.WriteUInt16LittleEndian(extended, (ushort)extended.Length);
        return extended;
    }
}
