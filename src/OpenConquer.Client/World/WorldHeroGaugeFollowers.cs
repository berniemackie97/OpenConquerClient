namespace OpenConquer.Client.World;

internal sealed class WorldHeroGaugeFollowers
{
    internal int Life { get; private set; }
    internal int Mana { get; private set; }

    internal void Advance(WorldHeroAttributes hero, WorldHeroMaxStats maxima)
    {
        Life = AdvanceValue(Life, unchecked((int)hero.CurrentLife), maxima.Life);
        Mana = AdvanceValue(Mana, unchecked((int)hero.CurrentMana), maxima.Mana);
    }

    private static int AdvanceValue(int follower, int current, int maximum)
    {
        if (follower < current)
        {
            int step = maximum >= 80 ? maximum / 80 : 1;
            follower = unchecked(follower + step);
        }

        return Math.Min(follower, current);
    }
}
