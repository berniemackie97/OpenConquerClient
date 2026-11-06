namespace OpenConquer.Client.World;

internal readonly record struct WorldHeroMaxStats(int Life, int Mana)
{
    // CHero_GetEffectiveMaxLife 0x65D1DF..0x65D239: guarded tier index 1..9, with a zero entry at index0.
    private static ReadOnlySpan<int> LifeTierBonus =>
        [0, 100, 200, 300, 400, 500, 600, 800, 1000, 1200];

    internal static WorldHeroMaxStats Resolve(WorldHeroAttributes hero,
        int equippedLife, int equippedMana, ulong mapFlags, bool hasTransformLook,
        int lifeTierDigit)
    {
        // 0x6206C8 masks HIGH dword 0x40 (bit38); life also checks transform at 0x65D098.
        bool overrideMap = (mapFlags & 0x0000004000000000UL) != 0;
        int life;
        if (hasTransformLook || (hero.MaximumLifeOverride > 0 && overrideMap))
        {
            life = unchecked((int)hero.MaximumLifeOverride);
        }
        else
        {
            // 0x65D0C6/DC: vitality*8, sum*3. 0x65D0D1..0x65D0EF: unsigned %1000 /10 ==1.
            int initial = unchecked((int)(hero.Strength + hero.Agility +
                hero.Vitality * 8u + hero.Spirit) * 3);
            // Rank %10 and 5/8/10/12/15 table: 0x65D0F1..0x65D11F.
            int percent = (hero.Profession % 1000u) / 10u == 1u
                ? (hero.Profession % 10u) switch
                {
                    1 => 5,
                    2 => 8,
                    3 => 10,
                    4 => 12,
                    5 => 15,
                    _ => 0,
                } : 0;
            // Signed /100 at 0x65D123..0x65D129, wrapping add; overlay mask 0x61DFF5.
            life = unchecked(initial + percent * initial / 100 + equippedLife);
            if ((mapFlags & 0x400000UL) != 0)
            {
                life = unchecked(life + (int)hero.OverlayLifeBonus);
            }

            if (lifeTierDigit is >= 1 and <= 9)
            {
                life = unchecked(life + LifeTierBonus[lifeTierDigit]);
            }

            life = Math.Max(0, life); // Signed final floor at 0x65D23D..0x65D245.
        }

        int mana;
        // 0x65D25E..0x65D284: mana override never consults transformation state.
        if (hero.MaximumManaOverride > 0 && overrideMap)
        {
            mana = unchecked((int)hero.MaximumManaOverride);
        }
        else
        {
            // 0x65D290..0x65D2EA: default5, unsigned family>10, rank multipliers 10/15/20/25/30.
            int multiplier = 5;
            if ((hero.Profession % 1000u) / 10u > 10u)
            {
                multiplier = (hero.Profession % 10u) switch
                {
                    >= 5 => 30,
                    4 => 25,
                    3 => 20,
                    2 => 15,
                    1 => 10,
                    _ => 5,
                };
            }

            mana = Math.Max(0, unchecked((int)hero.Spirit * multiplier + equippedMana)); // Signed floor 0x65D30D.
        }

        return new WorldHeroMaxStats(life, mana);
    }
}
