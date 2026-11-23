using OpenConquer.Content.Items;
using OpenConquer.Content.Progression;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal sealed class WorldHeroModel
{
    private const int NormalStaminaMaximum = 100;
    private const int ExtendedStaminaMaximum = 150;
    private const ulong ExtendedStaminaStatusMask = 1UL << 33;

    private readonly WorldHeroGaugeFollowers _followers = new();
    private readonly ItemStatCatalog _itemStats;
    private readonly ulong _mapFlags;
    private readonly bool _hasTransformLookSource;

    internal WorldHeroModel(WorldHeroAttributes attributes, ItemStatCatalog itemStats, ulong mapFlags, bool hasTransformLookSource)
    {
        ArgumentNullException.ThrowIfNull(itemStats);
        Attributes = attributes;
        _itemStats = itemStats;
        _mapFlags = mapFlags;
        _hasTransformLookSource = hasTransformLookSource;
    }

    internal WorldHeroAttributes Attributes
    {
        get;
        private set;
    }

    internal WorldHeroEquipment Equipment { get; } = new();

    internal uint EquipmentRevision
    {
        get;
        private set;
    }

    internal bool TryApplyAttribute(uint attributeId, ulong low, ulong high)
    {
        if (!Attributes.TryApply(attributeId, low, high, out WorldHeroAttributes next))
        {
            return false;
        }

        Attributes = next;
        return true;
    }

    internal void ApplyItemRecord(GameItemRecord1008 record)
    {
        if (_itemStats.TryGetItem(record.ItemTypeId, out _) && Equipment.Apply(record))
        {
            EquipmentRevision++;
        }
    }

    internal void ApplyItem(GameItem1009 item)
    {
        if (!Equipment.Apply(item))
        {
            return;
        }

        EquipmentRevision++;
        if (item.Subtype == GameItem1009.EquipmentSnapshot)
        {
            ClampLifeAfterEquipmentSnapshot();
        }
    }

    internal WorldHeroVitals AdvanceVitals(LevelExperienceTable experience)
    {
        ArgumentNullException.ThrowIfNull(experience);
        WorldHeroMaxStats maxima = ResolveMaxima();
        _followers.Advance(Attributes, maxima);
        bool extended = (Attributes.StatusLow & ExtendedStaminaStatusMask) != 0;
        int maximumStamina = extended ? ExtendedStaminaMaximum : NormalStaminaMaximum;
        int stamina = Math.Min(unchecked((int)Attributes.CurrentStamina), maximumStamina);
        return new WorldHeroVitals(unchecked((int)Attributes.CurrentLife), _followers.Life, maxima.Life, unchecked((int)Attributes.CurrentMana),
            _followers.Mana, maxima.Mana, stamina, maximumStamina, extended, checked((int)Attributes.SkillGaugePercent),
            Attributes.Experience, experience.GetNextLevelExperience(Attributes.Level));
    }

    private WorldHeroMaxStats ResolveMaxima()
    {
        (int itemLife, int itemMana) = WorldHeroItemStats.Resolve(Equipment, _itemStats);
        return WorldHeroMaxStats.Resolve(Attributes, itemLife, itemMana, _mapFlags, hasTransformLook: false, lifeTierDigit: 0);
    }

    private void ClampLifeAfterEquipmentSnapshot()
    {
        if (_hasTransformLookSource)
        {
            return;
        }

        int maximumLife = ResolveMaxima().Life;
        if (unchecked((int)Attributes.CurrentLife) <= maximumLife)
        {
            return;
        }

        Attributes = Attributes with
        {
            CurrentLife = unchecked((uint)maximumLife)
        };
        _followers.ResetLifeForEquipmentClamp(maximumLife);
    }
}

internal readonly record struct WorldHeroVitals(int CurrentLife, int LifeFollower, int MaximumLife, int CurrentMana, int ManaFollower,
    int MaximumMana, int Stamina, int MaximumStamina, bool HasExtendedStamina, int SkillGaugePercent, ulong Experience, ulong NextLevelExperience);
