using System.Buffers.Binary;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameWeather1016Tests
{
    // typed-world-dispatch-2026-11-06/wire-layout-golden.json "weather".
    private const string GoldenFrame = "1400f80302000000fe0300006701000098badcfe";

    [Fact]
    public void TryRead_PreservesTheFullWidthGoldenFields()
    {
        Assert.True(GameWeather1016.TryRead(Convert.FromHexString(GoldenFrame), out GameWeather1016 weather));
        Assert.Equal(new GameWeather1016(2, 1022, 359, 0xfedcba98), weather);
        Assert.True(weather.HasSupportedValues);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(21)]
    public void TryRead_RejectsAnyLengthOtherThanTwenty(int length)
    {
        byte[] packet = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), GameWeather1016.PacketType);
        Assert.False(GameWeather1016.TryRead(packet, out _));
    }

    [Fact]
    public void TryRead_RejectsAMismatchedType()
    {
        byte[] packet = Convert.FromHexString(GoldenFrame);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1017);
        Assert.False(GameWeather1016.TryRead(packet, out _));
    }

    [Theory]
    [InlineData(0u, 0u, 0u, false)]
    [InlineData(1u, 0u, 0u, true)]
    [InlineData(6u, 0u, 0u, false)]
    [InlineData(11u, 1022u, 359u, true)]
    [InlineData(12u, 0u, 0u, false)]
    [InlineData(1u, 1023u, 0u, false)]
    [InlineData(1u, 0u, 360u, false)]
    public void HasSupportedValues_AppliesTheClientIngressRanges(uint kind, uint intensity, uint direction, bool supported) =>
        Assert.Equal(supported, new GameWeather1016(kind, intensity, direction, 0).HasSupportedValues);
}
