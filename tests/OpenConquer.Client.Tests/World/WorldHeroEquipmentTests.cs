using System.Buffers.Binary;
using OpenConquer.Client.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldHeroEquipmentTests
{
    [Fact]
    public void TrackedItemsRequireTheActivationSnapshotForOrdinarySlots()
    {
        WorldHeroEquipment tracked = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Item(200, 130003, placement: 3)]);

        Assert.Null(tracked.GetActiveItem(1));
        Assert.Null(tracked.GetActiveItem(3));

        WorldHeroEquipment activated = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Item(200, 130003, placement: 3),
                Equipment(headwear: 100, armor: 200)]);

        Assert.Equal(1111000u, activated.GetActiveItem(1)?.TypeId);
        Assert.Equal(130003u, activated.GetActiveItem(3)?.TypeId);
    }

    [Fact]
    public void EquipmentSnapshotResolvesTrackedUidWithoutRequiringAMatchingPlacement()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 3), Equipment(headwear: 100)]);

        Assert.Equal(100u, equipment.GetActiveItem(1)?.Id);
    }

    [Fact]
    public void SnapshotMapsHandAndAccessoryUidsToTheirRecordedSlots()
    {
        byte[] snapshot = Equipment();
        BinaryPrimitives.WriteUInt32LittleEndian(snapshot.AsSpan(44), 101);
        BinaryPrimitives.WriteUInt32LittleEndian(snapshot.AsSpan(48), 102);
        BinaryPrimitives.WriteUInt32LittleEndian(snapshot.AsSpan(68), 103);
        BinaryPrimitives.WriteUInt32LittleEndian(snapshot.AsSpan(72), 104);

        WorldHeroEquipment state = WorldEquipmentFrames.Hydrate(
            [Item(101, 410003, placement: 4), Item(102, 420003, placement: 5),
                Item(103, 215003, placement: 15), Item(104, 216003, placement: 16), snapshot]);

        Assert.Equal(101u, state.GetActiveItem(4)?.Id);
        Assert.Equal(102u, state.GetActiveItem(5)?.Id);
        Assert.Equal(103u, state.GetActiveItem(15)?.Id);
        Assert.Equal(104u, state.GetActiveItem(16)?.Id);
    }

    [Fact]
    public void EquipmentSnapshotMapsAllThirteenWireOffsetsToTheirSlotNumbers()
    {
        byte[] snapshot = Equipment();
        byte[] slots = [1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 18];
        List<byte[]> hydration = [];

        for (int index = 0; index < slots.Length; index++)
        {
            uint id = (uint)(1000 + index);
            hydration.Add(Item(id, (uint)(410000 + index * 10), placement: slots[index]));
            BinaryPrimitives.WriteUInt32LittleEndian(snapshot.AsSpan(32 + index * 4), id);
        }

        hydration.Add(snapshot);
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(hydration);

        for (int index = 0; index < slots.Length; index++)
        {
            Assert.Equal((uint)(1000 + index), equipment.GetActiveItem(slots[index])?.Id);
        }
    }

    [Fact]
    public void MissingAndZeroUidsClearPreviouslyActivatedSlots()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Equipment(headwear: 100),
                Equipment(headwear: 999)]);

        Assert.Null(equipment.GetActiveItem(1));

        equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Equipment(headwear: 100), Equipment()]);

        Assert.Null(equipment.GetActiveItem(1));
    }

    [Fact]
    public void SpecialEquipmentSlotsActivateWhenTheirTrackedItemArrives()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 201003, placement: 10), Item(200, 202003, placement: 11),
                Item(300, 130003, placement: 13)]);

        Assert.Equal(100u, equipment.GetActiveItem(10)?.Id);
        Assert.Equal(200u, equipment.GetActiveItem(11)?.Id);
        Assert.Null(equipment.GetActiveItem(13));
    }

    [Fact]
    public void AlternatePlacementAndInventoryDoNotActivateWithoutTheUidSnapshot()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 21), Item(200, 130003, placement: 0),
                Equipment(headwear: 100, armor: 200)]);

        Assert.Equal(100u, equipment.GetActiveItem(1)?.Id);
        Assert.Null(equipment.GetActiveItem(3));
    }

    [Fact]
    public void HydrationRetainsVerifiedItemStatInputs()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Equipment(headwear: 100)]);

        Assert.Equal(new WorldHeroItem(100, 1111000, 40, 2, 7), equipment.GetActiveItem(1));
    }

    [Fact]
    public void UnsupportedItemAndEquipmentSubtypesDoNotActivateSlots()
    {
        byte[] item = Item(100, 1111000, placement: 1);
        item[16] = 4;
        byte[] equipment = Equipment(headwear: 100);
        BinaryPrimitives.WriteUInt16LittleEndian(equipment.AsSpan(12), 17);

        WorldHeroEquipment state = WorldEquipmentFrames.Hydrate([item, equipment]);

        Assert.Null(state.GetActiveItem(1));
    }

    [Fact]
    public void LiveItemUpdateRefreshesActiveEquipmentStatisticsWithoutAnotherSnapshot()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 120020, placement: 1), Equipment(headwear: 100)]);

        byte[] updated = Item(100, 120020, placement: 1);
        updated[16] = 3;
        BinaryPrimitives.WriteUInt16LittleEndian(updated.AsSpan(12), 0);
        updated[33] = 9;
        updated[36] = 20;

        Assert.True(equipment.Apply(updated));
        Assert.Equal(new WorldHeroItem(100, 120020, 0, 9, 20), equipment.GetActiveItem(1));
    }

    [Fact]
    public void LiveItemRemovalClearsTrackedAndActiveReferences()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Equipment(headwear: 100)]);

        Assert.True(equipment.Apply(Tick(3, itemId: 100)));
        Assert.Null(equipment.GetActiveItem(1));
        Assert.False(equipment.Apply(Tick(3, itemId: 100)));
        Assert.Null(WorldEquipmentFrames.Hydrate([Equipment(headwear: 100)]).GetActiveItem(1));
    }

    [Fact]
    public void LiveActiveSnapshotReplacesThePreviouslyEquippedItem()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Item(200, 1111010, placement: 21),
                Equipment(headwear: 100)]);

        Assert.True(equipment.Apply(Equipment(headwear: 200)));
        Assert.Equal(200u, equipment.GetActiveItem(1)?.Id);

        Assert.True(equipment.Apply(Equipment()));
        Assert.Null(equipment.GetActiveItem(1));
    }

    [Fact]
    public void LiveUpdateForUnknownItemCannotCreateTrackedEquipment()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate([]);
        byte[] update = Item(100, 120020, placement: 1);
        update[16] = 3;

        Assert.False(equipment.Apply(update));
        Assert.Null(equipment.GetActiveItem(1));
        Assert.False(equipment.Apply(Tick(3, itemId: 100)));
    }

    [Fact]
    public void LiveItemPlacementChangeClearsItsPreviousActiveSlot()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Equipment(headwear: 100)]);
        byte[] update = Item(100, 1111000, placement: 3);
        update[16] = 3;

        Assert.True(equipment.Apply(update));
        Assert.Null(equipment.GetActiveItem(1));
        Assert.True(equipment.Apply(Equipment(armor: 100)));
        Assert.Equal(100u, equipment.GetActiveItem(3)?.Id);
    }

    [Fact]
    public void LiveEquipMovesInventoryItemIntoTheRequestedSlotAndDisplacesThePreviousItem()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Item(200, 1111010, placement: 0),
                Equipment(headwear: 100)]);

        byte[] equip = Tick(5, itemId: 200);
        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(84), 100);

        Assert.True(equipment.Apply(equip));
        Assert.Equal(200u, equipment.GetActiveItem(1)?.Id);

        byte[] unequip = Tick(6);
        BinaryPrimitives.WriteUInt32LittleEndian(unequip.AsSpan(8), 1);
        Assert.True(equipment.Apply(unequip));
        Assert.Null(equipment.GetActiveItem(1));

        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(4), 100);
        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(20), 0);
        Assert.True(equipment.Apply(equip));
        Assert.Equal(100u, equipment.GetActiveItem(1)?.Id);
    }

    [Fact]
    public void LiveInventoryItemUpdateIsRetainedUntilEquipped()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 0)]);

        byte[] update = Item(100, 1111000, placement: 0);
        update[16] = 3;
        BinaryPrimitives.WriteUInt16LittleEndian(update.AsSpan(12), 3);
        Assert.True(equipment.Apply(update));

        byte[] equip = Tick(5, itemId: 100);
        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(8), 1);
        Assert.True(equipment.Apply(equip));
        Assert.Equal((ushort)3, equipment.GetActiveItem(1)?.Durability);
    }

    [Fact]
    public void DurabilityUpdateChangesActiveItemWithoutReplacingItsIdentity()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Equipment(headwear: 100)]);
        byte[] durability = Tick(17, itemId: 0);
        BinaryPrimitives.WriteUInt32LittleEndian(durability.AsSpan(8), 1);

        Assert.True(equipment.Apply(durability));
        Assert.Equal(new WorldHeroItem(100, 1111000, 0, 2, 7), equipment.GetActiveItem(1));

        Assert.False(equipment.Apply(durability));
        Assert.Equal(100u, equipment.GetActiveItem(1)?.Id);
    }

    [Fact]
    public void UnequippingTwoHandedWeaponAlsoClearsThePairedHand()
    {
        byte[] snapshot = Equipment();
        BinaryPrimitives.WriteUInt32LittleEndian(snapshot.AsSpan(44), 100);
        BinaryPrimitives.WriteUInt32LittleEndian(snapshot.AsSpan(48), 200);
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 500003, placement: 4), Item(200, 410003, placement: 5), snapshot]);

        byte[] unequip = Tick(6);
        BinaryPrimitives.WriteUInt32LittleEndian(unequip.AsSpan(8), 4);

        Assert.True(equipment.Apply(unequip));
        Assert.Null(equipment.GetActiveItem(4));
        Assert.Null(equipment.GetActiveItem(5));
    }

    [Fact]
    public void InventoryCreateRejectsMoreThanFortyDistinctItemsWithoutCorruptingExistingItems()
    {
        List<byte[]> packets = [];
        for (uint id = 1; id <= 40; id++)
        {
            packets.Add(Item(id, 120020, placement: 0));
        }

        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(packets);
        Assert.Throws<InvalidDataException>(() => equipment.Apply(Item(41, 120020, placement: 0)));

        byte[] equip = Tick(5, itemId: 40);
        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(8), 1);
        Assert.True(equipment.Apply(equip));
        Assert.Equal(40u, equipment.GetActiveItem(1)?.Id);
    }

    [Fact]
    public void MalformedDisplacementCountCannotApplyPartialEquipmentChanges()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Item(200, 1111010, placement: 0),
                Equipment(headwear: 100)]);
        byte[] equip = Tick(5, itemId: 200);
        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(equip.AsSpan(20), 2);

        Assert.Throws<InvalidDataException>(() => equipment.Apply(equip));
        Assert.Equal(100u, equipment.GetActiveItem(1)?.Id);
    }

    [Fact]
    public void MalformedLiveMutationsLeaveExistingEquipmentUnchanged()
    {
        WorldHeroEquipment equipment = WorldEquipmentFrames.Hydrate(
            [Item(100, 1111000, placement: 1), Equipment(headwear: 100)]);
        byte[] malformed = Tick(46);
        Array.Resize(ref malformed, 84);
        BinaryPrimitives.WriteUInt16LittleEndian(malformed, 84);

        Assert.Throws<InvalidDataException>(() => equipment.Apply(malformed));
        Assert.Equal(100u, equipment.GetActiveItem(1)?.Id);
    }

    [Fact]
    public void MalformedItemAndEquipmentEnvelopesAreRejected()
    {
        byte[] item = Item(100, 1111000, placement: 1);
        Array.Resize(ref item, 67);
        BinaryPrimitives.WriteUInt16LittleEndian(item, 67);
        Assert.Throws<InvalidDataException>(() => WorldEquipmentFrames.Hydrate([item]));

        byte[] equipment = Equipment(headwear: 100);
        Array.Resize(ref equipment, 84);
        BinaryPrimitives.WriteUInt16LittleEndian(equipment, 84);
        Assert.Throws<InvalidDataException>(() => WorldEquipmentFrames.Hydrate([equipment]));

        Assert.Throws<InvalidDataException>(() => WorldEquipmentFrames.Hydrate([new byte[3]]));
    }

    private static byte[] Item(uint id, uint typeId, byte placement)
    {
        byte[] packet = new byte[68];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 68);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1008);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), id);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), typeId);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), 40);
        packet[16] = 1;
        packet[18] = placement;
        packet[33] = 2;
        packet[36] = 7;
        return packet;
    }

    private static byte[] Tick(ushort subtype, uint itemId = 0)
    {
        byte[] packet = new byte[88];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 88);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1009);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), itemId);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), subtype);
        return packet;
    }

    private static byte[] Equipment(uint headwear = 0, uint armor = 0)
    {
        byte[] packet = new byte[88];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 88);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1009);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), 46);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(32), headwear);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(40), armor);
        return packet;
    }
}
