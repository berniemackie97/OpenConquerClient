using System.Globalization;
using System.Text;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;
using OpenConquer.Content.Magic;
using OpenConquer.Content.Text;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints.Magic;

public sealed class MainHudMagicHintTextBuilderTests
{
    [Fact]
    public void Build_UsesNameElementaryLevelExtendedDescriptionAndFixedProgress()
    {
        MagicTypeRecord record = CreateRecord(1000, 0, requiredExperience: 0);

        MainHudMagicHintTextBuilder builder = CreateBuilder(
            "[10000]\nName=Thunder\nDescEx=Magic~attack\n",
            "");

        MainHudMagicHintRuntimeSnapshot runtime = CreateRuntime(
            [new MainHudLearnedMagic(1000, 0, 0)]);

        IReadOnlyList<MainHudMagicHintTextGroup> groups = builder.Build(
            record, new MainHudLearnedMagic(1000, 0, 0), runtime);

        Assert.Equal(
            ["Thunder", "Level: Elementary", "Magic attack", "Fixed"],
            Decode(groups));

        Assert.All(groups, static group => Assert.Equal(SpriteColor.White, group.Color));
    }

    [Fact]
    public void Build_FormatsExperienceWithoutClampingOverfullProgress()
    {
        MagicTypeRecord record = CreateRecord(1000, 1, requiredExperience: 2000);

        MainHudMagicHintTextBuilder builder = CreateBuilder("[10000]\nName=Thunder\n", "");
        MainHudMagicHintRuntimeSnapshot runtime = CreateRuntime(
            [new MainHudLearnedMagic(1000, 1, 3000)]);

        IReadOnlyList<MainHudMagicHintTextGroup> groups = builder.Build(
            record, new MainHudLearnedMagic(1000, 1, 3000), runtime);

        Assert.Equal(
            ["Thunder", "Level: 1", "EXP: 150.000% "],
            Decode(groups));
    }

    [Fact]
    public void Build_UsesDescInsteadOfExperienceWhenCharacterLevelIsInsufficient()
    {
        MagicTypeRecord record = CreateRecord(1000, 1,
            requiredExperience: 2000, requiredCharacterLevel: 100);

        MainHudMagicHintTextBuilder builder = CreateBuilder(
            "[10000]\nName=Thunder\nDesc=Requires~Level~100\nDescEx=Magic~attack\n", "");

        MainHudMagicHintRuntimeSnapshot runtime = CreateRuntime(
            [new MainHudLearnedMagic(1000, 1, 1500)], characterLevel: 90);

        IReadOnlyList<MainHudMagicHintTextGroup> groups = builder.Build(
            record, new MainHudLearnedMagic(1000, 1, 1500), runtime);

        Assert.Equal(
            ["Thunder", "Level: 1", "Magic attack", "Requires Level 100"],
            Decode(groups));
    }

    [Fact]
    public void Build_SelectsWhiteSatisfiedSubprofessionRequirement()
    {
        MagicTypeRecord record = CreateRecord(1415, 0, 0,
            requiredSubprofessionPhase: 3, encodedProfessionRequirement: 1006);

        MainHudMagicHintTextBuilder builder = CreateBuilder(
            "[14150]\nName=Dance\n",
            "STR_MAGIC_REQ_SUBPRO_ACCORD=Requires: P%d %s\n");

        MainHudMagicHintRuntimeSnapshot runtime = CreateRuntime(
            [new MainHudLearnedMagic(1415, 0, 0)],
            activeClass: 6,
            activePhase: 3,
            packedPhases: 300000);

        IReadOnlyList<MainHudMagicHintTextGroup> groups = builder.Build(
            record, new MainHudLearnedMagic(1415, 0, 0), runtime);

        Assert.Contains(groups, static group =>
            Encoding.Latin1.GetString(group.EncodedText.Span) == "Requires: P3 Performer" &&
            group.Color == SpriteColor.White);
    }

    [Fact]
    public void Build_SelectsRedInsufficientPhaseAndDanceWarning()
    {
        MagicTypeRecord record = CreateRecord(1415, 0, 0,
            requiredSubprofessionPhase: 3, encodedProfessionRequirement: 1006);

        MainHudMagicHintTextBuilder builder = CreateBuilder(
            "[14150]\nName=Dance\n",
            "STR_MAGIC_REQ_SUBPRO_UNACCORD_STEP=Requires: P%d %s(Only P%d %s can use %s)\n" +
            "STR_MAGIC_SUBPRO_UNACCORD_SWITCH=Requires: P%d %s(Switch to P%d %s to use %s)\n" +
            "STR_NOT_COOL_DANCE_STATE=Usable only as a Performer.\n");

        MainHudMagicHintRuntimeSnapshot runtime = CreateRuntime(
            [new MainHudLearnedMagic(1415, 0, 0)],
            activeClass: 1,
            activePhase: 1,
            packedPhases: 300000);

        IReadOnlyList<MainHudMagicHintTextGroup> groups = builder.Build(
            record, new MainHudLearnedMagic(1415, 0, 0), runtime);

        Assert.Contains("Requires: P3 Performer(Switch to P3 Performer to use Dance)", Decode(groups));
        Assert.Contains("Usable only as a Performer.", Decode(groups));

        Assert.Contains(groups, static group =>
            Encoding.Latin1.GetString(group.EncodedText.Span) == "Usable only as a Performer." &&
            group.Color == new SpriteColor(255, 0, 0, 255));
    }

