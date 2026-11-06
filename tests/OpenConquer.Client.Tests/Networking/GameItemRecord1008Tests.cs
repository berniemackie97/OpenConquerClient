using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameItemRecord1008Tests
{
    // Synthetic sentinel frame built from the 20_notes 1008 layout, not a capture. Unconsumed offsets carry noise.
    private const string SentinelFrame =
        "4400f00378563412f1ee0b004523000001ee09dd000000000000000000000000005a00006b000000cc00000000000000" +
        "00000000000000000000000000000000bb000000";

    [Fact]
    public void Read_DecodesTheConsumedFieldsAtTheirNativeOffsets() =>
        Assert.Equal(new GameItemRecord1008(0x12345678, 0x000BEEF1, 0x2345, 1, 9, 0x5a, 0x6b),
            GameItemRecord1008.Read(GameMessages.Load(Convert.FromHexString(SentinelFrame))));

    // Parse 0x762AA0 has no size rule; the buffer is zero past the message and extra bytes are not fields.
    [Fact]
    public void Read_ReadsZeroPastAShortRecordAndIgnoresExtraBytes()
    {
        byte[] sentinel = Convert.FromHexString(SentinelFrame);
        Assert.Equal(new GameItemRecord1008(0x12345678, 0x000BEEF1, 0x2345, 1, 9, 0, 0),
            GameItemRecord1008.Read(GameMessages.Load(GameMessages.Truncate(sentinel, 20))));
        Assert.Equal(GameItemRecord1008.Read(GameMessages.Load(sentinel)),
            GameItemRecord1008.Read(GameMessages.Load(GameMessages.Extend(sentinel, 12))));
    }
}
