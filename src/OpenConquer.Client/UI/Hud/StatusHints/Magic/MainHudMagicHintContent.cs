using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Magic;
using OpenConquer.Content.Text;

namespace OpenConquer.Client.UI.Hud.StatusHints.Magic;

internal sealed class MainHudMagicHintContent
{
    private MainHudMagicHintContent(MagicTypeFile types, MagicEffectFile effects, ClientKeyedStringResources keyedStrings, SubProfessionInfoFile subprofessions, TryWrapTipConfiguration tryWrapTip)
    {
        Types = types;
        Effects = effects;
        KeyedStrings = keyedStrings;
        Subprofessions = subprofessions;
        TryWrapTip = tryWrapTip;
    }

    public MagicTypeFile Types
    {
        get;
    }

    public MagicEffectFile Effects
    {
        get;
    }

    public ClientKeyedStringResources KeyedStrings
    {
        get;
    }

    public SubProfessionInfoFile Subprofessions
    {
        get;
    }

    public TryWrapTipConfiguration TryWrapTip
    {
        get;
    }

    public static MainHudMagicHintContent Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        return new MainHudMagicHintContent(MagicTypeFile.Load(contentSource), MagicEffectFile.Load(contentSource), ClientKeyedStringResources.Load(contentSource), SubProfessionInfoFile.Load(contentSource), TryWrapTipConfiguration.Load(contentSource));
    }
}
