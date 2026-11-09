using System.Buffers.Binary;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameWalk10005Tests
{
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(2, -1, 0)]
    [InlineData(3, -1, -1)]
    [InlineData(4, 0, -1)]
    [InlineData(5, 1, -1)]
    [InlineData(6, 1, 0)]
    [InlineData(7, 1, 1)]
    public void OrdinaryDirectionsMatchTheEightDirectionWireTable(
        byte direction, int expectedX, int expectedY)
    {
        byte[] packet = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(packet, 0x11223344, direction, 0x55667788, 0x99AABBCC);

        Assert.Equal((ushort)24, BinaryPrimitives.ReadUInt16LittleEndian(packet));
        Assert.Equal((ushort)10005, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)));
        GameWalk10005 parsed = GameWalk10005.Read(GameMessages.Load(packet));
        Assert.Equal(0x11223344u, parsed.EntityId);
        Assert.Equal(0x55667788u, parsed.MovementTick);
        Assert.Equal(0x99AABBCCu, parsed.MapState);
        Assert.Equal(0u, parsed.Selector);
        Assert.Equal(direction, parsed.DirectionIndex);
        Assert.Equal((expectedX, expectedY), parsed.GetDelta());
    }

    [Theory]
    [InlineData(0, 0, 2)]
    [InlineData(1, -2, 2)]
    [InlineData(2, -2, 0)]
    [InlineData(3, -2, -2)]
    [InlineData(4, 0, -2)]
    [InlineData(5, 2, -2)]
    [InlineData(6, 2, 0)]
    [InlineData(7, 2, 2)]
    [InlineData(8, -1, 2)]
    [InlineData(9, -2, 1)]
    [InlineData(10, -2, -1)]
    [InlineData(11, -1, -2)]
    [InlineData(12, 1, -2)]
    [InlineData(13, 2, -1)]
    [InlineData(14, 2, 1)]
    [InlineData(15, 1, 2)]
    [InlineData(16, -1, 2)]
    [InlineData(17, -2, 1)]
    [InlineData(18, -2, -1)]
    [InlineData(19, -1, -2)]
    [InlineData(20, 1, -2)]
    [InlineData(21, 2, -1)]
    [InlineData(22, 2, 1)]
    [InlineData(23, 1, 2)]
    public void SelectorNineUsesVerifiedTwentyFourDirectionOffsets(
        byte index, int dx, int dy)
    {
        GameWalk10005 packet = new(index, 1_000_001, GameWalk10005.MountedSelector, 25, 1010);

        Assert.Equal(index, packet.DirectionIndex);
        Assert.Equal((dx, dy), packet.GetDelta());
    }

    [Fact]
    public void PacketParserPreservesUpperBitsAndFullWidthSelector()
    {
        byte[] packet = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(packet, 123, 6, 100, 300);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0xAABBCC17);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), GameWalk10005.MountedSelector);

        GameWalk10005 mounted = GameWalk10005.Read(GameMessages.Load(packet));
        Assert.Equal(0xAABBCC17u, mounted.PackedDirection);
        Assert.Equal((byte)(0x17 % 24), mounted.DirectionIndex);
        Assert.Equal((1, 2), mounted.GetDelta());

        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), 0x10009);
        GameWalk10005 ordinary = GameWalk10005.Read(GameMessages.Load(packet));
        Assert.Equal((byte)(0x17 % 8), ordinary.DirectionIndex);
        Assert.Equal((1, 1), ordinary.GetDelta());
    }

    [Fact]
    public void WriteOrdinary_RejectsAZeroEntityOrAnOutOfRangeDirection()
    {
        byte[] packet = new byte[GameWalk10005.PacketLength];
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameWalk10005.WriteOrdinary(packet, 0, 2, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameWalk10005.WriteOrdinary(packet, 123, 8, 1, 0));
    }

    // CMsgEntityAction__Parse 0x7404BE has no size or entity rule; the buffer is zero past the message.
    [Fact]
    public void Read_DecodesEntityZeroAndReadsZeroPastAShortMessage()
    {
        byte[] packet = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(packet, 123, 6, 0x55667788, 0x99AABBCC);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), 0);

        GameWalk10005 walk = GameWalk10005.Read(GameMessages.Load(GameMessages.Truncate(packet, 18)));

        Assert.Equal(new GameWalk10005(6, 0, 0, 0x7788, 0), walk);
    }

    [Fact]
    public void Read_IgnoresBytesPastTheFixedFields()
    {
        byte[] packet = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(packet, 123, 6, 100, 300);

        Assert.Equal(GameWalk10005.Read(GameMessages.Load(packet)),
            GameWalk10005.Read(GameMessages.Load(GameMessages.Extend(packet, 40))));
    }
}
