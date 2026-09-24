using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudChromeAssets
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string ProgressSectionName = "Progress45";
    private const string DialogSectionName = "Dialog4";

    private MainHudChromeAssets(RgbaImage? progressBackground, RgbaImage? dialogFrame0, RgbaImage? dialogFrame1)
    {
        ProgressBackground = progressBackground;
        DialogFrame0 = dialogFrame0;
        DialogFrame1 = dialogFrame1;
    }

    public RgbaImage? ProgressBackground
    {
        get;
    }

    public RgbaImage? DialogFrame0
    {
        get;
    }

    public RgbaImage? DialogFrame1
    {
        get;
    }
    public bool HasDialogPanels => DialogFrame0 is not null && DialogFrame1 is not null;

    public static MainHudChromeAssets Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        AniIndexFile controlAni;

        try
        {
            controlAni = AniIndexFile.Load(contentSource, ControlAniPath, ContentLookupMode.LooseOnly);
        }
        catch (FileNotFoundException)
        {
            return new MainHudChromeAssets(null, null, null);
        }

        RgbaImage? progressBackground = LoadProgressBackground(contentSource, controlAni);
        (RgbaImage? dialogFrame0, RgbaImage? dialogFrame1) = LoadDialogFrames(contentSource, controlAni);

        return new MainHudChromeAssets(progressBackground, dialogFrame0, dialogFrame1);
    }

    private static RgbaImage? LoadProgressBackground(IClientContentSource contentSource, AniIndexFile controlAni)
    {
        if (!controlAni.TryGetSection(ProgressSectionName, out AniIndexSection? section))
        {
            return null;
        }

        if (section.FrameCount != 1)
        {
            throw new InvalidDataException($"ANI section [{ProgressSectionName}] must contain exactly 1 frame; found {section.FrameCount}.");
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

        RgbaImage frame = frames.GetFrame(0);

        ValidateDimensions(frame, ProgressSectionName, 0, 256, 256);

        return frame;
    }

    private static (RgbaImage? Frame0, RgbaImage? Frame1) LoadDialogFrames(IClientContentSource contentSource, AniIndexFile controlAni)
    {
        if (!controlAni.TryGetSection(DialogSectionName, out AniIndexSection? section))
        {
            return (null, null);
        }

        if (section.FrameCount != 2)
        {
            throw new InvalidDataException($"ANI section [{DialogSectionName}] must contain exactly 2 frames; found {section.FrameCount}.");
        }

        AniFrameSet frames;

        try
        {
            frames = AniFrameSetLoader.Load(contentSource, section, ContentLookupMode.LooseThenPackage);
        }
        catch (FileNotFoundException)
        {
            return (null, null);
        }

        RgbaImage frame0 = frames.GetFrame(0);
        RgbaImage frame1 = frames.GetFrame(1);

        ValidateDimensions(frame0, DialogSectionName, 0, 256, 256);
        ValidateDimensions(frame1, DialogSectionName, 1, 256, 128);

        return (frame0, frame1);
    }

    private static void ValidateDimensions(RgbaImage image, string sectionName, int frameIndex, int expectedWidth, int expectedHeight)
    {
        if (image.Width != expectedWidth || image.Height != expectedHeight)
        {
            throw new InvalidDataException($"ANI section [{sectionName}] frame {frameIndex} decoded as {image.Width}x{image.Height}; expected {expectedWidth}x{expectedHeight}.");
        }
    }
}
