using System.Text;
using System.Text.Json;
using OpenConquer.Client.World;
using OpenConquer.Content.Items;
using OpenConquer.Content.Progression;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldHeroModelTests
{
    [Fact]
    public void MaximaMatchIndependentlyDerivedNativeInstructionGoldenCases()
    {
        using JsonDocument fixture = Fixture();
        foreach (JsonElement row in fixture.RootElement.GetProperty("maxima").EnumerateArray())
        {
            WorldHeroAttributes hero = Hero() with
            {
                Strength = UInt(row, "strength", 5),
                Agility = UInt(row, "agility", 6),
                Vitality = UInt(row, "vitality", 7),
                Spirit = UInt(row, "spirit", 8),
                Profession = UInt(row, "profession", 10),
                MaximumLifeOverride = UInt(row, "maxLife"),
                MaximumManaOverride = UInt(row, "maxMana"),
                OverlayLifeBonus = UInt(row, "overlay"),
            };
            WorldHeroMaxStats actual = WorldHeroMaxStats.Resolve(hero, Int(row, "equippedLife"), Int(row, "equippedMana"),
                ULong(row, "mapFlags"), row.TryGetProperty("transform", out JsonElement transform) && transform.GetBoolean(),
                Int(row, "lifeTier"));
            WorldHeroMaxStats expected = new(Int(row, "life"), Int(row, "mana"));
            Assert.True(actual == expected, $"{row.GetProperty("name").GetString()}: expected {expected}, actual {actual}");
        }
    }

    [Fact]
    public void StaminaMatchesNativeSignedCapAndLowWordStatusGoldenCases()
    {
        using JsonDocument fixture = Fixture();
        ItemStatCatalog catalog = NeutralCatalog(fixture);
        LevelExperienceTable experience = Experience();
        foreach (JsonElement row in fixture.RootElement.GetProperty("stamina").EnumerateArray())
        {
            WorldHeroModel model = Model(Hero() with
            {
                CurrentStamina = UInt(row, "current"),
                StatusLow = ULong(row, "low"),
                StatusHigh = ULong(row, "high"),
            });
            WorldHeroVitals result = Advance(model, catalog, experience);
            Assert.Equal(Int(row, "maximum"), result.MaximumStamina);
            Assert.Equal(Int(row, "value"), result.Stamina);
            Assert.Equal(Int(row, "maximum") == 150, result.HasExtendedStamina);
        }
    }

    [Theory]
    [InlineData(300u, 236u, 236)]
    [InlineData(236u, 236u, 2)]
    [InlineData(200u, 200u, 2)]
    [InlineData(uint.MaxValue, uint.MaxValue, -1)]
    [InlineData(0x80000000u, 0x80000000u, int.MinValue)]
    public void SnapshotUsesSignedCompareAndResetsFollowerOnlyWhenLifeActuallyClamps(uint current, uint expected, int follower)
    {
        using JsonDocument fixture = Fixture();
        WorldHeroModel model = Model(Hero() with
        {
            CurrentLife = current
        }, pendingClamp: true);

        WorldHeroVitals frame = Advance(model, NeutralCatalog(fixture), Experience());

        Assert.Equal(expected, model.Attributes.CurrentLife);
        Assert.Equal(unchecked((int)expected), frame.CurrentLife);
        Assert.Equal(follower, frame.LifeFollower);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NegativeNativeOverrideDoesNotAddANonnegativeClampGuard(bool snapshot)
    {
        using JsonDocument fixture = Fixture();
        WorldHeroModel model = Model(Hero() with
        {
            CurrentLife = 100,
            MaximumLifeOverride = uint.MaxValue
        }, snapshot);

        WorldHeroVitals result = model.AdvanceVitals(NeutralCatalog(fixture), Experience(),
            mapFlags: 0x4000000000UL, hasTransformLook: false, lifeTierDigit: 0);

        Assert.Equal(-1, result.MaximumLife);
        Assert.Equal(snapshot ? -1 : 100, result.CurrentLife);
        Assert.Equal(snapshot ? -1 : 1, result.LifeFollower);
    }

    [Fact]
    public void SnapshotResetsOnlyLifeFollowerAndDoesNotBecomeAPerFrameLifeCap()
    {
        using JsonDocument fixture = Fixture();
        ItemStatCatalog catalog = NeutralCatalog(fixture);
        LevelExperienceTable experience = Experience();
        WorldHeroModel model = Model(Hero() with
        {
            CurrentLife = 300,
            CurrentMana = 60
        });
        WorldHeroVitals first = Advance(model, catalog, experience);
        Assert.Equal((2, 1), (first.LifeFollower, first.ManaFollower));
        model.RecordEquipmentChange(isSnapshot: true);

        WorldHeroVitals clamped = Advance(model, catalog, experience);

        Assert.Equal((236, 236), (clamped.CurrentLife, clamped.LifeFollower));
        Assert.Equal((60, 2), (clamped.CurrentMana, clamped.ManaFollower));
        Assert.True(model.TryApplyAttribute(0, 300, 0));
        WorldHeroVitals later = Advance(model, catalog, experience);
        Assert.Equal((300, 238, 3), (later.CurrentLife, later.LifeFollower, later.ManaFollower));
    }

    [Fact]
    public void NewWorldModelStartsWithFreshFollowers()
    {
        using JsonDocument fixture = Fixture();
        ItemStatCatalog catalog = NeutralCatalog(fixture);
        LevelExperienceTable experience = Experience();
        WorldHeroModel previous = Model(Hero());
        Assert.Equal(2, Advance(previous, catalog, experience).LifeFollower);
        Assert.Equal(4, Advance(previous, catalog, experience).LifeFollower);

        Assert.Equal(2, Advance(Model(Hero()), catalog, experience).LifeFollower);
    }

    [Fact]
    public void EncodedItemTablesAndLiveEquipmentComposeBeforeSnapshotClampAndProjection()
    {
        WorldHeroEquipment equipment = WorldHeroEquipment.FromHydration(
            [WorldHeroItemStatsTests.Item(100, 120020, 40), WorldHeroItemStatsTests.Equipment(100)]);
        WorldHeroModel model = new(Hero() with
        {
            CurrentLife = 600,
            CurrentMana = 50,
            StatusLow = 1UL << 33
        }, equipment, true);
        ItemStatCatalog catalog = WorldHeroItemStatsTests.Catalog();
        LevelExperienceTable experience = Experience();
        Assert.True(model.TryApplyAttribute(27, 150, 0));

        WorldHeroVitals frame = Advance(model, catalog, experience);

        Assert.Equal((532, 532, 532), (frame.CurrentLife, frame.LifeFollower, frame.MaximumLife));
        Assert.Equal((50, 1, 30), (frame.CurrentMana, frame.ManaFollower, frame.MaximumMana));
        Assert.Equal((150, 100, 1234UL, 120UL),
            (frame.MaximumStamina, frame.SkillGaugePercent, frame.Experience, frame.NextLevelExperience));
        byte[] broken = WorldHeroItemStatsTests.Item(100, 120020, 0);
        broken[16] = 3;
        Assert.True(model.Equipment.Apply(broken));
        model.RecordEquipmentChange(isSnapshot: false);
        WorldHeroVitals afterBreak = Advance(model, catalog, experience);
        Assert.Equal((236, 30, 532), (afterBreak.MaximumLife, afterBreak.MaximumMana, afterBreak.CurrentLife));
        Assert.Equal(1u, model.EquipmentRevision);
        model.RecordEquipmentChange(isSnapshot: true);
        WorldHeroVitals afterSnapshot = Advance(model, catalog, experience);
        Assert.Equal((236, 236), (afterSnapshot.CurrentLife, afterSnapshot.LifeFollower));
        Assert.Equal(2u, model.EquipmentRevision);
    }

    private static WorldHeroModel Model(WorldHeroAttributes hero, bool pendingClamp = false) =>
        new(hero, WorldHeroEquipment.FromHydration([]), pendingClamp);

    private static WorldHeroVitals Advance(WorldHeroModel model, ItemStatCatalog catalog, LevelExperienceTable experience) =>
        model.AdvanceVitals(catalog, experience, mapFlags: 0, hasTransformLook: false, lifeTierDigit: 0);

    private static WorldHeroAttributes Hero() => WorldHeroAttributes.FromCharacter(new GameCharacterInfo1006
    {
        EntityId = 1001,
        Strength = 5,
        Agility = 6,
        Vitality = 7,
        Spirit = 8,
        CurrentProfession = 11,
        CurrentLife = 300,
        CurrentMana = 60,
        Level = 1,
        Experience = 1234,
    });

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hero", "native-stats-5517.json")));

    private static ItemStatCatalog NeutralCatalog(JsonDocument fixture) => ItemStatCatalog.Parse(
        Convert.FromBase64String(fixture.RootElement.GetProperty("neutralOriginalItemTypeRowBase64").GetString()!),
        "120020 9 239 0 0 23 0 0 0 0\r\n"u8);

    private static int Int(JsonElement row, string name) => row.TryGetProperty(name, out JsonElement value) ? value.GetInt32() : 0;
    private static uint UInt(JsonElement row, string name, uint fallback = 0) => row.TryGetProperty(name, out JsonElement value) ? value.GetUInt32() : fallback;
    private static ulong ULong(JsonElement row, string name) => row.TryGetProperty(name, out JsonElement value) ? value.GetUInt64() : 0;

    private static LevelExperienceTable Experience()
    {
        // Independent synthetic encoded table: level1 requirement120; not a captured native progression vector.
        byte[] data = Encoding.ASCII.GetBytes("0 1 120 0 0\r\n");
        Span<byte> keys = stackalloc byte[128];
        uint seed = 0x4D2;
        for (int index = 0; index < keys.Length; index++)
        {
            seed = unchecked(seed * 214013 + 2531011);
            keys[index] = (byte)(seed >> 16);
        }
        for (int index = 0; index < data.Length; index++)
        {
            int shift = index & 7;
            int value = data[index];
            data[index] = (byte)(((value << shift) | (value >> (8 - shift))) ^ keys[index % keys.Length]);
        }
        return LevelExperienceTable.Parse(data);
    }
}
