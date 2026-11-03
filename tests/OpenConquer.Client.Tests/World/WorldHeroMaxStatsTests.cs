using OpenConquer.Client.World;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldHeroMaxStatsTests
{
    [Fact]
    public void AppliesLifeProfessionPercentageAndDefaultManaMultiplier()
    {
        WorldHeroMaxStats maxima = WorldHeroMaxStats.Resolve(Hero(profession: 11),
            equippedLife: 3, equippedMana: 4, mapFlags: 0,
            hasTransformLook: false, lifeTierDigit: 0);

        Assert.Equal(new WorldHeroMaxStats(239, 44), maxima);
    }

    [Fact]
    public void OverrideMapGateUsesHighFlagBitAndManaIgnoresTransformation()
    {
        WorldHeroAttributes hero = Hero(profession: 11) with
        {
            MaximumLifeOverride = 800,
            MaximumManaOverride = 700,
        };

        Assert.Equal(new WorldHeroMaxStats(236, 40),
            WorldHeroMaxStats.Resolve(hero, 0, 0, mapFlags: 0x40UL,
                hasTransformLook: false, lifeTierDigit: 0));
        Assert.Equal(new WorldHeroMaxStats(800, 700),
            WorldHeroMaxStats.Resolve(hero, 0, 0, mapFlags: 0x0000004000000000UL,
                hasTransformLook: false, lifeTierDigit: 0));
        Assert.Equal(new WorldHeroMaxStats(800, 40),
            WorldHeroMaxStats.Resolve(hero, 0, 0, mapFlags: 0,
                hasTransformLook: true, lifeTierDigit: 0));
    }

    [Fact]
    public void TransformationReturnsLifeOverrideEvenWhenZeroAndManaUsesProfessionTensDigit()
    {
        Assert.Equal(new WorldHeroMaxStats(0, 120),
            WorldHeroMaxStats.Resolve(Hero(profession: 132), 0, 0, 0,
                hasTransformLook: true, lifeTierDigit: 0));
    }

    [Fact]
    public void AppliesOverlayAndPackedLifeTierButNotToMana()
    {
        WorldHeroAttributes hero = Hero(profession: 10) with
        {
            OverlayLifeBonus = 25,
        };
        Assert.Equal(new WorldHeroMaxStats(225 + 25 + 1200, 40),
            WorldHeroMaxStats.Resolve(hero, 0, 0, 0x400000UL,
                hasTransformLook: false, lifeTierDigit: 9));
    }

    [Fact]
    public void NegativeEffectiveStatContributionsClampOnlyAfterFinalSum()
    {
        Assert.Equal(new WorldHeroMaxStats(0, 0),
            WorldHeroMaxStats.Resolve(Hero(profession: 10),
                equippedLife: -1000, equippedMana: -1000, mapFlags: 0,
                hasTransformLook: false, lifeTierDigit: 0));
    }

    private static WorldHeroAttributes Hero(uint profession) =>
        WorldHeroAttributes.FromCharacter(new GameCharacterInfo1006
        {
            EntityId = 1001,
            Strength = 5,
            Agility = 6,
            Vitality = 7,
            Spirit = 8,
            CurrentProfession = (byte)profession,
        });
}
