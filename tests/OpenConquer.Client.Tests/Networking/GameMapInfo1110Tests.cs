using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameMapInfo1110Tests
{
    // Independently encoded full-width field sentinels from Process 0x732B6F/84/99; not a runtime capture.
    private const string GoldenFrame = "1400560478563412efcdab891032547698badcfe";

    [Theory]
    [InlineData(4, 0u, 0u, 0UL)]
    [InlineData(8, 0x12345678u, 0u, 0UL)]
    [InlineData(12, 0x12345678u, 0x89abcdefu, 0UL)]
    [InlineData(16, 0x12345678u, 0x89abcdefu, 0x76543210UL)]
    [InlineData(20, 0x12345678u, 0x89abcdefu, 0xfedcba9876543210UL)]
    public void TryRead_UsesNativeZeroFilledFieldWidths(int length, uint id, uint document, ulong flags)
    {
        byte[] packet = GameMessages.Truncate(Convert.FromHexString(GoldenFrame), length);
        Assert.True(GameMapInfo1110.TryRead(packet, out GameMapInfo1110 map));
        Assert.Equal(new GameMapInfo1110(id, document, flags), map);
    }

    [Fact]
    public void TryRead_IgnoresExtraBytesAndEnforcesTheSharedBufferBoundary()
    {
        byte[] packet = Convert.FromHexString(GoldenFrame);
        Assert.True(GameMapInfo1110.TryRead(GameMessages.Extend(packet, 1004), out GameMapInfo1110 map));
        Assert.Equal(new GameMapInfo1110(0x12345678, 0x89abcdef, 0xfedcba9876543210), map);
        Assert.False(GameMapInfo1110.TryRead(GameMessages.Extend(packet, 1005), out _));
    }

    [Theory]
    [InlineData(0, 19)]
    [InlineData(2, 0)]
    [InlineData(2, 87)]
    public void TryRead_RejectsInvalidHeadersOrAnotherType(int offset, byte value)
    {
        byte[] packet = Convert.FromHexString(GoldenFrame);
        packet[offset] = value;
        Assert.False(GameMapInfo1110.TryRead(packet, out GameMapInfo1110 map));
        Assert.Equal(default, map);
    }
}
