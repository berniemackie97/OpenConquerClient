using System.Buffers.Binary;
using OpenConquer.Client.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldHeroEquipmentTests
{
    [Fact]
    public void TrackedItemsRequireTheActivationSnapshotForOrdinarySlots()
    {
        WorldHeroEquipment tracked = WorldHeroEquipment.FromHydration(
            [Item(100, 1111000, placement: 1), Item(200, 130003, placement: 3)]);

        Assert.Null(tracked.GetActiveItem(1));
        Assert.Null(tracked.GetActiveItem(3));

        WorldHeroEquipment activated = WorldHeroEquipment.FromHydration(
            [Item(100, 1111000, placement: 1), Item(200, 130003, placement: 3),
                Equipment(headwear: 100, armor: 200)]);

        Assert.Equal(1111000u, activated.GetActiveItem(1)?.TypeId);
        Assert.Equal(130003u, activated.GetActiveItem(3)?.TypeId);
    }

    [Fact]
    public void EquipmentSnapshotResolvesTrackedUidWithoutRequiringAMatchingPlacement()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
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

        WorldHeroEquipment state = WorldHeroEquipment.FromHydration(
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
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(hydration);

        for (int index = 0; index < slots.Length; index++)
        {
            Assert.Equal((uint)(1000 + index), equipment.GetActiveItem(slots[index])?.Id);
        }
    }

    [Fact]
    public void MissingAndZeroUidsClearPreviouslyActivatedSlots()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [Item(100, 1111000, placement: 1), Equipment(headwear: 100),
                Equipment(headwear: 999)]);

        Assert.Null(equipment.GetActiveItem(1));

        equipment = WorldHeroEquipment.FromHydration(
            [Item(100, 1111000, placement: 1), Equipment(headwear: 100), Equipment()]);

        Assert.Null(equipment.GetActiveItem(1));
    }

    [Fact]
    public void SpecialEquipmentSlotsActivateWhenTheirTrackedItemArrives()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [Item(100, 201003, placement: 10), Item(200, 202003, placement: 11),
                Item(300, 130003, placement: 13)]);

        Assert.Equal(100u, equipment.GetActiveItem(10)?.Id);
        Assert.Equal(200u, equipment.GetActiveItem(11)?.Id);
        Assert.Null(equipment.GetActiveItem(13));
    }

    [Fact]
    public void AlternatePlacementAndInventoryDoNotActivateWithoutTheUidSnapshot()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [Item(100, 1111000, placement: 21), Item(200, 130003, placement: 0),
                Equipment(headwear: 100, armor: 200)]);

        Assert.Equal(100u, equipment.GetActiveItem(1)?.Id);
        Assert.Null(equipment.GetActiveItem(3));
    }

    [Fact]
    public void HydrationRetainsVerifiedItemStatInputs()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
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

        WorldHeroEquipment state = WorldHeroEquipment.FromHydration([item, equipment]);

        Assert.Null(state.GetActiveItem(1));
    }

    [Fact]
    public void MalformedItemAndEquipmentEnvelopesAreRejected()
    {
        byte[] item = Item(100, 1111000, placement: 1);
        Array.Resize(ref item, 67);
        BinaryPrimitives.WriteUInt16LittleEndian(item, 67);
        Assert.Throws<InvalidDataException>(() => WorldHeroEquipment.FromHydration([item]));

        byte[] equipment = Equipment(headwear: 100);
        Array.Resize(ref equipment, 84);
        BinaryPrimitives.WriteUInt16LittleEndian(equipment, 84);
        Assert.Throws<InvalidDataException>(() => WorldHeroEquipment.FromHydration([equipment]));

        Assert.Throws<InvalidDataException>(() => WorldHeroEquipment.FromHydration([new byte[3]]));
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
