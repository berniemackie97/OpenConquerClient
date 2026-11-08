using System.Buffers.Binary;
using OpenConquer.Client.Diagnostics;
using OpenConquer.Client.Tests.Diagnostics;
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
        Assert.Equal(61, state.X);
        Assert.Equal(101, state.Y);
        Assert.Equal((byte)3, state.DirectionIndex);
        Assert.Equal((1u, 0u, 0u, 0u), state.Weather);
    }

    [Fact]
    public void CorrectionUpdatesTheLocalPositionOnlyForTheAuthenticatedHero()
    {
        WorldInboundState state = CreateState();
        Assert.True(state.Apply(Correction(HeroId, 62, 102)));
        Assert.Equal((62, 102), (state.X, state.Y));
        Assert.False(state.Apply(Correction(HeroId, 62, 102)));
        Assert.False(state.Apply(Correction(entityId: HeroId + 1, x: 63, y: 103)));
        Assert.Equal((62, 102), (state.X, state.Y));
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
        Assert.Equal(((int)x, (int)y), (state.X, state.Y));
        Assert.Equal(direction, state.DirectionIndex);
    }

    [Fact]
    public void RemoteWalkDoesNotMoveTheLocalCamera()
    {
        WorldInboundState state = CreateState();
        byte[] packet = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(packet, HeroId + 1, 6, movementTick: 100, mapState: 0);

        Assert.False(state.Apply(packet));
        Assert.Equal((61, 101), (state.X, state.Y));
        Assert.Equal((byte)3, state.DirectionIndex);
    }

    [Theory]
    [InlineData(0, 0, 3, -1, -1)]
    [InlineData(0, 0, 2, -1, 0)]
    [InlineData(0, 0, 4, 0, -1)]
    [InlineData(65535, 65535, 7, 65536, 65536)]
    [InlineData(65535, 61, 6, 65536, 61)]
    [InlineData(61, 65535, 0, 61, 65536)]
    public void WalkAcrossUnsignedWordBoundaryKeepsSignedTilesAndAcceptsLaterCorrection(
        ushort startX, ushort startY, byte direction, int expectedX, int expectedY)
    {
        WorldInboundState state = CreateState();
        state.Apply(Correction(HeroId, startX, startY));
        byte[] walk = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(walk, HeroId, direction, movementTick: 1, mapState: 0);

        Assert.True(state.Apply(walk));

        Assert.Equal((expectedX, expectedY), (state.X, state.Y));
        Assert.Equal(direction, state.DirectionIndex);
        Assert.True(state.Apply(Correction(HeroId, 61, 101)));
        Assert.Equal((61, 101), (state.X, state.Y));
        Assert.Equal(direction, state.DirectionIndex);
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
        Assert.Equal((61, 101), (state.X, state.Y));
        BinaryPrimitives.WriteUInt32LittleEndian(weather.AsSpan(8), 1023);
        Assert.False(state.Apply(weather));
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
        Assert.Equal((61, 101), (state.X, state.Y));

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

    // Native loops the declared count with no length check (0x758B92): an entry past the message is attribute 0 value 0.
    [Fact]
    public void AttributeCountPastTheMessageAppliesZeroEntriesToTheHero()
    {
        WorldInboundState state = CreateState();
        byte[] packet = Attributes(HeroId, (0, 60, 0), (2, 30, 0));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), 3);

        Assert.False(state.Apply(packet));
        Assert.Equal((0u, 30u), (state.HeroAttributes.CurrentLife, state.HeroAttributes.CurrentMana));
    }

    // Native dispatch drops a message whose size word is not its byte count and the receive loop continues (0x6DEC3A).
    [Fact]
    public void FramesWithAMismatchedSizeWordAreSkippedWithoutChangingState()
    {
        WorldInboundState state = CreateState();
        byte[] attributes = Attributes(HeroId, (0, 60, 0));
        BinaryPrimitives.WriteUInt16LittleEndian(attributes, (ushort)(attributes.Length - 1));
        byte[] correction = Correction(HeroId, 61, 102);
        BinaryPrimitives.WriteUInt16LittleEndian(correction, 37);
        (int x, int y) = (state.X, state.Y);

        Assert.False(state.Apply(attributes));
        Assert.False(state.Apply(correction));
        Assert.Equal((70u, x, y), (state.HeroAttributes.CurrentLife, state.X, state.Y));
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
        Assert.Equal((61, 101), (state.X, state.Y));
    }

    [Fact]
    public void ActiveSnapshotClampsAtItsOwnPositionInTheBatchAndNotALaterLifeWrite()
    {
        // Base maximum life for the test hero is (5 + 6 + 7*8 + 8) * 3 = 225 with no active items.
        WorldInboundState state = CreateState();
        state.Apply(Attributes(HeroId, (0, 900, 0)));

        state.Apply(ActiveEquipment(headwear: 0));
        Assert.Equal(225u, state.HeroAttributes.CurrentLife);

        state.Apply(Attributes(HeroId, (0, 900, 0)));
        Assert.Equal(900u, state.HeroAttributes.CurrentLife);
    }

    [Fact]
    public void HydrationSnapshotClampsTheAdmissionLifeBeforeAnyLivePacket()
    {
        WorldInboundState state = CreateState(currentLife: 900, hydration: [ActiveEquipment(headwear: 0)]);
        Assert.Equal(225u, state.HeroAttributes.CurrentLife);
        Assert.Equal(1u, state.EquipmentRevision);
    }

    [Fact]
    public void SnapshotLeavesLifeUnclampedWhileATransformLookSourceIsActive()
    {
        WorldInboundState state = CreateState(currentLife: 900, hydration: [ActiveEquipment(headwear: 0)],
            transformLookSourceId: 1);
        Assert.Equal(900u, state.HeroAttributes.CurrentLife);
    }

    [Fact]
    public void EquipmentFrameWithAMismatchedSizeWordIsSkipped()
    {
        WorldInboundState state = CreateState();
        state.Apply(Item(101, 120020, placement: 1));
        byte[] snapshot = ActiveEquipment(headwear: 101);
        BinaryPrimitives.WriteUInt16LittleEndian(snapshot, 87);

        Assert.False(state.Apply(snapshot));
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

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(22)]
    [InlineData(28)]
    [InlineData(32)]
    [InlineData(36)]
    [InlineData(37)]
    public void CorrectionIgnoresFieldsNativeDoesNotRead(int offset)
    {
        WorldInboundState state = CreateState();
        byte[] correction = Correction(HeroId, 62, 102);
        correction[offset] = 1;
        Assert.True(state.Apply(correction));
        Assert.Equal((62, 102), (state.X, state.Y));
        Assert.Equal((byte)3, state.DirectionIndex);
    }

    [Fact]
    public void StaticGoldenCorrectionAppliesOnlyItsHeroTileFields()
    {
        // Static-disassembly-derived sentinel frame from CP2.3a. Native 108 reads only target and X/Y (0x6FFCBF..CF3).
        WorldInboundState state = CreateState();
        byte[] golden = Convert.FromHexString(
            "26001a2778563412896745239a785634ab8967456c00785689679a78efcdab89f0debc9aa500");
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(4), HeroId);
        Assert.True(state.Apply(golden));
        Assert.Equal((0x6789, 0x789A), (state.X, state.Y));
        Assert.Equal((byte)3, state.DirectionIndex);
    }

    [Fact]
    public void WeatherKindSixIsRefusedWithoutChangingWeather()
    {
        WorldInboundState state = CreateState();
        byte[] weather = Convert.FromHexString("1400f80306000000000000000000000000000000");
        Assert.False(state.Apply(weather));
        Assert.Equal((1u, 0u, 0u, 0u), state.Weather);
    }

    [Theory]
    [InlineData(0u, 0u, 0u)]
    [InlineData(6u, 0u, 0u)]
    [InlineData(12u, 0u, 0u)]
    [InlineData(uint.MaxValue, 0u, 0u)]
    [InlineData(2u, 1023u, 0u)]
    [InlineData(2u, uint.MaxValue, 0u)]
    [InlineData(2u, 0u, 360u)]
    [InlineData(2u, 0u, uint.MaxValue)]
    public void NativeRejectedWeatherRetainsPreviousStateAndDoesNotStopLaterPackets(uint kind, uint intensity, uint direction)
    {
        // Signed input gates 0x7606D6..0x760711; kind6 table entry -> return0 at 0x7607A9.
        WorldInboundState state = CreateState();
        Assert.False(state.Apply(Weather(2, 1022, 359, uint.MaxValue)));

        Assert.False(state.Apply(Weather(kind, intensity, direction, 17)));

        Assert.Equal((2u, 1022u, 359u, uint.MaxValue), state.Weather);
        Assert.True(state.Apply(Correction(HeroId, 62, 102)));
        Assert.False(state.Apply(Weather(11, 0, 0, 0)));
        Assert.Equal((11u, 0u, 0u, 0u), state.Weather);
    }

    [Theory]
    [InlineData(0u, 120020u)]
    [InlineData(102u, 0u)]
    [InlineData(102u, 130003u)]
    public void InvalidOrMissingItemIdentityIsSkippedBeforeSnapshotResolution(uint id, uint type)
    {
        WorldInboundState state = CreateState();
        Assert.False(state.Apply(Item(id, type, placement: 1)));
        Assert.Equal(0u, state.EquipmentRevision);
        Assert.False(state.Apply(ActiveEquipment(headwear: id)));
        Assert.Null(state.Equipment.GetActiveItem(1));
        Assert.False(state.Apply(Item(101, 120020, placement: 1)));
        Assert.False(state.Apply(ActiveEquipment(headwear: 101)));
        Assert.Equal(101u, state.Equipment.GetActiveItem(1)?.Id);
        Assert.Equal(3u, state.EquipmentRevision);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(130003u)]
    public void MissingTypeChangeLeavesExistingActiveItemAndRevisionUntouched(uint type)
    {
        WorldInboundState state = CreateState(hydration: [Item(101, 120020, 1), ActiveEquipment(101)]);
        WorldHeroItem original = Assert.IsType<WorldHeroItem>(state.Equipment.GetActiveItem(1));
        byte[] update = Item(101, type, placement: 15);
        update[16] = 3;
        BinaryPrimitives.WriteUInt16LittleEndian(update.AsSpan(12), 0);

        Assert.False(state.Apply(update));

        Assert.Same(original, state.Equipment.GetActiveItem(1));
        Assert.Null(state.Equipment.GetActiveItem(15));
        Assert.Equal((120020u, (ushort)40, 2u), (original.TypeId, original.Durability, state.EquipmentRevision));
        Assert.False(state.Apply(ActiveEquipment(101)));
        Assert.False(state.Apply(Attributes(HeroId, (0, 77, 0))));
        Assert.Equal(77u, state.HeroAttributes.CurrentLife);
    }

    // Native processes each world-entry frame when it arrives: a correction before the EnterMap reply is replaced by
    // the reply's position, one after it moves the hero (CP2.4d2b, 20_notes Network/bootstrap-tolerance-cp24d.md).
    [Theory]
    [InlineData(1, 61, 101)]
    [InlineData(0, 70, 80)]
    public void HydrationFramesApplyInArrivalOrderAroundTheEntryPosition(int enteredAt, int x, int y)
    {
        WorldInboundState state = CreateState(hydration: [Correction(HeroId, 70, 80), Attributes(HeroId, (0, 55, 0))],
            hydrationEnteredAt: enteredAt);

        Assert.Equal(((ushort)x, (ushort)y, (byte)3), (state.X, state.Y, state.DirectionIndex));
        Assert.Equal(55u, state.HeroAttributes.CurrentLife);
    }

    [Theory]
    [InlineData(5_000u, 4_250u, 750u)]
    [InlineData(7u, 4_294_967_282u, 21u)]
    public void LatencyTick_UsesTheRawEchoedTimerAcrossUnsignedWrap(uint now, uint sentAt, uint elapsed)
    {
        WorldInboundState state = new(new GameWorldEntrySnapshot(new GameCharacterInfo1006 { EntityId = HeroId },
            1002, 1010, 0, 1, 61, 101, [], 3), WorldHeroItemStatsTests.Catalog(), () => now);
        byte[] tick = new byte[GameItem1009.FixedLength];
        GameLatencyTick1009.Write(tick, HeroId, sentAt);

        Assert.False(state.Apply(tick));
        Assert.Equal(elapsed, state.Heartbeat.Latency);
        Assert.Equal(0u, state.EquipmentRevision);
    }

    [Fact]
    public void UnhandledPacketDiagnosticsAreBoundedAndDoNotReadPayload()
    {
        RecordingClientLogger logger = new();
        WorldInboundState state = CreateState(diagnostics: new ClientDiagnostics(logger));
        byte[] packet = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)packet.Length);
        "PRIVATE-DATA"u8.CopyTo(packet.AsSpan(4));

        for (ushort type = 20_000; type < 20_100; type++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), type);
            Assert.False(state.Apply(packet));
            Assert.False(state.Apply(packet));
        }

        Assert.Equal(65, logger.Events.Count);
        Assert.All(logger.Events[..64], logged =>
        {
            Assert.Equal(2306, logged.Id);
            Assert.Null(logged.Exception);
            Assert.DoesNotContain("PRIVATE-DATA", logged.Message, StringComparison.Ordinal);
        });
        Assert.Equal((ushort)20_000, Assert.Single(logger.Events[0].Fields, field => field.Key == "PacketType").Value);
        Assert.Equal(2308, logger.Events[^1].Id);
    }

    [Fact]
    public void UnknownAttributeIdsShareOneSafeDiagnosticCode()
    {
        RecordingClientLogger logger = new();
        WorldInboundState state = CreateState(diagnostics: new ClientDiagnostics(logger));

        Assert.False(state.Apply(Attributes(HeroId, (0xDEADBEEF, ulong.MaxValue, 0), (0xDABCFEED, 0, ulong.MaxValue))));

        RecordingClientLogger.RecordedEvent logged = Assert.Single(logger.Events);
        Assert.Equal(2307, logged.Id);
        Assert.Equal(uint.MaxValue, Assert.Single(logged.Fields, field => field.Key == "AttributeCode").Value);
    }

    private static WorldInboundState CreateState(uint currentLife = 70, byte[][]? hydration = null,
        ushort transformLookSourceId = 0, int hydrationEnteredAt = 0, ClientDiagnostics? diagnostics = null) =>
        new(new GameWorldEntrySnapshot(
            new GameCharacterInfo1006
            {
                EntityId = HeroId,
                Strength = 5,
                Agility = 6,
                Vitality = 7,
                Spirit = 8,
                CurrentLife = (ushort)currentLife,
                CurrentMana = 20,
                Level = 42,
                CurrentProfession = 10,
                Experience = 12345,
                TransformLookSourceId = transformLookSourceId,
            },
            1002, 1010, 0, 1, 61, 101, hydration ?? [], 3, hydrationEnteredAt), WorldHeroItemStatsTests.Catalog(), diagnostics: diagnostics);

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

    private static byte[] Weather(uint kind, uint intensity, uint direction, uint parameter)
    {
        byte[] packet = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 20);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1016);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), kind);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), intensity);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), direction);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16), parameter);
        return packet;
    }
}
