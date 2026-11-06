using System.Buffers.Binary;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameItemRecord1008Tests
{
    // Synthetic sentinel frame built from the 20_notes 1008 layout, not a capture. Unconsumed offsets carry noise.
    private const string SentinelFrame =
        "4400f00378563412f1ee0b004523000001ee09dd000000000000000000000000005a00006b000000cc00000000000000" +
        "00000000000000000000000000000000bb000000";

    [Fact]
    public void TryRead_DecodesTheConsumedFieldsAtTheirNativeOffsets()
    {
        Assert.True(GameItemRecord1008.TryRead(Convert.FromHexString(SentinelFrame), out GameItemRecord1008 record));
        Assert.Equal(new GameItemRecord1008(0x12345678, 0x000BEEF1, 0x2345, 1, 9, 0x5a, 0x6b), record);
    }

    [Theory]
    [InlineData(67)]
    [InlineData(69)]
    public void TryRead_RejectsAnyLengthOtherThanSixtyEight(int length)
    {
        byte[] packet = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), GameItemRecord1008.PacketType);
        Assert.False(GameItemRecord1008.TryRead(packet, out _));
    }

    [Fact]
    public void TryRead_RejectsAMismatchedType()
    {
        byte[] packet = Convert.FromHexString(SentinelFrame);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1009);
        Assert.False(GameItemRecord1008.TryRead(packet, out _));
    }
}
