using OpenConquer.Client.World;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldHeroGaugeFollowersTests
{
    [Fact]
    public void RisesByEffectiveMaximumOverEightyAndSnapsDownOnLoss()
    {
        WorldHeroGaugeFollowers followers = new();
        WorldHeroAttributes hero = WorldHeroAttributes.FromCharacter(
            new GameCharacterInfo1006 { CurrentLife = 200, CurrentMana = 30 });

        followers.Advance(hero, new WorldHeroMaxStats(400, 40));
        Assert.Equal((5, 1), (followers.Life, followers.Mana));

        followers.Advance(hero, new WorldHeroMaxStats(400, 40));
        Assert.Equal((10, 2), (followers.Life, followers.Mana));

        followers.Advance(hero with
        {
            CurrentLife = 3,
            CurrentMana = 0,
        },
            new WorldHeroMaxStats(400, 40));
        Assert.Equal((3, 0), (followers.Life, followers.Mana));

        followers.Advance(hero with
        {
            CurrentLife = 4,
            CurrentMana = 2,
        },
            new WorldHeroMaxStats(400, 40));
        Assert.Equal((4, 1), (followers.Life, followers.Mana));
    }
}
