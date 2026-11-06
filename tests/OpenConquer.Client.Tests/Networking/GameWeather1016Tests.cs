using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameWeather1016Tests
{
    // typed-world-dispatch-2026-11-06/wire-layout-golden.json "weather".
    private const string GoldenFrame = "1400f80302000000fe0300006701000098badcfe";

    [Fact]
    public void Read_PreservesTheFullWidthGoldenFields()
    {
        GameWeather1016 weather = GameWeather1016.Read(GameMessages.Load(Convert.FromHexString(GoldenFrame)));
        Assert.Equal(new GameWeather1016(2, 1022, 359, 0xfedcba98), weather);
        Assert.True(weather.HasSupportedValues);
    }

    // Parse 0x7607DE has no size rule; the buffer is zero past the message and extra bytes are not fields.
    [Fact]
    public void Read_ReadsZeroPastAShortMessageAndIgnoresExtraBytes()
    {
        byte[] golden = Convert.FromHexString(GoldenFrame);
        Assert.Equal(new GameWeather1016(2, 1022, 0, 0), GameWeather1016.Read(GameMessages.Load(GameMessages.Truncate(golden, 12))));
        Assert.Equal(new GameWeather1016(2, 1022, 359, 0xfedcba98), GameWeather1016.Read(GameMessages.Load(GameMessages.Extend(golden, 8))));
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
