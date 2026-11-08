using System.Buffers.Binary;
using System.Text.Json;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameWorldAction10010Tests
{
    [Theory]
    [InlineData(GameWorldAction10010.EnterMap, 1000000u)]
    [InlineData(GameWorldAction10010.ClientStateApplied, 0u)]
    [InlineData(GameWorldAction10010.GetItems, 1000000u)]
    [InlineData(GameWorldAction10010.GetStatistics, 1000000u)]
    public void Write_ProducesFixed5517ActionAndRoundTrips(ushort action, uint entityId)
    {
        byte[] packet = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(packet, action, entityId, parameterPair: 0x12345678, timestamp: 123);

        Assert.Equal(38, BinaryPrimitives.ReadUInt16LittleEndian(packet));
        Assert.Equal(10010, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)));
        Assert.Equal(entityId, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)));
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8)));
        Assert.Equal(123u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(16)));
        Assert.Equal(action, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(20)));
        Assert.All(packet.AsSpan(22).ToArray(), value => Assert.Equal(0, value));

        Assert.True(GameWorldAction10010.TryRead(packet, out GameWorldAction10010 read));
        Assert.Equal(new GameWorldAction10010(entityId, 0x12345678, 0, 123, action, 0, 0, 0, 0, 0, 0), read);
    }

    [Fact]
    public void WriteCorrectionReply_UsesCurrentHeroCoordinatesAndFacing()
    {
        byte[] packet = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.WriteCorrectionReply(packet, 0x11223344, 0x1234, 0x5678, direction: 3,
            mapId: 1002, timestamp: 0x55667788);

        Assert.Equal(Convert.FromHexString(
            "26001a27443322113412785600000000887766559200030000000000ea030000ffffffff0000"), packet);
        Assert.True(GameWorldAction10010.TryRead(packet, out GameWorldAction10010 parsed));
        Assert.Equal(new GameWorldAction10010(0x11223344, 0x56781234, 0, 0x55667788,
            GameWorldAction10010.CorrectionReply, 3, 0, 0, 1002, uint.MaxValue, 0), parsed);
    }

    [Fact]
    public void WriteCorrectionReply_ValidatesBeforeTouchingDestination()
    {
        byte[] packet = new byte[GameWorldAction10010.PacketLength];
        packet.AsSpan().Fill(0xAF);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameWorldAction10010.WriteCorrectionReply(packet, 0, 1, 2, 3, 1002, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameWorldAction10010.WriteCorrectionReply(packet, 1, 1, 2, 8, 1002, 5));
        Assert.Throws<ArgumentException>(() =>
            GameWorldAction10010.WriteCorrectionReply(packet.AsSpan(1), 1, 1, 2, 3, 1002, 5));
        Assert.All(packet, b => Assert.Equal((byte)0xAF, b));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    public void TryRead_RejectsMalformedHeaders(int offset, byte corruption)
    {
        byte[] packet = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(packet, GameWorldAction10010.EnterMap, 123);
        packet[offset] = corruption;
        Assert.False(GameWorldAction10010.TryRead(packet, out _));
    }

    [Fact]
    public void TryRead_MatchesRetainedOriginalX86ParserReplayResults()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "Bootstrap", "parser-5517.json")));
        foreach (JsonElement row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            byte[] packet = GameMessages.Header(row.GetProperty("size").GetInt32(), row.GetProperty("packet_type").GetUInt16());
            BinaryPrimitives.WriteUInt16LittleEndian(packet, row.GetProperty("header_size").GetUInt16());
            Assert.Equal(row.GetProperty("accepted").GetBoolean(), GameWorldAction10010.TryRead(packet, out _));
        }
    }

    [Fact]
    public void TryRead_ZeroFillsMissingFieldsAndDoesNotInterpretTrailingStrings()
    {
        byte[] packet = Convert.FromHexString("26001a2778563412896745239a785634ab8967456c00785689679a78efcdab89f0debc9aa500");
        Assert.True(GameWorldAction10010.TryRead(GameMessages.Truncate(packet, 28), out GameWorldAction10010 shortAction));
        Assert.Equal(new GameWorldAction10010(0x12345678, 0x23456789, 0x3456789a, 0x456789ab, 108, 0x5678,
            0x6789, 0x789a, 0, 0, 0), shortAction);
        byte[] extended = GameMessages.Extend(packet, 6);
        extended[37] = byte.MaxValue;
        Assert.True(GameWorldAction10010.TryRead(extended, out GameWorldAction10010 action));
        Assert.True(GameWorldAction10010.TryRead(packet, out GameWorldAction10010 original));
        Assert.Equal(original, action);
    }

    [Fact]
    public void Read_PreservesEveryFieldOfTheStaticGoldenFrame()
    {
        byte[] packet = Convert.FromHexString(
            "26001a2778563412896745239a785634ab8967456c00785689679a78efcdab89f0debc9aa500");

        Assert.Equal(new GameWorldAction10010(0x12345678, 0x23456789, 0x3456789a, 0x456789ab, 108, 0x5678, 0x6789,
            0x789a, 0x89abcdef, 0x9abcdef0, 0xa5), GameWorldAction10010.Read(GameMessages.Load(packet)));
    }

    // Parse 0x6FEB93 has no size rule and does not read the +37 string list; the buffer is zero past the message.
    [Fact]
    public void Read_ReadsZeroPastAShortMessageAndIgnoresTheStringList()
    {
        byte[] packet = Convert.FromHexString(
            "26001a2778563412896745239a785634ab8967456c00785689679a78efcdab89f0debc9aa500");

        Assert.Equal(new GameWorldAction10010(0x12345678, 0x23456789, 0x3456789a, 0x456789ab, 108, 0x5678, 0x6789,
            0x789a, 0, 0, 0), GameWorldAction10010.Read(GameMessages.Load(GameMessages.Truncate(packet, 28))));

        byte[] withStrings = GameMessages.Extend(packet, 6);
        withStrings[37] = 1;
        withStrings[38] = 4;
        Assert.Equal(GameWorldAction10010.Read(GameMessages.Load(packet)), GameWorldAction10010.Read(GameMessages.Load(withStrings)));
    }
}
