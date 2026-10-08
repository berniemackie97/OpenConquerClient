using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Content.Regions;
using OpenConquer.Content.Text;

namespace OpenConquer.Client.UI.Hud.StatusHints;

internal sealed class MainHudStatusHintAssets
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string DialogSectionName = "Dialog21";
    private const string SkillRegionPath = "ini/ProgressXp.rgn";
    private const string ManaRegionPath = "ini/ProgressMp.rgn";
    private const string LifeRegionPath = "ini/ProgressHp.rgn";

    private MainHudStatusHintAssets(ClientStringResources strings, ClientRegionFile skillRegion, ClientRegionFile manaRegion, ClientRegionFile lifeRegion, RgbaImage? backdrop)
    {
        Strings = strings;
        SkillRegion = skillRegion;
        ManaRegion = manaRegion;
        LifeRegion = lifeRegion;
        Backdrop = backdrop;
    }

    public ClientStringResources Strings
    {
        get;
    }

    public ClientRegionFile SkillRegion
    {
        get;
    }

    public ClientRegionFile ManaRegion
    {
        get;
    }

    public ClientRegionFile LifeRegion
    {
        get;
    }

    public RgbaImage? Backdrop
    {
        get;
    }

    public static MainHudStatusHintAssets Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        ClientStringResources strings = ClientStringResources.Load(contentSource);
        ClientRegionFile skillRegion = ClientRegionFile.Load(contentSource, SkillRegionPath);
        ClientRegionFile manaRegion = ClientRegionFile.Load(contentSource, ManaRegionPath);
        ClientRegionFile lifeRegion = ClientRegionFile.Load(contentSource, LifeRegionPath);
        RgbaImage? backdrop = LoadBackdrop(contentSource);

        return new MainHudStatusHintAssets(strings, skillRegion, manaRegion, lifeRegion, backdrop);
    }

    private static RgbaImage? LoadBackdrop(IClientContentSource contentSource)
    {
        AniIndexFile controlAni;

        try
        {
            controlAni = AniIndexFile.Load(contentSource, ControlAniPath, ContentLookupMode.LooseOnly);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        if (!controlAni.TryGetSection(DialogSectionName, out AniIndexSection? section))
        {
            return null;
        }

        if (section.FrameCount != MainHudStatusHintDefinition.Dialog21FrameCount)
        {
            throw new InvalidDataException($"ANI section [{section.Name}] must contain exactly {MainHudStatusHintDefinition.Dialog21FrameCount} frame; found {section.FrameCount}.");
        }

        AniFrameSet frames;

        try
        {
            frames = AniFrameSetLoader.Load(contentSource, section, ContentLookupMode.LooseThenPackage);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        RgbaImage image = frames.GetFrame(0);

        if (image.Width != MainHudStatusHintDefinition.Dialog21TextureWidth ||
            image.Height != MainHudStatusHintDefinition.Dialog21TextureHeight)
        {
            throw new InvalidDataException($"ANI section [{section.Name}] frame 0 decoded as {image.Width}x{image.Height}; expected 256x256.");
        }

        return image;
    }
}
