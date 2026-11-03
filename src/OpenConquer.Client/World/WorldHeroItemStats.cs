using OpenConquer.Content.Items;

namespace OpenConquer.Client.World;

internal static class WorldHeroItemStats
{
    internal static (int Life, int Mana) Resolve(WorldHeroEquipment equipment, ItemStatCatalog catalog)
    {
        int life = 0;
        int mana = 0;
        unchecked
        {
            for (int slot = 1; slot <= 18; slot++)
            {
                if (equipment.GetActiveItem(slot) is not { } item)
                {
                    continue;
                }

                if (!catalog.TryGetItem(item.TypeId, out ItemBaseStats stats))
                {
                    throw new InvalidDataException($"Equipped item {item.TypeId} is not in the item catalog.");
                }

                mana += stats.Mana;
                if (item.Durability == 0 && IsEquipmentCategory(item.TypeId))
                {
                    continue;
                }

                life += stats.Life;
                life += catalog.GetAdditionLife(NormalizeAdditionType(item.TypeId), item.AdditionLevel);
                if (slot != 12)
                {
                    life += item.EnchantmentLifeBonus;
                }
            }
        }

        return (life, mana);
    }

    internal static uint NormalizeAdditionType(uint type)
    {
        int category = Category(type);
        uint subtype = Subtype(type);
        uint normalized = type;

        if (!((category == 4 && subtype == 21000) || (category == 5 && subtype == 0)))
        {
            uint family = type % 1_000_000 / 1000;
            if (category == 4 && family is not 601 and not 610)
            {
                normalized = type / 100_000 * 100_000 + type % 1000 + 44_000;
            }
            else if (category == 5)
            {
                normalized = type / 100_000 * 100_000 + type % 1000 + 55_000;
            }
        }

        return normalized / 10 * 10;
    }

    private static bool IsEquipmentCategory(uint type) =>
        Category(type) is >= 1 and <= 8 or 12 or 14 or 15 or 16 or 20 or 21;

    private static uint Subtype(uint type)
    {
        uint family = type % 1_000_000 / 1000;
        if (family is 201 or 202)
        {
            return type % 10_000 / 1000;
        }

        uint bucket = type % 10_000_000 / 100_000;
        return bucket switch
        {
            4 or 5 => type % 100_000 / 1000 * 1000,
            7 or 10 => type % 100_000 / 10_000 * 10_000,
            _ => uint.MaxValue,
        };
    }

    private static int Category(uint type)
    {
        uint normalized = type % 1_000_000;
        uint family = normalized / 1000;
        if (family == 300 && type % 10 == 0)
        {
            return 14;
        }

        int familyCategory = family switch
        {
            123 => 1,
            203 => 21,
            601 => 4,
            201 or 202 => 12,
            200 => 20,
            350 => 16,
            360 => 15,
            370 => 17,
            380 => 18,
            _ => -1,
        };
        if (familyCategory != -1)
        {
            return familyCategory;
        }

        uint bucket = type % 10_000_000 / 100_000;
        if (bucket <= 7)
        {
            if (bucket == 1)
            {
                return (int)(normalized / 10_000) switch
                {
                    11 or 14 or 17 => 1,
                    12 => 2,
                    13 => 3,
                    15 => 7,
                    16 => 8,
                    18 or 19 => 11,
                    _ => -1,
                };
            }

            return bucket switch
            {
                4 or 6 => 4,
                5 => 5,
                7 => 9,
                _ => -1,
            };
        }

        return bucket switch
        {
            9 => 6,
            10 => 0,
            >= 20 and <= 29 => 10,
            _ => -1,
        };
    }
}
