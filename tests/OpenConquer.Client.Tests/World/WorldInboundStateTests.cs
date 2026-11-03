using System.Buffers.Binary;
using OpenConquer.Client.World;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldInboundStateTests
{
    private const uint HeroId = 0x11223344;

    [Fact]
    public void WorldStateStartsFromAuthenticatedAdmission()
    {
        WorldInboundState state = CreateState();
        Assert.Equal((ushort)61, state.X);
        Assert.Equal((ushort)101, state.Y);
        Assert.Equal((1u, 0u, 0u, 0u), state.Weather);
    }

    [Fact]
    public void CorrectionUpdatesTheLocalPositionOnlyForTheAuthenticatedHero()
    {
        WorldInboundState state = CreateState();
        Assert.True(state.Apply(Correction(HeroId, 62, 102)));
        Assert.Equal(((ushort)62, (ushort)102), (state.X, state.Y));
        Assert.False(state.Apply(Correction(HeroId, 62, 102)));
        Assert.Throws<InvalidDataException>(() => state.Apply(Correction(OtherHeroId: HeroId + 1, x: 63, y: 103)));
        Assert.Equal(((ushort)62, (ushort)102), (state.X, state.Y));
    }

    [Fact]
    public void WeatherChangesAreBoundedAndDoNotChangePosition()
    {
        WorldInboundState state = CreateState();
        byte[] weather = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(weather, 20);
        BinaryPrimitives.WriteUInt16LittleEndian(weather.AsSpan(2), 1016);
        BinaryPrimitives.WriteUInt32LittleEndian(weather.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(weather.AsSpan(8), 900);
        BinaryPrimitives.WriteUInt32LittleEndian(weather.AsSpan(12), 270);
        BinaryPrimitives.WriteUInt32LittleEndian(weather.AsSpan(16), 17);

        Assert.False(state.Apply(weather));
        Assert.Equal((2u, 900u, 270u, 17u), state.Weather);
        Assert.Equal(((ushort)61, (ushort)101), (state.X, state.Y));
        BinaryPrimitives.WriteUInt32LittleEndian(weather.AsSpan(8), 1023);
        Assert.Throws<InvalidDataException>(() => state.Apply(weather));
        Assert.Equal((2u, 900u, 270u, 17u), state.Weather);
    }

    [Fact]
    public void MalformedPacketHeaderIsRejectedBeforeDispatch()
    {
        WorldInboundState state = CreateState();
        byte[] correction = Correction(HeroId, 61, 102);
        BinaryPrimitives.WriteUInt16LittleEndian(correction, 37);
        Assert.Throws<InvalidDataException>(() => state.Apply(correction));
    }

    private static WorldInboundState CreateState() =>
        new(new GameWorldEntrySnapshot(
            new GameCharacterInfo1006 { EntityId = HeroId },
            1002, 1010, 0, 1, 61, 101, []));

    private static byte[] Correction(uint OtherHeroId, ushort x, ushort y)
    {
        byte[] frame = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(frame, 108, OtherHeroId);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(24), x);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(26), y);
        return frame;
    }
}
