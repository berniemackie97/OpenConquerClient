using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

/// <summary>
/// One local item instance. Native Add allocates a fresh CItem (0x762D84/0x762D9C), while Update mutates the located
/// instance (0x763570..0x7638A4). Active equipment refers to that instance, not merely its UID (0x6DB0C0).
/// </summary>
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
