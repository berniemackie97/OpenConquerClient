namespace OpenConquer.Client.World;

internal sealed class WorldHeroGaugeFollowers
{
    // Signed per-frame rule: HP 0x65E128..0x65E167, MP 0x65E1C9..0x65E24C.
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

    // Native equipment clamp updates HP follower index15 only (0x6EC4C7), never the MP follower.
    internal void ResetLifeForEquipmentClamp(int maximumLife) => Life = maximumLife;

    private static int AdvanceValue(int follower, int current, int maximum)
    {
        if (follower < current)
        {
            int step = maximum >= RiseDivisor ? maximum / RiseDivisor : 1;
            follower = unchecked(follower + step);
        }

        return Math.Min(follower, current);
    }
}
