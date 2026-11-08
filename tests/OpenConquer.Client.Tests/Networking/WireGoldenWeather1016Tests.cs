using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class WireGoldenWeather1016Tests
{
    [Fact]
    public void StaticDerivedWeatherFrame_IsPinnedAndDecodesAsDocumented()
    {
        byte[] frame = WireGoldenFixture.ReadFrame(
            "1016-weather.static-derived.json", GameWeather1016.PacketType, "27925e3dd6c73095eca525c2c03f12449fd3d0894f4a1c06ce6628c3f89d53ee");

        Assert.True(GameWeather1016.TryRead(frame, out GameWeather1016 weather));
        Assert.Equal(new GameWeather1016(2, 1022, 359, 0xfedcba98), weather);
    }
}
