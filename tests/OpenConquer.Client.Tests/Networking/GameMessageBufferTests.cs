using System.Buffers.Binary;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Client.Tests.Networking;

// Native receive path: 20_notes Network/inbound-tolerance-cp24.md.
public sealed class GameMessageBufferTests
{
    [Fact]
    public void TryLoad_ZeroesTheBufferPastTheMessageEvenWhenStorageWasDirty()
    {
        byte[] storage = Enumerable.Repeat((byte)0xCC, GameMessageBuffer.Capacity).ToArray();
        byte[] packet = GameMessages.Header(8, 1009);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0x11223344);

        Assert.True(GameMessageBuffer.TryLoad(packet, storage, out GameMessageBuffer message));
        Assert.Equal((8, (ushort)1009, 0x11223344u), (message.Length, message.Type, message.ReadUInt32(4)));
        Assert.Equal(0u, message.ReadUInt32(8));
        Assert.Equal(0ul, message.ReadUInt64(GameMessageBuffer.Capacity - 8));
    }

    [Fact]
    public void TryLoad_AcceptsAFullSizeMessage()
    {
        byte[] packet = GameMessages.Header(GameMessageBuffer.Capacity, 2030);
        packet[^1] = 0x5A;
        Assert.True(GameMessageBuffer.TryLoad(packet, new byte[GameMessageBuffer.Capacity], out GameMessageBuffer message));
        Assert.Equal(0x5A, message.ReadByte(GameMessageBuffer.Capacity - 1));
    }

    [Fact]
    public void TryLoad_DropsWhatNativeDispatchDropsBeforeParse()
    {
        byte[] storage = new byte[GameMessageBuffer.Capacity];

        // Size above 0x400: dispatcher 0x6DF3D8.
        Assert.False(GameMessageBuffer.TryLoad(new byte[GameMessageBuffer.Capacity + 1], storage, out _));
        // Size word not the byte count, and type 0: CNetMsg__ValidateHeaderAndCopy 0x6DF2AE.
        byte[] mismatched = GameMessages.Header(8, 1009);
        BinaryPrimitives.WriteUInt16LittleEndian(mismatched, 9);
        Assert.False(GameMessageBuffer.TryLoad(mismatched, storage, out _));
        Assert.False(GameMessageBuffer.TryLoad(GameMessages.Header(8, 0), storage, out _));
        Assert.False(GameMessageBuffer.TryLoad(new byte[3], storage, out _));
    }

    [Fact]
    public void TryLoad_RequiresExactlyOneNativeBufferOfStorage() =>
        Assert.Throws<ArgumentException>(() =>
            GameMessageBuffer.TryLoad(GameMessages.Header(8, 1009), new byte[GameMessageBuffer.Capacity - 1], out _));
}
