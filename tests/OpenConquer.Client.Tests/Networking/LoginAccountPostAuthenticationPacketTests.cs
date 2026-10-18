using System.Buffers.Binary;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class LoginAccountPostAuthenticationPacketTests
{
    [Fact]
    public void MacReportMatchesNative52ByteLayout()
    {
        byte[] packet = Enumerable.Repeat((byte)0xA5, 52).ToArray();
        LoginAccountMacAddressReport1100.Write(packet, 0x11223344, "001122AABBCC"u8);
        Assert.Equal((ushort)52, BinaryPrimitives.ReadUInt16LittleEndian(packet));
        Assert.Equal((ushort)1100, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)));
        Assert.Equal(0x11223344u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)));
        Assert.Equal("001122AABBCC"u8.ToArray(), packet.AsSpan(8, 12).ToArray());
        Assert.All(packet.AsSpan(20, 32).ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void MissingHardwareReportIsEncodedAsZeroFilledNativeField()
    {
        byte[] packet = new byte[52];
        LoginAccountMacAddressReport1100.Write(packet, 7, []);
        Assert.All(packet.AsSpan(8).ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void ResourceReportMatchesNative28ByteLayoutAndSignedVersion()
    {
        byte[] packet = Enumerable.Repeat((byte)0xA5, 28).ToArray();
        LoginAccountResourceVersionReport1052.Write(packet, 0x11223344, -1);
        Assert.Equal((ushort)28, BinaryPrimitives.ReadUInt16LittleEndian(packet));
        Assert.Equal((ushort)1052, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)));
        Assert.Equal(0x11223344u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)));
        Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(8)));
        Assert.Equal("res.dat"u8.ToArray(), packet.AsSpan(12, 7).ToArray());
        Assert.All(packet.AsSpan(19).ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Theory]
    [InlineData("001122AABBC")]
    [InlineData("001122AABBCCD")]
    [InlineData("001122aabbcc")]
    [InlineData("001122AABBGG")]
    [InlineData("001122AABB C")]
    public void InvalidHardwareTextLeavesDestinationUntouched(string value)
    {
        byte[] packet = Enumerable.Repeat((byte)0xA5, 52).ToArray();
        Assert.Throws<ArgumentException>(() =>
            LoginAccountMacAddressReport1100.Write(packet, 7,
                System.Text.Encoding.ASCII.GetBytes(value)));
        Assert.All(packet, b => Assert.Equal((byte)0xA5, b));
    }

    [Fact]
    public void ZeroSessionOrInvalidDestinationNeverPartiallyWrites()
    {
        byte[] mac = Enumerable.Repeat((byte)0xA5, 52).ToArray();
        byte[] resource = Enumerable.Repeat((byte)0xA5, 28).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoginAccountMacAddressReport1100.Write(mac, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoginAccountResourceVersionReport1052.Write(resource, 0, 5517));
        Assert.Throws<ArgumentException>(() =>
            LoginAccountMacAddressReport1100.Write(new byte[51], 7, []));
        Assert.Throws<ArgumentException>(() =>
            LoginAccountResourceVersionReport1052.Write(new byte[27], 7, 5517));
        Assert.All(mac, b => Assert.Equal((byte)0xA5, b));
        Assert.All(resource, b => Assert.Equal((byte)0xA5, b));
    }
}
