using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed class WireGoldenAction10010Tests
{
    [Fact]
    public void StaticDerivedActionFrame_PreservesEveryField()
    {
        byte[] frame = WireGoldenFixture.ReadFrame(
            "10010-action.static-derived.json", "action-all-fields-sentinel", GameWorldAction10010.PacketType, "4b56d70dcb3e59d3ed0a1aa316c2d56603e6eb0dd0acb2dfe426ff0cf7d30a03");

        Assert.True(GameWorldAction10010.TryRead(frame, out GameWorldAction10010 action));
        Assert.Equal(new GameWorldAction10010(
            0x12345678, 0x23456789, 0x3456789a, 0x456789ab,
            108, 0x5678, 0x6789, 0x789a,
            0x89abcdef, 0x9abcdef0, 0xa5), action);
    }
}
