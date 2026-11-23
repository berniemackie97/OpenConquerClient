using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal sealed class WorldHeroItem(GameItemRecord1008 record)
{
    internal uint Id { get; } = record.ItemId;
    internal uint TypeId { get; private set; } = record.ItemTypeId;
    internal ushort Durability { get; private set; } = record.Durability;
    internal byte AdditionLevel { get; private set; } = record.AdditionLevel;
    internal byte EnchantmentLifeBonus { get; private set; } = record.EnchantmentLifeBonus;

    internal void UpdateStats(GameItemRecord1008 update)
    {
        TypeId = update.ItemTypeId;
        Durability = update.Durability;
        AdditionLevel = update.AdditionLevel;
        EnchantmentLifeBonus = update.EnchantmentLifeBonus;
    }

    internal bool SetDurability(ushort value)
    {
        bool changed = Durability != value;
        Durability = value;
        return changed;
    }
}
