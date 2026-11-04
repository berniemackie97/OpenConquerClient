using System.Buffers.Binary;
using OpenConquer.Client.World;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

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
        Assert.Equal((byte)3, state.DirectionIndex);
        Assert.Equal((1u, 0u, 0u, 0u), state.Weather);
    }

    [Fact]
    public void CorrectionUpdatesTheLocalPositionOnlyForTheAuthenticatedHero()
    {
        WorldInboundState state = CreateState();
        Assert.True(state.Apply(Correction(HeroId, 62, 102)));
        Assert.Equal(((ushort)62, (ushort)102), (state.X, state.Y));
        Assert.False(state.Apply(Correction(HeroId, 62, 102)));
        Assert.Throws<InvalidDataException>(() => state.Apply(Correction(entityId: HeroId + 1, x: 63, y: 103)));
        Assert.Equal(((ushort)62, (ushort)102), (state.X, state.Y));
        Assert.Equal((byte)3, state.DirectionIndex);
    }

    [Theory]
    [InlineData(0, 61, 102)]
    [InlineData(1, 60, 102)]
    [InlineData(2, 60, 101)]
    [InlineData(3, 60, 100)]
    [InlineData(4, 61, 100)]
    [InlineData(5, 62, 100)]
    [InlineData(6, 62, 101)]
    [InlineData(7, 62, 102)]
    public void LocalWalkEchoAdvancesTheAuthoritativeTile(byte direction, ushort x, ushort y)
    {
        WorldInboundState state = CreateState();
        byte[] packet = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(packet, HeroId, direction, movementTick: 100, mapState: 0);

        Assert.True(state.Apply(packet));
        Assert.Equal((x, y), (state.X, state.Y));
        Assert.Equal(direction, state.DirectionIndex);
    }

    [Fact]
    public void RemoteWalkDoesNotMoveTheLocalCamera()
    {
        WorldInboundState state = CreateState();
        byte[] packet = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(packet, HeroId + 1, 6, movementTick: 100, mapState: 0);

        Assert.False(state.Apply(packet));
        Assert.Equal(((ushort)61, (ushort)101), (state.X, state.Y));
        Assert.Equal((byte)3, state.DirectionIndex);
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
    public void CharacterInfoSeedsWorldHeroAttributesBeforeAnyWorldPacket()
    {
        WorldHeroAttributes stats = CreateState().HeroAttributes;
        Assert.Equal((5u, 6u, 7u, 8u),
            (stats.Strength, stats.Agility, stats.Vitality, stats.Spirit));
        Assert.Equal((70u, 20u, 0u),
            (stats.CurrentLife, stats.CurrentMana, stats.CurrentStamina));
        Assert.Equal((42u, 10u, 12345ul),
            (stats.Level, stats.Profession, stats.Experience));
        Assert.Equal((0u, 0u, 0u),
            (stats.MaximumLifeOverride, stats.MaximumManaOverride, stats.SkillGaugePercent));
    }

    [Fact]
    public void AttributeEntriesApplyInWireOrderWithoutChangingTheWorldPosition()
    {
        WorldInboundState state = CreateState();
        byte[] packet = Attributes(HeroId,
            (0, 100, 0), (2, 40, 0), (8, 90, 0), (5, 1UL << 40, 0),
            (25, 3, 2), (27, 145, 0), (1, 250, 0), (3, 180, 0));

        Assert.False(state.Apply(packet));

        WorldHeroAttributes stats = state.HeroAttributes;
        Assert.Equal((100u, 40u, 90u), (stats.CurrentLife, stats.CurrentMana, stats.CurrentStamina));
        Assert.Equal(1UL << 40, stats.Experience);
        Assert.Equal((3ul, 2ul), (stats.StatusLow, stats.StatusHigh));
        Assert.Equal((250u, 180u), (stats.MaximumLifeOverride, stats.MaximumManaOverride));
        Assert.Equal(100u, stats.SkillGaugePercent);
        Assert.Equal(((ushort)61, (ushort)101), (state.X, state.Y));

        Assert.False(state.Apply(Attributes(HeroId, (0, 95, 0), (0, 91, 0))));
        Assert.Equal(91u, state.HeroAttributes.CurrentLife);
    }

    [Fact]
    public void CharacterProgressionAndOverlayAttributesRemainDirectPacketState()
    {
        WorldInboundState state = CreateState();

        Assert.False(state.Apply(Attributes(HeroId,
            (7, 132, 0), (10, 23, 0), (12, 98, 0), (22, 2, 0), (43, 150, 0))));

        WorldHeroAttributes stats = state.HeroAttributes;
        Assert.Equal((132u, 23u, 98u, 2u, 150u),
            (stats.Profession, stats.UnspentAttributePoints, stats.Level, stats.RebirthCount,
                stats.OverlayLifeBonus));
    }

    [Fact]
    public void UnsupportedHeroAttributeDoesNotOverwritePreviouslyVerifiedState()
    {
        WorldInboundState state = CreateState();

        Assert.False(state.Apply(Attributes(HeroId, (128, ulong.MaxValue, ulong.MaxValue))));
        Assert.Equal((70u, 20u), (state.HeroAttributes.CurrentLife, state.HeroAttributes.CurrentMana));
        Assert.Equal((0ul, 0ul), (state.HeroAttributes.StatusLow, state.HeroAttributes.StatusHigh));
    }

    [Fact]
    public void AttributePacketsRejectMalformedCountsWithoutPartialChanges()
    {
        WorldInboundState state = CreateState();
        byte[] packet = Attributes(HeroId, (0, 60, 0), (2, 30, 0));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), 3);

        Assert.Throws<InvalidDataException>(() => state.Apply(packet));
        Assert.Equal((70u, 20u),
            (state.HeroAttributes.CurrentLife, state.HeroAttributes.CurrentMana));

        packet = Attributes(HeroId, (0, 60, 0));
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)(packet.Length - 1));
        Assert.Throws<InvalidDataException>(() => state.Apply(packet));
        Assert.Equal(70u, state.HeroAttributes.CurrentLife);
    }

    [Fact]
    public void RemoteAttributeUpdatesCannotMutateTheAuthenticatedHero()
    {
        WorldInboundState state = CreateState();
        Assert.False(state.Apply(Attributes(HeroId + 1, (0, 1, 0), (2, 1, 0))));
        Assert.Equal((70u, 20u),
            (state.HeroAttributes.CurrentLife, state.HeroAttributes.CurrentMana));
    }

    [Fact]
    public void LiveEquipmentPacketsUpdateTheAuthenticatedWorldWithoutMovingItsCamera()
    {
        WorldInboundState state = CreateState();
        Assert.Equal(0u, state.EquipmentRevision);

        Assert.False(state.Apply(Item(101, 120020, placement: 1)));
        Assert.Equal(1u, state.EquipmentRevision);
        Assert.Null(state.Equipment.GetActiveItem(1));

        Assert.False(state.Apply(ActiveEquipment(headwear: 101)));
        Assert.Equal(2u, state.EquipmentRevision);
        Assert.Equal(120020u, state.Equipment.GetActiveItem(1)?.TypeId);

        byte[] update = Item(101, 120020, placement: 1);
        update[16] = 3;
        BinaryPrimitives.WriteUInt16LittleEndian(update.AsSpan(12), 0);
        Assert.False(state.Apply(update));
        Assert.Equal((ushort)0, state.Equipment.GetActiveItem(1)?.Durability);

        Assert.False(state.Apply(TickRemove(101)));
        Assert.Null(state.Equipment.GetActiveItem(1));
        Assert.Equal(4u, state.EquipmentRevision);
        Assert.Equal(((ushort)61, (ushort)101), (state.X, state.Y));
    }

    [Fact]
    public void ActiveSnapshotClampsCurrentLifeOnlyAfterTheEffectiveMaximumIsKnown()
    {
        WorldInboundState state = CreateState();
        state.Apply(Item(101, 120020, placement: 1));
        state.Apply(ActiveEquipment(headwear: 101));

        state.ApplyEquipmentLifeClamp(60);
        Assert.Equal(60u, state.HeroAttributes.CurrentLife);

        state.Apply(Attributes(HeroId, (0, 90, 0)));
        state.ApplyEquipmentLifeClamp(60);
        Assert.Equal(90u, state.HeroAttributes.CurrentLife);
    }

    [Fact]
    public void MalformedEquipmentPacketsDoNotPartiallyChangeWorldState()
    {
        WorldInboundState state = CreateState();
        state.Apply(Item(101, 120020, placement: 1));
        byte[] snapshot = ActiveEquipment(headwear: 101);
        BinaryPrimitives.WriteUInt16LittleEndian(snapshot, 87);

        Assert.Throws<InvalidDataException>(() => state.Apply(snapshot));
        Assert.Null(state.Equipment.GetActiveItem(1));
        Assert.Equal(1u, state.EquipmentRevision);
    }

    [Fact]
    public void WorldIngressAppliesInventoryEquipmentAndDurabilityInPacketOrder()
    {
        WorldInboundState state = CreateState();
        byte[] item = new byte[68];
        BinaryPrimitives.WriteUInt16LittleEndian(item, 68);
        BinaryPrimitives.WriteUInt16LittleEndian(item.AsSpan(2), 1008);
        BinaryPrimitives.WriteUInt32LittleEndian(item.AsSpan(4), 100);
        BinaryPrimitives.WriteUInt32LittleEndian(item.AsSpan(8), 120020);
        BinaryPrimitives.WriteUInt16LittleEndian(item.AsSpan(12), 40);
        item[16] = 1;

        Assert.False(state.Apply(item));
        Assert.Equal(1u, state.EquipmentRevision);
        Assert.Null(state.Equipment.GetActiveItem(1));

        byte[] equip = new byte[88];
        BinaryPrimitives.WriteUInt16LittleEndian(equip, 88);
        BinaryPrimitives.WriteUInt16LittleEndian(equip.AsSpan(2), 1009);
        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(4), 100);
        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(equip.AsSpan(12), 5);

        Assert.False(state.Apply(equip));
        Assert.Equal(2u, state.EquipmentRevision);
        Assert.Equal(100u, state.Equipment.GetActiveItem(1)?.Id);

        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(4), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(equip.AsSpan(12), 17);
        Assert.False(state.Apply(equip));
        Assert.Equal(3u, state.EquipmentRevision);
        Assert.Equal((ushort)0, state.Equipment.GetActiveItem(1)?.Durability);
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
            new GameCharacterInfo1006
            {
                EntityId = HeroId,
                Strength = 5,
                Agility = 6,
                Vitality = 7,
                Spirit = 8,
                CurrentLife = 70,
                CurrentMana = 20,
                Level = 42,
                CurrentProfession = 10,
                Experience = 12345,
            },
            1002, 1010, 0, 1, 61, 101, [], 3));

    private static byte[] Attributes(uint entityId, params (uint Id, ulong Low, ulong High)[] entries)
    {
        byte[] frame = new byte[12 + entries.Length * 20];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 10017);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), entityId);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8), (uint)entries.Length);
        for (int i = 0; i < entries.Length; i++)
        {
            Span<byte> entry = frame.AsSpan(12 + i * 20, 20);
            BinaryPrimitives.WriteUInt32LittleEndian(entry, entries[i].Id);
            BinaryPrimitives.WriteUInt64LittleEndian(entry[4..], entries[i].Low);
            BinaryPrimitives.WriteUInt64LittleEndian(entry[12..], entries[i].High);
        }

        return frame;
    }

    private static byte[] Item(uint id, uint typeId, byte placement)
    {
        byte[] frame = new byte[68];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, 68);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 1008);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), id);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8), typeId);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(12), 40);
        frame[16] = 1;
        frame[18] = placement;
        return frame;
    }

    private static byte[] ActiveEquipment(uint headwear)
    {
        byte[] frame = new byte[88];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, 88);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 1009);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(12), 46);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(32), headwear);
        return frame;
    }

    private static byte[] TickRemove(uint id)
    {
        byte[] frame = new byte[88];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, 88);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 1009);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), id);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(12), 3);
        return frame;
    }

    private static byte[] Correction(uint entityId, ushort x, ushort y)
    {
        byte[] frame = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(frame, 108, entityId);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(24), x);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(26), y);
        return frame;
    }
}
