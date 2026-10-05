using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudQuickbarAssets
{
    public const string ControlAniPath = "ani/Control.ani";
    public const string MagicAniPath = "ani/Magic.ani";
    public const string ItemMinIconAniPath = "ani/ItemMinIcon.Ani";
    public const string EffectAniPath = "ani/effect.ani";

    private readonly IClientContentSource _contentSource;

    private AniIndexFile? _controlIndex;
    private AniIndexFile? _magicIndex;
    private AniIndexFile? _itemMinIconIndex;
    private AniIndexFile? _effectIndex;
    private bool _controlIndexLoaded;
    private bool _magicIndexLoaded;
    private bool _itemMinIconIndexLoaded;
    private bool _effectIndexLoaded;

    public MainHudQuickbarAssets(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        _contentSource = contentSource;
    }

    public RgbaImage? GetCoverFrame()
    {
        AniFrameSet? frames = GetFrames(ControlAniPath, MainHudQuickbarDefinition.CoverAniSectionName);

        if (frames is null)
        {
            return null;
        }

        if (frames.FrameCount != MainHudQuickbarDefinition.CoverFrameCount)
        {
            throw new InvalidDataException($"ANI section [{MainHudQuickbarDefinition.CoverAniSectionName}] must contain exactly {MainHudQuickbarDefinition.CoverFrameCount} frame; found {frames.FrameCount}.");
        }

        return frames.GetFrame(0);
    }

    public RgbaImage? GetControlFrame0(string sectionName) => GetFrame0(ControlAniPath, sectionName);
    public RgbaImage? GetMagicFrame0(string sectionName) => GetFrame0(MagicAniPath, sectionName);
    public AniFrameSet? GetEffectFrames(string sectionName) => GetFrames(EffectAniPath, sectionName);

    public RgbaImage? GetItemFrame(uint iconKey)
    {
        if (iconKey == 0)
        {
            return null;
        }

        return GetFrame0(ItemMinIconAniPath, $"Item{iconKey}") ?? GetFrame0(ItemMinIconAniPath, "ItemDefault");
    }

    private RgbaImage? GetFrame0(string aniPath, string sectionName) => GetFrames(aniPath, sectionName)?.GetFrame(0);

    private AniFrameSet? GetFrames(string aniPath, string sectionName)
    {
        AniIndexFile? index = GetIndex(aniPath);

        if (index is null || !index.TryGetSection(sectionName, out AniIndexSection? section) || section.FrameCount == 0)
        {
            return null;
        }

        try
        {
            return AniFrameSetLoader.Load(_contentSource, section, ContentLookupMode.LooseThenPackage);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private AniIndexFile? GetIndex(string aniPath) =>
        aniPath switch
        {
            ControlAniPath => GetOrLoadIndex(ref _controlIndexLoaded, ref _controlIndex, ControlAniPath, ContentLookupMode.LooseOnly),
            MagicAniPath => GetOrLoadIndex(ref _magicIndexLoaded, ref _magicIndex, MagicAniPath, ContentLookupMode.LooseThenPackage),
            ItemMinIconAniPath => GetOrLoadIndex(ref _itemMinIconIndexLoaded, ref _itemMinIconIndex, ItemMinIconAniPath, ContentLookupMode.LooseThenPackage),
            EffectAniPath => GetOrLoadIndex(ref _effectIndexLoaded, ref _effectIndex, EffectAniPath, ContentLookupMode.LooseThenPackage),
            _ => throw new ArgumentOutOfRangeException(nameof(aniPath), aniPath, "Unknown quickbar ANI index."),
        };

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
