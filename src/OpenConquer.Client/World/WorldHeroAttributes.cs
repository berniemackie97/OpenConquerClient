using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.World;

internal readonly record struct WorldHeroAttributes(
    uint Strength, uint Agility, uint Vitality, uint Spirit,
    uint Level, uint Profession, uint RebirthCount, uint UnspentAttributePoints,
    uint CurrentLife, uint CurrentMana, uint CurrentStamina,
    uint MaximumLifeOverride, uint MaximumManaOverride, uint OverlayLifeBonus,
    ulong Experience, uint SkillGaugePercent, ulong StatusLow, ulong StatusHigh)
{
    internal static WorldHeroAttributes FromCharacter(GameCharacterInfo1006 character) => new(
        character.Strength, character.Agility, character.Vitality, character.Spirit,
        character.Level, character.CurrentProfession, character.RebirthCount,
        character.UnspentAttributePoints, character.CurrentLife, character.CurrentMana,
        0, 0, 0, 0, character.Experience, 0, 0, 0);

    internal bool TryApply(uint attributeId, ulong low, ulong high, out WorldHeroAttributes next)
    {
        next = attributeId switch
        {
            0 => this with { CurrentLife = (uint)low },
            1 => this with { MaximumLifeOverride = (uint)low },
            2 => this with { CurrentMana = (uint)low },
            3 => this with { MaximumManaOverride = (uint)low },
            5 => this with { Experience = low },
            7 => this with { Profession = (uint)low },
            8 => this with { CurrentStamina = (uint)low },
            10 => this with { UnspentAttributePoints = (uint)low },
            12 => this with { Level = (uint)low },
            22 => this with { RebirthCount = (uint)low },
            25 => this with { StatusLow = low, StatusHigh = high },
            27 => this with { SkillGaugePercent = Math.Min((uint)low, 100u) },
            43 => this with { OverlayLifeBonus = (uint)low },
            _ => this,
        };

        return attributeId is 0 or 1 or 2 or 3 or 5 or 7 or 8 or 10 or 12 or 22 or 25 or 27 or 43;
    }
}
