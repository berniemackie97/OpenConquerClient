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
        Assert.True(GameWorldPacketDispatcher.Dispatch(Header(68, 1008), handler));
        Assert.True(GameWorldPacketDispatcher.Dispatch(Header(88, 1009), handler));
        Assert.True(GameWorldPacketDispatcher.Dispatch(Header(8, 2030), handler));

        Assert.Equal(
            [
                "action 108 0x12345678",
                "weather 2 0xfedcba98",
                "attributes 0x12345678 2",
                "item 1008 68",
                "item 1009 88",
                "unknown 2030",
            ],
            handler.Calls);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Dispatch_RejectsAFrameWhoseHeaderSizeIsNotTheBufferLength(int declaredDelta)
    {
        byte[] packet = Header(8, 2030);
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)(8 + declaredDelta));
        RecordingHandler handler = new();
        Assert.Throws<InvalidDataException>(() => GameWorldPacketDispatcher.Dispatch(packet, handler));
        Assert.Throws<InvalidDataException>(() => GameWorldPacketDispatcher.Dispatch(packet.AsSpan(0, 3), handler));
        Assert.Empty(handler.Calls);
    }

    [Theory]
    [InlineData(GameWalk10005.PacketType, 8)]
    [InlineData(GameWorldAction10010.PacketType, 37)]
    [InlineData(GameUserAttrib10017.PacketType, 13)]
    [InlineData(GameWeather1016.PacketType, 21)]
    public void Dispatch_RejectsAKnownTypeWhoseLayoutDoesNotMatchItsCodec(ushort type, int length)
    {
        RecordingHandler handler = new();
        Assert.Throws<InvalidDataException>(() => GameWorldPacketDispatcher.Dispatch(Header(length, type), handler));
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

        public bool HandleWalk(GameWalk10005 walk) => Record($"walk {walk.EntityId:x}");

        public bool HandleAction(GameWorldAction10010 action) => Record($"action {action.Action} 0x{action.EntityId:x8}");

        public bool HandleItem(ushort packetType, ReadOnlySpan<byte> packet) => Record($"item {packetType} {packet.Length}");

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
