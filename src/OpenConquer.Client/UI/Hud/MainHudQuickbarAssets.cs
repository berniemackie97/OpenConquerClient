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
    private readonly Dictionary<string, AniIndexFile?> _indexes = new(StringComparer.Ordinal);
    private readonly Dictionary<(string AniPath, string Section), AniFrameSet?> _sections = [];

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
        (string AniPath, string Section) key = (aniPath, sectionName);

        if (_sections.TryGetValue(key, out AniFrameSet? cached))
        {
            return cached;
        }

        AniIndexFile? index = GetIndex(aniPath);

        if (index is null || !index.TryGetSection(sectionName, out AniIndexSection? section))
        {
            _sections[key] = null;
            return null;
        }

        AniFrameSet? frames;

        try
        {
            frames = AniFrameSetLoader.Load(_contentSource, section, ContentLookupMode.LooseThenPackage);
        }
        catch (FileNotFoundException)
        {
            frames = null;
        }

        _sections[key] = frames;
        return frames;
    }

    private AniIndexFile? GetIndex(string aniPath)
    {
        if (_indexes.TryGetValue(aniPath, out AniIndexFile? cached))
        {
            return cached;
        }

        AniIndexFile? index;

        try
        {
            index = AniIndexFile.Load(_contentSource, aniPath, GetIndexLookupMode(aniPath));
        }
        catch (FileNotFoundException)
        {
            index = null;
        }

        _indexes[aniPath] = index;
        return index;
    }

    private static ContentLookupMode GetIndexLookupMode(string aniPath) =>
        aniPath == ControlAniPath ? ContentLookupMode.LooseOnly : ContentLookupMode.LooseThenPackage;
}
