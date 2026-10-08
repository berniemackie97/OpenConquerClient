using OpenConquer.Content.Ani;
using OpenConquer.Content.Text;

namespace OpenConquer.Content;

internal static class MainHudStatusHintContentRequirements
{
    public const string DialogSectionName = "Dialog21";
    public const string SkillRegionPath = "ini/ProgressXp.rgn";
    public const string ManaRegionPath = "ini/ProgressMp.rgn";
    public const string LifeRegionPath = "ini/ProgressHp.rgn";

    public static void Add(List<ClientContentRequirement> requirements, AniIndexFile controlAni)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(controlAni);

        requirements.Add(new ClientContentRequirement(ClientStringResources.RelativePath, ContentLookupMode.LooseOnly));
        requirements.Add(new ClientContentRequirement(SkillRegionPath, ContentLookupMode.LooseOnly));
        requirements.Add(new ClientContentRequirement(ManaRegionPath, ContentLookupMode.LooseOnly));
        requirements.Add(new ClientContentRequirement(LifeRegionPath, ContentLookupMode.LooseOnly));

        AniIndexSection section = controlAni.GetRequiredSection(DialogSectionName);

        if (section.FrameCount != 1)
        {
            throw new InvalidDataException($"ANI section [{DialogSectionName}] must contain exactly 1 frame; found {section.FrameCount}.");
        }

        requirements.Add(new ClientContentRequirement(section.FramePaths[0], ContentLookupMode.LooseThenPackage));
    }
}
