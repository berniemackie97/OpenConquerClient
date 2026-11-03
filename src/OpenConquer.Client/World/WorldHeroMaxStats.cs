namespace OpenConquer.Client.World;

internal readonly record struct WorldHeroMaxStats(int Life, int Mana)
{
    private static ReadOnlySpan<int> LifeTierBonus =>
        [0, 100, 200, 300, 400, 500, 600, 800, 1000, 1200];

    internal static WorldHeroMaxStats Resolve(WorldHeroAttributes hero,
        int equippedLife, int equippedMana, ulong mapFlags, bool hasTransformLook,
        int lifeTierDigit)
    {
        bool overrideMap = (mapFlags & 0x0000004000000000UL) != 0;
        int life;
        if (hasTransformLook || (hero.MaximumLifeOverride > 0 && overrideMap))
        {
            life = unchecked((int)hero.MaximumLifeOverride);
        }
        else
        {
            int initial = unchecked((int)(hero.Strength + hero.Agility +
                hero.Vitality * 8u + hero.Spirit) * 3);
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
            life = unchecked(initial + percent * initial / 100 + equippedLife);
            if ((mapFlags & 0x400000UL) != 0)
            {
                life = unchecked(life + (int)hero.OverlayLifeBonus);
            }

            if (lifeTierDigit is >= 1 and <= 9)
            {
                life = unchecked(life + LifeTierBonus[lifeTierDigit]);
            }

            life = Math.Max(0, life);
        }

        int mana;
        if (hero.MaximumManaOverride > 0 && overrideMap)
        {
            mana = unchecked((int)hero.MaximumManaOverride);
        }
        else
        {
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

            mana = Math.Max(0, unchecked((int)hero.Spirit * multiplier + equippedMana));
        }

        return new WorldHeroMaxStats(life, mana);
    }
}
