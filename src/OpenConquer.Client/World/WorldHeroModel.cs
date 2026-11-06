using OpenConquer.Content.Items;
using OpenConquer.Content.Progression;

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
    private bool _needsEquipmentLifeClamp;

    internal WorldHeroModel(WorldHeroAttributes attributes, WorldHeroEquipment equipment, bool needsEquipmentLifeClamp)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        Attributes = attributes;
        Equipment = equipment;
        _needsEquipmentLifeClamp = needsEquipmentLifeClamp;
    }

    internal WorldHeroAttributes Attributes
    {
        get;
        private set;
    }

    internal WorldHeroEquipment Equipment
    {
        get;
    }
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

    internal void RecordEquipmentChange(bool isSnapshot)
    {
        EquipmentRevision++;
        _needsEquipmentLifeClamp |= isSnapshot;
    }

    internal void ApplyEquipmentLifeClamp(int maximumLife)
    {
        if (!_needsEquipmentLifeClamp)
        {
            return;
        }

        _needsEquipmentLifeClamp = false;
        // Native 1009 subtype 46: signed CMP/JLE 0x6EC49A/49C; no max>=0 guard.
        // Actual clamp writes both current index1 and follower index15 at 0x6EC4B8/4C7.
        // Consumption is still deferred to the content-ready frame; packet-batch ordering is a CP2.3 residual.
        if (unchecked((int)Attributes.CurrentLife) > maximumLife)
        {
            Attributes = Attributes with
            {
                CurrentLife = unchecked((uint)maximumLife)
            };
            _followers.ResetLifeForEquipmentClamp(maximumLife);
        }
    }

    /// <summary>Called once at the existing guarded world-draw point, after scene drawing.</summary>
    internal WorldHeroVitals AdvanceVitals(ItemStatCatalog catalog, LevelExperienceTable experience,
        ulong mapFlags, bool hasTransformLook, int lifeTierDigit)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(experience);
        (int itemLife, int itemMana) = WorldHeroItemStats.Resolve(Equipment, catalog);
        WorldHeroMaxStats maxima = WorldHeroMaxStats.Resolve(Attributes, itemLife, itemMana,
            mapFlags, hasTransformLook, lifeTierDigit);
        ApplyEquipmentLifeClamp(maxima.Life);
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
}

/// <summary>Resolved model values consumed by presentation, with no HUD or graphics dependency.</summary>
internal readonly record struct WorldHeroVitals(int CurrentLife, int LifeFollower, int MaximumLife,
    int CurrentMana, int ManaFollower, int MaximumMana, int Stamina, int MaximumStamina,
    bool HasExtendedStamina, int SkillGaugePercent, ulong Experience, ulong NextLevelExperience);
