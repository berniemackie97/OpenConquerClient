using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameWorldAction10010Tests
{
    [Theory]
    [InlineData(GameWorldAction10010.EnterMap, 1000000u)]
    [InlineData(GameWorldAction10010.ClientStateApplied, 0u)]
    [InlineData(GameWorldAction10010.GetItems, 1000000u)]
    [InlineData(GameWorldAction10010.GetStatistics, 1000000u)]
    public void Write_ProducesFixed5517ActionAndRoundTrips(ushort action, uint entityId)
    {
        byte[] packet = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(packet, action, entityId, parameterPair: 0x12345678, timestamp: 123);

        Assert.Equal(38, BinaryPrimitives.ReadUInt16LittleEndian(packet));
        Assert.Equal(10010, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)));
        Assert.Equal(entityId, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)));
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8)));
        Assert.Equal(123u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(16)));
        Assert.Equal(action, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(20)));
        Assert.All(packet.AsSpan(22).ToArray(), value => Assert.Equal(0, value));

        Assert.True(GameWorldAction10010.TryRead(packet, out ushort readAction, out uint readEntity, out uint parameter,
            out ushort x, out ushort y));
        Assert.Equal((action, entityId, 0x12345678u, (ushort)0, (ushort)0),
            (readAction, readEntity, parameter, x, y));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    [InlineData(37, 1)]
    public void TryRead_RejectsMalformedHeadersAndStringCounts(int offset, byte corruption)
    {
        byte[] packet = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(packet, GameWorldAction10010.EnterMap, 123);
        packet[offset] = corruption;
        Assert.False(GameWorldAction10010.TryRead(packet, out _, out _, out _, out _, out _));
    }
}
