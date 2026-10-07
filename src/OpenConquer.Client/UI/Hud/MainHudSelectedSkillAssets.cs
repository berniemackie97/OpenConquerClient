using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudSelectedSkillAssets
{
    public const string ControlAniPath = "ani/Control.ani";
    public const string MagicAniPath = "ani/Magic.ani";

    private readonly IClientContentSource _contentSource;

    private AniIndexFile? _controlIndex;
    private AniIndexFile? _magicIndex;
    private bool _controlIndexLoaded;
    private bool _magicIndexLoaded;

    public MainHudSelectedSkillAssets(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        _contentSource = contentSource;
    }

    public RgbaImage? GetSelectedFrame(string sectionName, uint frameIndex)
    {
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        return GetFrame(GetMagicIndex(), sectionName, frameIndex, ContentLookupMode.LooseThenPackage);
    }

    public RgbaImage? GetCoverFrame() =>
        GetFrame(GetControlIndex(), MainHudSelectedSkillDefinition.CoverSectionName, frameIndex: 0, ContentLookupMode.LooseThenPackage);

    private AniIndexFile? GetControlIndex() =>
        GetOrLoadIndex(ref _controlIndexLoaded, ref _controlIndex, ControlAniPath, ContentLookupMode.LooseOnly);

    private AniIndexFile? GetMagicIndex() =>
        GetOrLoadIndex(ref _magicIndexLoaded, ref _magicIndex, MagicAniPath, ContentLookupMode.LooseThenPackage);

    private RgbaImage? GetFrame(AniIndexFile? index, string sectionName, uint frameIndex, ContentLookupMode lookupMode)
    {
        if (index is null || !index.TryGetSection(sectionName, out AniIndexSection? section) || section.FrameCount == 0)
        {
            return null;
        }

        try
        {
            return AniFrameLoader.LoadWrappedFrame(_contentSource, section, frameIndex, lookupMode);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private AniIndexFile? GetOrLoadIndex(ref bool loaded, ref AniIndexFile? index, string aniPath, ContentLookupMode lookupMode)
    {
        if (loaded)
        {
            return index;
        }

        try
        {
            index = AniIndexFile.Load(_contentSource, aniPath, lookupMode);
        }
        catch (FileNotFoundException)
        {
            index = null;
        }

        loaded = true;
        return index;
    }
}
