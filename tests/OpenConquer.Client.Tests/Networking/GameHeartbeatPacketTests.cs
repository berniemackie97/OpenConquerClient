using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

// 20_notes Network/bootstrap-tolerance-cp24d.md, d2b slice 2.
public sealed class GameHeartbeatPacketTests
{
    [Fact]
    public void LatencyTick_MatchesCMsgTickBuildSubType()
    {
        byte[] packet = new byte[88];
        GameLatencyTick1009.Write(packet, heroId: 0x11223344, milliseconds: 0x0000ABCD);

        byte[] expected = new byte[88];
        BinaryPrimitives.WriteUInt16LittleEndian(expected, 88);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 1009);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4), 0x11223344);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(12), 27);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(16), 0x0000ABCDu ^ 0x11223344u);
        Assert.Equal(Convert.ToHexString(expected), Convert.ToHexString(packet));
    }

    [Fact]
    public void LatencyReport_MatchesSub743fed()
    {
        byte[] packet = new byte[20];
        GameLatencyReport1037.Write(packet, localAddress: 0x0337D30A, latency: 42, milliseconds: 0x01020304);

        Assert.Equal("14000D04000000000AD33703" + "2A000000" + "04030201", Convert.ToHexString(packet));
    }

    [Fact]
    public void Writers_RequireTheirFixedLengths()
    {
        Assert.Throws<ArgumentException>(() => GameLatencyTick1009.Write(new byte[87], 1, 1));
        Assert.Throws<ArgumentException>(() => GameLatencyReport1037.Write(new byte[21], 1, 1, 1));
    }

    // Expected values computed separately from the table read out of Conquer.exe at 0x9432D8.
    [Theory]
    [InlineData("", 0x00000000u)]
    [InlineData("4F70656E436F6E71756572", 0xB29F7EC2u)]
    public void Checksum_MatchesTheNativeTableDrivenLoop(string hex, uint expected) =>
        Assert.Equal(expected, GameSilentInfoReport.ComputeChecksum(Convert.FromHexString(hex)));

    [Fact]
    public void Checksum_CoversEveryTableIndex()
    {
        byte[] data = new byte[256];
        for (int index = 0; index < data.Length; index++)
        {
            data[index] = (byte)index;
        }

        Assert.Equal(0xB27A31C2u, GameSilentInfoReport.ComputeChecksum(data));
    }

    [Theory]
    [InlineData("  42\r\n", 42u)]
    [InlineData("-3", 0xFFFFFFFDu)]
    [InlineData("abc", 0u)]
    [InlineData("+7x", 7u)]
    [InlineData("4294967297", 1u)]
    public void Version_ParsesLikeTheNativeAtoi(string text, uint expected) =>
        Assert.Equal(expected, GameSilentInfoReport.ParseVersion(System.Text.Encoding.ASCII.GetBytes(text)));

    [Fact]
    public void Version_ReadsOnlyTheFirst128Bytes() =>
        Assert.Equal(0xFFFFFFFFu, GameSilentInfoReport.ParseVersion(System.Text.Encoding.ASCII.GetBytes(new string('9', 200))));
}
