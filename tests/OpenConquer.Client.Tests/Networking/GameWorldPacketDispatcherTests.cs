using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameWorldPacketDispatcherTests
{
    [Fact]
    public void Dispatch_RoutesEachTypeToItsTypedHandler()
    {
        RecordingHandler handler = new();

        Assert.True(GameWorldPacketDispatcher.Dispatch(Convert.FromHexString(
            "26001a2778563412896745239a785634ab8967456c00785689679a78efcdab89f0debc9aa500"), handler));
        Assert.True(GameWorldPacketDispatcher.Dispatch(Convert.FromHexString(
            "1400f80302000000fe0300006701000098badcfe"), handler));
        Assert.True(GameWorldPacketDispatcher.Dispatch(Convert.FromHexString(
            "340021277856341202000000050000001032547698badcfe000000000000000019000000efcdab89674523011032547698badcfe"),
            handler));
        byte[] record = Header(68, 1008);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4), 77);
        record[18] = 3;
        Assert.True(GameWorldPacketDispatcher.Dispatch(record, handler));
        byte[] item = Header(88, 1009);
        BinaryPrimitives.WriteUInt16LittleEndian(item.AsSpan(12), 46);
        Assert.True(GameWorldPacketDispatcher.Dispatch(item, handler));
        Assert.True(GameWorldPacketDispatcher.Dispatch(Header(8, 2030), handler));

        Assert.Equal(
            [
                "action 108 0x12345678",
                "weather 2 0xfedcba98",
                "attributes 0x12345678 2",
                "record 77 3",
                "item 46 88",
                "unknown 2030",
            ],
            handler.Calls);
    }

    // Native dispatch returns no message for these and the receive loop skips them (0x6DEC3A).
    [Fact]
    public void Dispatch_DropsWhatNativeDispatchDropsWithoutCallingTheHandler()
    {
        RecordingHandler handler = new();
        byte[] mismatched = Header(8, 2030);
        BinaryPrimitives.WriteUInt16LittleEndian(mismatched, 11);

        Assert.False(GameWorldPacketDispatcher.Dispatch(mismatched, handler));
        Assert.False(GameWorldPacketDispatcher.Dispatch(mismatched.AsSpan(0, 3), handler));
        Assert.False(GameWorldPacketDispatcher.Dispatch(Header(8, 0), handler));
        Assert.False(GameWorldPacketDispatcher.Dispatch(new byte[0x401], handler));
        Assert.Empty(handler.Calls);
    }

    // No native parser for these types has a size rule; short messages reach Process with zero fields.
    [Fact]
    public void Dispatch_DeliversEveryKnownTypeAtLengthsTheOldExactRulesRejected()
    {
        RecordingHandler handler = new();

        Assert.True(GameWorldPacketDispatcher.Dispatch(Header(8, GameWalk10005.PacketType), handler));
        Assert.True(GameWorldPacketDispatcher.Dispatch(Header(37, GameWorldAction10010.PacketType), handler));
        Assert.True(GameWorldPacketDispatcher.Dispatch(Header(13, GameUserAttrib10017.PacketType), handler));
        Assert.True(GameWorldPacketDispatcher.Dispatch(Header(21, GameWeather1016.PacketType), handler));
        Assert.True(GameWorldPacketDispatcher.Dispatch(Header(67, GameItemRecord1008.PacketType), handler));
        Assert.True(GameWorldPacketDispatcher.Dispatch(Header(69, GameItemRecord1008.PacketType), handler));
        Assert.True(GameWorldPacketDispatcher.Dispatch(Header(13, GameItem1009.PacketType), handler));

        Assert.Equal(
            ["walk 0", "action 0 0x00000000", "attributes 0x00000000 0", "weather 0 0x00000000", "record 0 0", "record 0 0",
                "item 0 13"],
            handler.Calls);
    }

    [Fact]
    public void Dispatch_DecodesOwnedPlayerSpawnFields()
    {
        RecordingHandler handler = new();
        byte[] frame = WorldSpawnPacketFixture.Create(1_000_001, name: "Peer");
        Assert.True(GameWorldPacketDispatcher.Dispatch(frame, handler));

        GamePlayerSpawn10014 player = Assert.IsType<GamePlayerSpawn10014>(handler.LastPlayer);
        Assert.Equal(1_000_001u, player.EntityId);
        Assert.Equal(1003u, player.PackedAppearance);
        Assert.Equal((ushort)810, player.HairComposite);
        Assert.Equal(((ushort)62, (ushort)101, (byte)3), (player.X, player.Y, player.Direction));
        Assert.Equal(250u, player.Life);
        Assert.Equal((ushort)120, player.Level);
        Assert.Equal((111000u, 130000u, 420000u, 410000u),
            (player.HeadwearItemType, player.ArmorItemType, player.LeftHandItemType,
                player.RightHandItemType));
        Assert.Equal((0x1234UL, 0x5678UL), (player.StatusMaskLow, player.StatusMaskHigh));
        frame[220] = (byte)'X';
        Assert.Equal("Peer", System.Text.Encoding.ASCII.GetString(player.NameBytes.Span));
    }

    [Fact]
    public void Dispatch_SkipsMalformedPlayerSpawnWithoutInvokingHandler()
    {
        RecordingHandler handler = new();
        byte[] truncated = WorldSpawnPacketFixture.Create(1_000_001);
        Array.Resize(ref truncated, truncated.Length - 1);
        BinaryPrimitives.WriteUInt16LittleEndian(truncated, checked((ushort)truncated.Length));
        byte[] missingName = WorldSpawnPacketFixture.Create(1_000_001);
        missingName[219] = 0;
        byte[] invalidDirection = WorldSpawnPacketFixture.Create(1_000_001);
        invalidDirection[90] = 8;

        Assert.False(GameWorldPacketDispatcher.Dispatch(truncated, handler));
        Assert.False(GameWorldPacketDispatcher.Dispatch(missingName, handler));
        Assert.False(GameWorldPacketDispatcher.Dispatch(invalidDirection, handler));
        Assert.Empty(handler.Calls);
    }

    private static byte[] Header(int length, ushort type)
    {
        byte[] packet = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), type);
        return packet;
    }

    private sealed class RecordingHandler : IGameWorldPacketHandler
    {
        public List<string> Calls { get; } = [];
        public GamePlayerSpawn10014? LastPlayer { get; private set; }

        public bool HandlePlayerSpawn(GamePlayerSpawn10014 player)
        {
            LastPlayer = player;
            return Record($"spawn {player.EntityId}");
        }

        public bool HandleWalk(GameWalk10005 walk) => Record($"walk {walk.EntityId:x}");

        public bool HandleAction(GameWorldAction10010 action) => Record($"action {action.Action} 0x{action.EntityId:x8}");

        public bool HandleItemRecord(GameItemRecord1008 record) => Record($"record {record.ItemId} {record.Position}");

        public bool HandleItem(GameItem1009 item) => Record($"item {item.Subtype} {item.Length}");

        public bool HandleAttributes(GameUserAttrib10017 attributes) =>
            Record($"attributes 0x{attributes.TargetId:x8} {attributes.Count}");

        public bool HandleWeather(GameWeather1016 weather) => Record($"weather {weather.Kind} 0x{weather.Parameter:x8}");

        public bool HandleUnknown(ushort packetType) => Record($"unknown {packetType}");

        private bool Record(string call)
        {
            Calls.Add(call);
            return true;
        }
    }
}
