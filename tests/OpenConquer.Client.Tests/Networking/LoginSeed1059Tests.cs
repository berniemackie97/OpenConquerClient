using System.Buffers.Binary;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class LoginSeed1059Tests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(0x12345678u)]
    [InlineData(uint.MaxValue)]
    public void ReadsOnlyTheVerifiedLittleEndianSeedField(uint value)
    {
        byte[] packet = new byte[LoginSeed1059.MinimumFrameLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)packet.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), LoginSeed1059.PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), value);

        Assert.True(LoginSeed1059.TryReadSeed(packet, out uint result));
        Assert.Equal(value, result);
    }

    [Fact]
    public void ExtraFrameBytesAreNotTreatedAsKnownSeedFields()
    {
        byte[] packet = Enumerable.Repeat((byte)0x5A, LoginSeed1059.MaximumFrameLength).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)packet.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), LoginSeed1059.PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0x89ABCDEFu);

        Assert.True(LoginSeed1059.TryReadSeed(packet, out uint seed));
        Assert.Equal(0x89ABCDEFu, seed);
    }

    [Fact]
    public void RejectsTruncatedOversizedWrongTypeOrInconsistentFrameLengths()
    {
        byte[] valid = new byte[LoginSeed1059.MinimumFrameLength];
        BinaryPrimitives.WriteUInt16LittleEndian(valid, (ushort)valid.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(valid.AsSpan(2), LoginSeed1059.PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(valid.AsSpan(4), 0xFFFFFFFFu);

        AssertInvalid([]);
        AssertInvalid(valid[..3]);
        AssertInvalid(valid[..7]);
        byte[] oversized = new byte[LoginSeed1059.MaximumFrameLength + 1];
        BinaryPrimitives.WriteUInt16LittleEndian(oversized, (ushort)oversized.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(oversized.AsSpan(2), LoginSeed1059.PacketType);
        AssertInvalid(oversized);

        byte[] mismatch = valid.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(mismatch, (ushort)(valid.Length + 1));
        AssertInvalid(mismatch);

        byte[] wrongType = valid.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(wrongType.AsSpan(2), 1060);
        AssertInvalid(wrongType);

        static void AssertInvalid(byte[] packet)
        {
            Assert.False(LoginSeed1059.TryReadSeed(packet, out uint seed));
            Assert.Equal(0u, seed);
        }
    }
}
