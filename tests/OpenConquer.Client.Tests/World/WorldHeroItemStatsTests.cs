using System.Buffers.Binary;
using System.Text;
using OpenConquer.Client.World;
using OpenConquer.Content.Items;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldHeroItemStatsTests
{
    [Fact]
    public void AddsVerifiedBaseItemAdditionAndEnchantmentWithSignedMana()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [Item(100, 120020, 40), Equipment(100)]);
        (int life, int mana) = WorldHeroItemStats.Resolve(equipment, Catalog());

        Assert.Equal(296, life);
        Assert.Equal(-10, mana);
    }

    [Fact]
    public void BrokenItemSkipsLifeButStillContributesMana()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [Item(100, 120020, 0), Equipment(100)]);
        Assert.Equal((0, -10), WorldHeroItemStats.Resolve(equipment, Catalog()));
    }

    [Fact]
    public void NormalizesWeaponItemAdditionLookupByFamily()
    {
        Assert.Equal(444000u, WorldHeroItemStats.NormalizeAdditionType(410003));
        Assert.Equal(500000u, WorldHeroItemStats.NormalizeAdditionType(500003));
        Assert.Equal(120020u, WorldHeroItemStats.NormalizeAdditionType(120020));
    }

    [Fact]
    public void DoesNotUseUnknownEquippedItemStatsAsZero()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [Item(100, 130003, 40), Equipment(100)]);
        Assert.Throws<InvalidDataException>(() => WorldHeroItemStats.Resolve(equipment, Catalog()));
    }

    private static ItemStatCatalog Catalog()
    {
        string[] row = Enumerable.Repeat("0", 60).ToArray();
        row[0] = "120020";
        row[1] = "TestArmor";
        row[4] = "15";
        row[19] = "50";
        row[20] = "-10";
        byte[] text = Encoding.ASCII.GetBytes(string.Join("@@", row) + "\r\n");
        uint seed = 0x2537;
        byte[] keys = new byte[128];
        for (int i = 0; i < 128; i++)
        {
            seed = unchecked(seed * 214013u + 2531011u);
            keys[i] = (byte)(seed >> 16);
        }

        for (int i = 0; i < text.Length; i++)
        {
            int rotate = i & 7;
            byte value = text[i];
            text[i] = (byte)((rotate == 0 ? value : (value << rotate | value >> (8 - rotate))) ^ keys[i % 128]);
        }

        return ItemStatCatalog.Parse(text,
            Encoding.ASCII.GetBytes("120020 9 239 0 0 23 0 0 0 0\r\n"));
    }

    private static byte[] Item(uint id, uint type, ushort durability)
    {
        byte[] frame = new byte[68];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, 68);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 1008);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), id);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8), type);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(12), durability);
        frame[16] = 1;
        frame[18] = 1;
        frame[33] = 9;
        frame[36] = 7;
        return frame;
    }

    private static byte[] Equipment(uint headwear)
    {
        byte[] frame = new byte[88];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, 88);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 1009);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(12), 46);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(32), headwear);
        return frame;
    }
}
