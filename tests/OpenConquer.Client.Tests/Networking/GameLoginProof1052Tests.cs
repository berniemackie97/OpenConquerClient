using System.Buffers.Binary;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameLoginProof1052Tests
{
    [Fact]
    public void StandardProofMatchesNative5517AndServerParserFixture()
    {
        byte[] packet = Enumerable.Repeat((byte)0xCC, GameLoginProof1052.PacketLength).ToArray();
        GameLoginProof1052.Write(packet, 0x10203040, 0x50607080, 0x6E45,
            [0x11, 0x22, 0x33, 0x44, 0x55, 0x66], 5517);

        Assert.Equal("1C001C0440302010807060507C00456E11223344556600008D150000",
            Convert.ToHexString(packet));
        Assert.Equal((ushort)28, BinaryPrimitives.ReadUInt16LittleEndian(packet));
        Assert.Equal((ushort)1052, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(22)));
    }

    [Fact]
    public void UnavailableHardwareAddressUsesSixZeroBytesAndSignedResourceVersion()
    {
        byte[] packet = Enumerable.Repeat((byte)0xCC, GameLoginProof1052.PacketLength).ToArray();
        GameLoginProof1052.Write(packet, 1, 0, 0, ReadOnlySpan<byte>.Empty, -123);

        Assert.All(packet.AsSpan(16, 8).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal(-123, BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(24)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8)));
    }

    [Fact]
    public void MissingHardwareAddressHasDeterministicZeroBytes()
    {
        byte[] packet = Enumerable.Repeat((byte)0xA5, GameLoginProof1052.PacketLength).ToArray();

        GameLoginProof1052.Write(packet, 0x10203040, 0x50607080, 0x6E45,
            ReadOnlySpan<byte>.Empty, 5517);

        Assert.Equal("1C001C0440302010807060507C00456E00000000000000008D150000",
            Convert.ToHexString(packet));
    }

    [Fact]
    public void HardwareInputMayAliasDestinationWithoutBeingCleared()
    {
        byte[] packet = new byte[GameLoginProof1052.PacketLength];
        byte[] address = [0x01, 0x23, 0x45, 0x67, 0x89, 0xAB];
        address.CopyTo(packet, 16);

        GameLoginProof1052.Write(packet, 1, 2, 0x6E45, packet.AsSpan(16, 6), 5517);

        Assert.Equal(address, packet.AsSpan(16, 6).ToArray());
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(22)));
    }

    [Fact]
    public void InvalidDestinationOrSessionTokenDoesNotWritePartialSensitiveData()
    {
        byte[] destination = Enumerable.Repeat((byte)0xA5, GameLoginProof1052.PacketLength).ToArray();
        byte[] shortDestination = Enumerable.Repeat((byte)0xA5, GameLoginProof1052.PacketLength - 1).ToArray();
        byte[] tooLongDestination = Enumerable.Repeat((byte)0xA5, GameLoginProof1052.PacketLength + 1).ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameLoginProof1052.Write(destination, 0, 1, 0, [1, 2, 3, 4, 5, 6], 5517));
        Assert.Throws<ArgumentException>(() =>
            GameLoginProof1052.Write(shortDestination, 1, 1, 0, [1, 2, 3, 4, 5, 6], 5517));
        Assert.Throws<ArgumentException>(() =>
            GameLoginProof1052.Write(tooLongDestination, 1, 1, 0, [1, 2, 3, 4, 5, 6], 5517));
        Assert.Throws<ArgumentException>(() =>
            GameLoginProof1052.Write(destination, 1, 1, 0, [1, 2, 3, 4, 5], 5517));

        Assert.All(destination, value => Assert.Equal((byte)0xA5, value));
        Assert.All(shortDestination, value => Assert.Equal((byte)0xA5, value));
        Assert.All(tooLongDestination, value => Assert.Equal((byte)0xA5, value));
    }
}