    [Fact]
    public void Build_UsesEquippedMountNameAndNativeEscapeConversion()
    {
        MagicTypeRecord record = CreateRecord(7001, 0, 0);

        MainHudMagicHintTextBuilder builder = CreateBuilder(
            "[70010]\nName=Mount\nDescEx=Not~used\n",
            "STR_MOUNT_MAGIC_TIP=Steed: %s, Lineage Level: %d.\\nRight click to mount.\n" +
            "STR_MOUNT_MAGIC_TIP_NO_EQUITMENT=Unequipped\n");

        MainHudMagicHintRuntimeSnapshot runtime = CreateRuntime(
            [new MainHudLearnedMagic(7001, 0, 0)],
            equippedMount: new MainHudEquippedMount("Dragon"u8, 4));

        IReadOnlyList<MainHudMagicHintTextGroup> groups = builder.Build(
            record, new MainHudLearnedMagic(7001, 0, 0), runtime);

        Assert.Contains("Steed: Dragon, Lineage Level: 4.\nRight click to mount.", Decode(groups));
        Assert.DoesNotContain("Not used", Decode(groups));
    }

    [Fact]
    public void Build_RejectsMismatchedStaticAndLearnedRecords()
    {
        MagicTypeRecord record = CreateRecord(1000, 0, 0);

        MainHudMagicHintTextBuilder builder = CreateBuilder("[10000]\nName=Thunder\n", "");
        MainHudMagicHintRuntimeSnapshot runtime = CreateRuntime(
            [new MainHudLearnedMagic(2000, 0, 0)]);

        Assert.Throws<ArgumentException>(() =>
            builder.Build(record, new MainHudLearnedMagic(2000, 0, 0), runtime));
    }

    private static MainHudMagicHintTextBuilder CreateBuilder(string effect, string keyed)
    {
        MagicEffectFile effects = MagicEffectFile.Parse(Encoding.Latin1.GetBytes(effect));
        ClientKeyedStringResources keyedStrings = ClientKeyedStringResources.Parse(Encoding.Latin1.GetBytes(keyed));
        SubProfessionInfoFile subprofessions = SubProfessionInfoFile.Parse("[6]\ntitle=Performer\n"u8);

        ClientStringResources strings = ClientStringResources.Parse(
            "11038=Level: Elementary\n11039=Level: %u\n11040=Fixed\n10019=EXP: %0.3f%% \n"u8);

        return new MainHudMagicHintTextBuilder(effects, strings, keyedStrings, subprofessions);
    }

    private static MainHudMagicHintRuntimeSnapshot CreateRuntime(
        IEnumerable<MainHudLearnedMagic> learned,
        uint characterLevel = 130,
        int activeClass = 0,
        int activePhase = 0,
        long packedPhases = 0,
        MainHudEquippedMount? equippedMount = null)
    {
        return new MainHudMagicHintRuntimeSnapshot(
            learned,
            characterLevel,
            activeClass,
            activePhase,
            packedPhases,
            equippedMount,
            default);
    }

    private static MagicTypeRecord CreateRecord(
        uint type,
        uint level,
        uint requiredExperience,
        uint requiredCharacterLevel = 0,
        uint requiredSubprofessionPhase = 0,
        uint encodedProfessionRequirement = 0)
    {
        string[] fields = new string[48];
        Array.Fill(fields, "0");

        fields[1] = unchecked((int)type).ToString(CultureInfo.InvariantCulture);
        fields[3] = "Magic";
        fields[8] = unchecked((int)level).ToString(CultureInfo.InvariantCulture);
        fields[18] = unchecked((int)requiredExperience).ToString(CultureInfo.InvariantCulture);
        fields[20] = unchecked((int)requiredCharacterLevel).ToString(CultureInfo.InvariantCulture);
        fields[41] = unchecked((int)encodedProfessionRequirement).ToString(CultureInfo.InvariantCulture);
        fields[42] = unchecked((int)requiredSubprofessionPhase).ToString(CultureInfo.InvariantCulture);

        byte[] encoded = Encoding.ASCII.GetBytes(string.Join("@@", fields) + "@@");
        Span<byte> key = stackalloc byte[128];
        uint state = 0x2537;

        for (int index = 0; index < key.Length; index++)
        {
            state = unchecked(state * 214013 + 2531011);
            key[index] = (byte)(state >> 16);
        }

        for (int index = 0; index < encoded.Length; index++)
        {
            int rotation = index & 7;
            byte value = encoded[index];
            byte rotated = unchecked((byte)((value << rotation) | (value >> (8 - rotation))));
            encoded[index] = (byte)(rotated ^ key[index % key.Length]);
        }

        MagicTypeFile file = MagicTypeFile.Parse(encoded);

        Assert.True(file.TryGet(type, level, out MagicTypeRecord? record));
        return record;
    }

    private static string[] Decode(IReadOnlyList<MainHudMagicHintTextGroup> groups)
    {
        return groups.Select(static group => Encoding.Latin1.GetString(group.EncodedText.Span)).ToArray();
    }
}
