using System.Globalization;
using System.Text;
using OpenConquer.Client.UI.Hud.StatusHints.Magic;
using OpenConquer.Content.Magic;
using OpenConquer.Content.Text;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.Tests.UI.Hud.StatusHints.Magic;

public sealed class MainHudMagicHintCatalogPreviewTests
{
    [Fact]
    public void UnlearnedDance_ShowsMissingSkillWarningInsteadOfPerformerWarning()
    {
        MainHudMagicHintRuntimeSnapshot runtime = CreateRuntime([], 0);
        IReadOnlyList<MainHudMagicHintTextGroup> groups = CreateBuilder().BuildCatalogPreview(CreateRecord(1415), runtime);

        Assert.Equal(["BattleDance", "Level: Elementary", "Unique Dance", "Fixed", "You haven`t learned a dance skill."], Decode(groups));
        Assert.Equal(new SpriteColor(255, 0, 0, 255), groups[^1].Color);
    }

    [Fact]
    public void LearnedDance_UsesStaticLevelZeroAndPerformerWarning()
    {
        MainHudMagicHintRuntimeSnapshot runtime = CreateRuntime([new MainHudLearnedMagic(1415, 5, 999999)], 0);
        Assert.Equal(["BattleDance", "Level: Elementary", "Unique Dance", "Fixed", "Usable only as a Performer."], Decode(CreateBuilder().BuildCatalogPreview(CreateRecord(1415), runtime)));
    }

    [Fact]
    public void LearnedPerformerDance_DoesNotEmitEitherWarning()
    {
        MainHudMagicHintRuntimeSnapshot runtime = CreateRuntime([new MainHudLearnedMagic(1415, 1, 100)], 6);
        Assert.Equal(["BattleDance", "Level: Elementary", "Unique Dance", "Fixed"], Decode(CreateBuilder().BuildCatalogPreview(CreateRecord(1415), runtime)));
    }

    [Fact]
    public void NonDancePreview_DoesNotInventDanceWarnings()
    {
        Assert.Equal(["Thunder", "Level: Elementary", "Unique Dance", "Fixed"], Decode(CreateBuilder().BuildCatalogPreview(CreateRecord(1000), CreateRuntime([], 0))));
    }

    private static MainHudMagicHintTextBuilder CreateBuilder()
    {
        MagicEffectFile effects = MagicEffectFile.Parse("[14150]\nName=BattleDance\nDescEx=Unique Dance\n[10000]\nName=Thunder\nDescEx=Unique Dance\n"u8);
        ClientStringResources numeric = ClientStringResources.Parse("11038=Level: Elementary\n11040=Fixed\n"u8);
        ClientKeyedStringResources keyed = ClientKeyedStringResources.Parse("STR_NOTSTUDY_DANCE=You haven`t learned a dance skill.\nSTR_NOT_COOL_DANCE_STATE=Usable only as a Performer.\n"u8);
        SubProfessionInfoFile subprofessions = SubProfessionInfoFile.Parse("[6]\ntitle=Performer\n"u8);
        return new MainHudMagicHintTextBuilder(effects, numeric, keyed, subprofessions);
    }

    private static MainHudMagicHintRuntimeSnapshot CreateRuntime(IEnumerable<MainHudLearnedMagic> learned, int activeClass) => new(learned, 130, activeClass, 0, 0, null, default);

    private static MagicTypeRecord CreateRecord(uint type)
    {
        string[] fields = new string[48];
        Array.Fill(fields, "0");
        fields[1] = type.ToString(CultureInfo.InvariantCulture);
        fields[3] = "Magic";

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
            encoded[index] = unchecked((byte)(((value << rotation) | (value >> (8 - rotation))) ^ key[index % key.Length]));
        }

        MagicTypeFile file = MagicTypeFile.Parse(encoded);
        Assert.True(file.TryGet(type, 0, out MagicTypeRecord? record));
        return record;
    }

    private static string[] Decode(IReadOnlyList<MainHudMagicHintTextGroup> groups) => groups.Select(static group => Encoding.Latin1.GetString(group.EncodedText.Span)).ToArray();
}
