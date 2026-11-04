using System.Buffers.Binary;
using OpenConquer.Client.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldRoleSourcesTests
{
    [Theory]
    [InlineData(2u, 335, 2_000_000u, 2_119_335u)]
    [InlineData(1003u, 735, 3_000_000u, 3_119_735u)]
    [InlineData(2004u, 400, 4_000_000u, 0u)]
    public void ResolvesInitialBodyAndLinkedHairFromCharacterInfo(
        uint appearance, int hair, uint expectedBody, uint expectedArmet)
    {
        WorldRoleSources sources = WorldRoleSources.Resolve(appearance, checked((ushort)hair),
            WorldHeroEquipment.FromHydration([]));
        Assert.Equal(expectedBody, sources.BodyModelId);
        Assert.Equal(expectedArmet, sources.ArmetModelId);
    }

    [Fact]
    public void ActivatesHeadwearAndArmorOnlyAfterTheEquipmentUidSnapshot()
    {
        byte[] helmet = ItemSnapshot(100, 1111000, placement: 1);
        byte[] armor = ItemSnapshot(200, 130003, placement: 3);
        WorldRoleSources withoutActivation = WorldRoleSources.Resolve(1002, 335,
            WorldHeroEquipment.FromHydration([helmet, armor]));
        Assert.Equal(2_000_000u, withoutActivation.BodyModelId);
        Assert.Equal(2_119_335u, withoutActivation.ArmetModelId);

        WorldRoleSources active = WorldRoleSources.Resolve(1002, 335,
            WorldHeroEquipment.FromHydration([helmet, armor, ActiveEquipment(headwear: 100, armor: 200)]));
        Assert.Equal(2_130_000u, active.BodyModelId);
        Assert.Equal(2_111_000u, active.ArmetModelId);
    }

    [Fact]
    public void MissingEquipmentUidClearsTheSlotWithoutPreventingWorldEntry()
    {
        WorldRoleSources sources = WorldRoleSources.Resolve(1002, 335,
            WorldHeroEquipment.FromHydration([ActiveEquipment(headwear: 999, armor: 200)]));

        Assert.Equal(2_000_000u, sources.BodyModelId);
        Assert.Equal(2_119_335u, sources.ArmetModelId);
    }

    [Fact]
    public void LiveEquipmentMutationsRefreshTheCharacterPartProjection()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [ItemSnapshot(100, 1111000, placement: 1), ItemSnapshot(200, 130003, placement: 3),
                ActiveEquipment(headwear: 100, armor: 200)]);

        Assert.Equal(new WorldRoleSources(2_130_000u, 2_111_000u, 0, 0),
            WorldRoleSources.Resolve(1002, 335, equipment));

        byte[] upgraded = ItemSnapshot(100, 1111010, placement: 1);
        upgraded[16] = 3;
        Assert.True(equipment.Apply(upgraded));
        Assert.Equal(new WorldRoleSources(2_130_000u, 2_111_010u, 0, 0),
            WorldRoleSources.Resolve(1002, 335, equipment));

        Assert.True(equipment.Apply(ActiveEquipment(armor: 200)));
        Assert.Equal(new WorldRoleSources(2_130_000u, 2_119_335u, 0, 0),
            WorldRoleSources.Resolve(1002, 335, equipment));
    }

    [Fact]
    public void EquippedHandSourcesProjectToTheMatchingAttachmentSlots()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [ItemSnapshot(101, 410003, placement: 4),
                ItemSnapshot(102, 420033, placement: 5),
                ActiveEquipment(rightHand: 101, leftHand: 102)]);
        WorldRoleSources sources = WorldRoleSources.Resolve(1002, 335, equipment);

        Assert.Equal((410000u, 420030u),
            (sources.RightWeaponModelId, sources.LeftWeaponModelId));

        Assert.True(equipment.Apply(ActiveEquipment(rightHand: 102)));
        sources = WorldRoleSources.Resolve(1002, 335, equipment);
        Assert.Equal((420030u, 0u),
            (sources.RightWeaponModelId, sources.LeftWeaponModelId));

        Assert.True(equipment.Apply(ActiveEquipment()));
        sources = WorldRoleSources.Resolve(1002, 335, equipment);
        Assert.Equal((0u, 0u),
            (sources.RightWeaponModelId, sources.LeftWeaponModelId));
    }

    [Theory]
    [InlineData(500003u, 500000u)]
    [InlineData(370013u, 370010u)]
    public void RightHandWeaponFamiliesUseTheLeftAttachment(uint itemType, uint expectedSource)
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [ItemSnapshot(101, itemType, placement: 4), ActiveEquipment(rightHand: 101)]);

        WorldRoleSources sources = WorldRoleSources.Resolve(1002, 335, equipment);

        Assert.Equal(0u, sources.RightWeaponModelId);
        Assert.Equal(expectedSource, sources.LeftWeaponModelId);
    }

    [Fact]
    public void LeftHandCompositeHundredFiveUsesTheRightAttachment()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [ItemSnapshot(101, 1_050_003, placement: 5), ActiveEquipment(leftHand: 101)]);

        WorldRoleSources sources = WorldRoleSources.Resolve(1002, 335, equipment);

        Assert.Equal(50_000u, sources.RightWeaponModelId);
        Assert.Equal(0u, sources.LeftWeaponModelId);
    }

    [Fact]
    public void RightHandCompositeHundredFiveReplacesLeftFiveHundredFamily()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [ItemSnapshot(101, 1_050_003, placement: 4),
                ItemSnapshot(102, 500003, placement: 5),
                ActiveEquipment(rightHand: 101, leftHand: 102)]);

        WorldRoleSources sources = WorldRoleSources.Resolve(1002, 335, equipment);

        Assert.Equal(50_000u, sources.RightWeaponModelId);
        Assert.Equal(0u, sources.LeftWeaponModelId);
    }

    [Fact]
    public void InventoryItemsAndUnactivatedEquipmentDoNotChangeAppearance()
    {
        byte[] inventory = ItemSnapshot(100, 130003, placement: 0);
        byte[] unrelated = ActiveEquipment(armor: 100);
        BinaryPrimitives.WriteUInt16LittleEndian(unrelated.AsSpan(12), 12);

        WorldRoleSources sources = WorldRoleSources.Resolve(1002, 335,
            WorldHeroEquipment.FromHydration([inventory, unrelated]));

        Assert.Equal(2_000_000u, sources.BodyModelId);
        Assert.Equal(2_119_335u, sources.ArmetModelId);
    }

    private static byte[] ItemSnapshot(uint itemId, uint typeId, byte placement)
    {
        byte[] packet = new byte[68];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 68);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1008);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), itemId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), typeId);
        packet[16] = 1;
        packet[18] = placement;
        return packet;
    }

    private static byte[] ActiveEquipment(uint headwear = 0, uint armor = 0,
        uint rightHand = 0, uint leftHand = 0)
    {
        byte[] packet = new byte[88];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 88);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1009);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), 46);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(32), headwear);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(40), armor);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(44), rightHand);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(48), leftHand);
        return packet;
    }
}
