namespace OpenConquer.Client.World;

internal sealed class WorldHeroGaugeFollowers
{
    private const int RiseDivisor = 80;

    internal int Life
    {
        get;
        private set;
    }

    internal int Mana
    {
        get;
        private set;
    }

    internal void Advance(WorldHeroAttributes hero, WorldHeroMaxStats maxima)
    {
        Life = AdvanceValue(Life, unchecked((int)hero.CurrentLife), maxima.Life);
        Mana = AdvanceValue(Mana, unchecked((int)hero.CurrentMana), maxima.Mana);
    }

    internal void ResetLifeForEquipmentClamp(int maximumLife)
    {
        Life = maximumLife;
    }

    private static int AdvanceValue(int follower, int current, int maximum)
    {
        if (follower >= current)
        {
            return Math.Min(follower, current);
        }

        int step = maximum >= RiseDivisor ? maximum / RiseDivisor : 1;
        follower = unchecked(follower + step);

        return Math.Min(follower, current);
    }
}
