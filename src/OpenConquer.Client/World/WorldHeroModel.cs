using OpenConquer.Content.Items;
using OpenConquer.Content.Progression;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

/// <summary>
/// Local hero rules/state, independent of HUD types. Native anchors and supported-input limits:
/// 20_notes/Rendering/hero-model-cp22.md and 14_hero_effective_max_life_mana.md.
/// </summary>
internal sealed class WorldHeroModel
{
    // CHero initialization 0x6570A9; status-33 getter 0x66F766 and uint32 LEA/SHR 0x66F792.
    // The current projection supports the initialized base, not a dynamic maximum-force input.
    private const int NormalStaminaMaximum = 100;
    private const int ExtendedStaminaMaximum = 150;
    private const ulong ExtendedStaminaStatusMask = 1UL << 33;

    private readonly WorldHeroGaugeFollowers _followers = new();
    private readonly ItemStatCatalog _itemStats;
    private readonly ulong _mapFlags;
    private readonly bool _hasTransformLookSource;

    /// <summary>
    /// The item table and map flags are fixed inputs of the effective-maximum rules for one world, so they are bound
    /// before any equipment packet. A transform-look source is not a supported maximum-life input yet.
    /// </summary>
    internal WorldHeroModel(WorldHeroAttributes attributes, ItemStatCatalog itemStats, ulong mapFlags,
        bool hasTransformLookSource)
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
        if (Equipment.Apply(record))
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

    /// <summary>Called once at the existing guarded world-draw point, after scene drawing.</summary>
    internal WorldHeroVitals AdvanceVitals(LevelExperienceTable experience)
    {
        ArgumentNullException.ThrowIfNull(experience);
        WorldHeroMaxStats maxima = ResolveMaxima();
        _followers.Advance(Attributes, maxima);
        bool extended = (Attributes.StatusLow & ExtendedStaminaStatusMask) != 0;
        int maximumStamina = extended ? ExtendedStaminaMaximum : NormalStaminaMaximum;
        // CHero_GetCappedPhysicalForce 0x66F77C: signed upper-only min; do not add a zero floor.
        int stamina = Math.Min(unchecked((int)Attributes.CurrentStamina), maximumStamina);
        return new WorldHeroVitals(unchecked((int)Attributes.CurrentLife), _followers.Life, maxima.Life,
            unchecked((int)Attributes.CurrentMana), _followers.Mana, maxima.Mana,
            stamina, maximumStamina, extended, checked((int)Attributes.SkillGaugePercent),
            Attributes.Experience, experience.GetNextLevelExperience(Attributes.Level));
    }

    // Supported inputs only: no transform look, neutral life tier.
    private WorldHeroMaxStats ResolveMaxima()
    {
        (int itemLife, int itemMana) = WorldHeroItemStats.Resolve(Equipment, _itemStats);
        return WorldHeroMaxStats.Resolve(Attributes, itemLife, itemMana, _mapFlags, hasTransformLook: false,
            lifeTierDigit: 0);
    }

    // Native 1009 subtype 46 clamps synchronously after CHero__RefreshEquipmentDerivedStats (0x6EC479), so a later
    // life write in the same batch is never clamped. Signed CMP/JLE 0x6EC49A/49C, no max>=0 guard; the clamp writes
    // current index1 and HP follower index15 (0x6EC4B8/4C7), never MP.
    private void ClampLifeAfterEquipmentSnapshot()
    {
        if (_hasTransformLookSource)
        {
            return;
        }

        int maximumLife = ResolveMaxima().Life;
        if (unchecked((int)Attributes.CurrentLife) > maximumLife)
        {
            Attributes = Attributes with
            {
                CurrentLife = unchecked((uint)maximumLife)
            };
            _followers.ResetLifeForEquipmentClamp(maximumLife);
        }
    }
}

/// <summary>Resolved model values consumed by presentation, with no HUD or graphics dependency.</summary>
internal readonly record struct WorldHeroVitals(int CurrentLife, int LifeFollower, int MaximumLife,
    int CurrentMana, int ManaFollower, int MaximumMana, int Stamina, int MaximumStamina,
    bool HasExtendedStamina, int SkillGaugePercent, ulong Experience, ulong NextLevelExperience);
